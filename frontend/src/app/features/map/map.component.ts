import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
  ViewChild,
  computed,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import {
  GeoJSONSource,
  LngLatBoundsLike,
  Map as MapLibreMap,
  Marker,
  StyleSpecification,
} from 'maplibre-gl';
import type { FeatureCollection, Feature, LineString, Point } from 'geojson';
import type { LatLon, PoiOnRoute, StageDto, TrackResponse } from '../tracks/track.models';

const IGN_STYLE: StyleSpecification = {
  version: 8,
  sources: {
    ign: {
      type: 'raster',
      tiles: [
        'https://data.geopf.fr/wmts?SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0&LAYER=GEOGRAPHICALGRIDSYSTEMS.PLANIGNV2&STYLE=normal&TILEMATRIXSET=PM&FORMAT=image/png&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}',
      ],
      tileSize: 256,
      attribution:
        '<a href="https://www.ign.fr/" target="_blank">IGN-F/Géoportail</a>',
    },
  },
  layers: [
    {
      id: 'ign-layer',
      type: 'raster',
      source: 'ign',
    },
  ],
};

const TRACK_SOURCE_ID = 'track';
const TRACK_LAYER_ID = 'track-line';
const STAGES_SOURCE_ID = 'stages';
const STAGES_LAYER_ID = 'stages-line';
const STAGE_ENDS_SOURCE_ID = 'stage-ends';
const STAGE_ENDS_CIRCLE_LAYER_ID = 'stage-ends-circle';
const STAGE_ENDS_LABEL_LAYER_ID = 'stage-ends-label';
const POIS_SOURCE_ID = 'pois';
const POIS_CIRCLE_LAYER_ID = 'pois-circle';
const POIS_LABEL_LAYER_ID = 'pois-label';

const STAGE_COLOR_EXPR = [
  'match',
  ['%', ['get', 'stageIndex'], 2],
  1, '#10b981',
  '#0ea5e9',
] as const;

@Component({
  selector: 'app-map',
  standalone: true,
  template: `<div #mapContainer style="position:absolute; inset:0;"></div>`,
  styles: [
    `
      :host {
        display: block;
        position: absolute;
        inset: 0;
      }
    `,
  ],
})
export class MapComponent implements AfterViewInit, OnDestroy {
  @ViewChild('mapContainer', { static: true })
  private readonly container!: ElementRef<HTMLDivElement>;

  readonly track = input<TrackResponse | null>(null);
  readonly startPoint = input<LatLon | null>(null);
  readonly endPoint = input<LatLon | null>(null);
  readonly focusBbox = input<[number, number, number, number] | null>(null);
  readonly mapClick = output<LatLon>();

  private map?: MapLibreMap;
  private startMarker?: Marker;
  private endMarker?: Marker;
  private readonly ready = signal(false);

  private readonly proposedEndFromTrack = computed<LatLon | null>(() => {
    const t = this.track();
    if (!t?.proposedDestinationName) return null;
    const coords = t.geojson.geometry.coordinates;
    if (coords.length === 0) return null;
    const last = coords[coords.length - 1];
    return { lat: last[1], lon: last[0] };
  });

  constructor() {
    effect(() => {
      const t = this.track();
      if (!this.ready() || !this.map) return;
      this.renderTrack(t);
    });
    effect(() => {
      const p = this.startPoint();
      if (!this.map) return;
      this.startMarker = this.renderMarker(this.startMarker, p, '#f59e0b');
    });
    effect(() => {
      const explicit = this.endPoint();
      const proposed = this.proposedEndFromTrack();
      if (!this.map) return;
      this.endMarker = this.renderMarker(this.endMarker, explicit ?? proposed, '#ef4444');
    });
    effect(() => {
      const bbox = this.focusBbox();
      if (!this.ready() || !this.map || !bbox) return;
      const [minLon, minLat, maxLon, maxLat] = bbox;
      this.map.fitBounds(
        [[minLon, minLat], [maxLon, maxLat]] as LngLatBoundsLike,
        { padding: 80, duration: 600 },
      );
    });
  }

  ngAfterViewInit(): void {
    this.map = new MapLibreMap({
      container: this.container.nativeElement,
      style: IGN_STYLE,
      center: [2.5, 46.5],
      zoom: 5.5,
      attributionControl: { compact: true },
    });
    this.map.on('load', () => {
      this.ready.set(true);
      this.map?.resize();
    });
    this.map.on('click', (e) => {
      this.mapClick.emit({ lat: e.lngLat.lat, lon: e.lngLat.lng });
    });
    this.map.getCanvas().style.cursor = 'crosshair';
    requestAnimationFrame(() => this.map?.resize());
    setTimeout(() => this.map?.resize(), 100);
  }

  ngOnDestroy(): void {
    this.startMarker?.remove();
    this.endMarker?.remove();
    this.map?.remove();
    this.map = undefined;
  }

  private renderTrack(track: TrackResponse | null): void {
    if (!track) {
      this.removeSingleTrackLayer();
      this.removeStageLayers();
      this.removePoisLayers();
      return;
    }

    if (track.stages && track.stages.length > 0) {
      this.removeSingleTrackLayer();
      this.renderStages(track.stages);
    } else {
      this.removeStageLayers();
      this.renderSingleTrack(track.geojson);
    }

    this.renderPois(track.poisOnRoute ?? []);

    if (track.bbox) {
      const [minLon, minLat, maxLon, maxLat] = track.bbox;
      this.map!.fitBounds(
        [[minLon, minLat], [maxLon, maxLat]] as LngLatBoundsLike,
        { padding: 60, duration: 600 },
      );
    }
  }

  private renderSingleTrack(geojson: Feature<LineString>): void {
    const map = this.map!;
    const existing = map.getSource(TRACK_SOURCE_ID) as GeoJSONSource | undefined;
    if (existing) {
      existing.setData(geojson);
      return;
    }
    map.addSource(TRACK_SOURCE_ID, { type: 'geojson', data: geojson });
    map.addLayer({
      id: TRACK_LAYER_ID,
      type: 'line',
      source: TRACK_SOURCE_ID,
      layout: { 'line-cap': 'round', 'line-join': 'round' },
      paint: {
        'line-color': '#10b981',
        'line-width': 4,
        'line-opacity': 0.9,
      },
    });
  }

  private renderStages(stages: readonly StageDto[]): void {
    const map = this.map!;

    const lineFC: FeatureCollection<LineString> = {
      type: 'FeatureCollection',
      features: stages.map((s) => ({
        type: 'Feature',
        geometry: s.geojson.geometry,
        properties: { stageIndex: s.index },
      })),
    };

    const existingLines = map.getSource(STAGES_SOURCE_ID) as GeoJSONSource | undefined;
    if (existingLines) {
      existingLines.setData(lineFC);
    } else {
      map.addSource(STAGES_SOURCE_ID, { type: 'geojson', data: lineFC });
      map.addLayer({
        id: STAGES_LAYER_ID,
        type: 'line',
        source: STAGES_SOURCE_ID,
        layout: { 'line-cap': 'round', 'line-join': 'round' },
        paint: {
          'line-color': STAGE_COLOR_EXPR as unknown as string,
          'line-width': 4,
          'line-opacity': 0.9,
        },
      });
    }

    // Intermediate sleep spots only — the final Arrival is already marked by the red end marker.
    const endsFC: FeatureCollection<Point> = {
      type: 'FeatureCollection',
      features: stages
        .filter((s) => s.endSleepSpot.kind !== 'arrival')
        .map<Feature<Point>>((s) => ({
          type: 'Feature',
          geometry: {
            type: 'Point',
            coordinates: [s.endSleepSpot.longitude, s.endSleepSpot.latitude],
          },
          properties: { stageIndex: s.index, label: String(s.index) },
        })),
    };

    const existingEnds = map.getSource(STAGE_ENDS_SOURCE_ID) as GeoJSONSource | undefined;
    if (existingEnds) {
      existingEnds.setData(endsFC);
    } else {
      map.addSource(STAGE_ENDS_SOURCE_ID, { type: 'geojson', data: endsFC });
      map.addLayer({
        id: STAGE_ENDS_CIRCLE_LAYER_ID,
        type: 'circle',
        source: STAGE_ENDS_SOURCE_ID,
        paint: {
          'circle-radius': 10,
          'circle-color': STAGE_COLOR_EXPR as unknown as string,
          'circle-stroke-width': 2,
          'circle-stroke-color': '#0f172a',
        },
      });
      map.addLayer({
        id: STAGE_ENDS_LABEL_LAYER_ID,
        type: 'symbol',
        source: STAGE_ENDS_SOURCE_ID,
        layout: {
          'text-field': ['get', 'label'],
          'text-size': 11,
          'text-font': ['Noto Sans Bold'],
          'text-allow-overlap': true,
        },
        paint: {
          'text-color': '#0f172a',
        },
      });
    }
  }

  private renderPois(pois: PoiOnRoute[]): void {
    const map = this.map!;
    const fc: FeatureCollection<Point> = {
      type: 'FeatureCollection',
      features: pois.map((p) => ({
        type: 'Feature' as const,
        geometry: { type: 'Point' as const, coordinates: [p.longitude, p.latitude] },
        properties: { label: String(p.monumentCount), name: p.communeName },
      })),
    };
    const existing = map.getSource(POIS_SOURCE_ID) as GeoJSONSource | undefined;
    if (existing) {
      existing.setData(fc);
      return;
    }
    map.addSource(POIS_SOURCE_ID, { type: 'geojson', data: fc });
    map.addLayer({
      id: POIS_CIRCLE_LAYER_ID,
      type: 'circle',
      source: POIS_SOURCE_ID,
      paint: {
        'circle-radius': 9,
        'circle-color': '#d97706',
        'circle-stroke-width': 2,
        'circle-stroke-color': '#0f172a',
        'circle-opacity': 0.9,
      },
    });
    map.addLayer({
      id: POIS_LABEL_LAYER_ID,
      type: 'symbol',
      source: POIS_SOURCE_ID,
      layout: {
        'text-field': ['get', 'label'],
        'text-size': 10,
        'text-font': ['Noto Sans Bold'],
        'text-allow-overlap': true,
      },
      paint: { 'text-color': '#fafaf9' },
    });
  }

  private removePoisLayers(): void {
    const map = this.map!;
    if (map.getLayer(POIS_LABEL_LAYER_ID)) map.removeLayer(POIS_LABEL_LAYER_ID);
    if (map.getLayer(POIS_CIRCLE_LAYER_ID)) map.removeLayer(POIS_CIRCLE_LAYER_ID);
    if (map.getSource(POIS_SOURCE_ID)) map.removeSource(POIS_SOURCE_ID);
  }

  private removeSingleTrackLayer(): void {
    const map = this.map!;
    if (map.getLayer(TRACK_LAYER_ID)) map.removeLayer(TRACK_LAYER_ID);
    if (map.getSource(TRACK_SOURCE_ID)) map.removeSource(TRACK_SOURCE_ID);
  }

  private removeStageLayers(): void {
    const map = this.map!;
    if (map.getLayer(STAGE_ENDS_LABEL_LAYER_ID)) map.removeLayer(STAGE_ENDS_LABEL_LAYER_ID);
    if (map.getLayer(STAGE_ENDS_CIRCLE_LAYER_ID)) map.removeLayer(STAGE_ENDS_CIRCLE_LAYER_ID);
    if (map.getSource(STAGE_ENDS_SOURCE_ID)) map.removeSource(STAGE_ENDS_SOURCE_ID);
    if (map.getLayer(STAGES_LAYER_ID)) map.removeLayer(STAGES_LAYER_ID);
    if (map.getSource(STAGES_SOURCE_ID)) map.removeSource(STAGES_SOURCE_ID);
  }

  private renderMarker(
    marker: Marker | undefined,
    point: LatLon | null,
    color: string,
  ): Marker | undefined {
    if (!point) {
      marker?.remove();
      return undefined;
    }
    if (marker) {
      marker.setLngLat([point.lon, point.lat]);
      return marker;
    }
    return new Marker({ color }).setLngLat([point.lon, point.lat]).addTo(this.map!);
  }
}
