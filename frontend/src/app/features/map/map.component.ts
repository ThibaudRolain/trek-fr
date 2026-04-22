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
import type { LatLon, TrackResponse } from '../tracks/track.models';

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
  readonly mapClick = output<LatLon>();

  private map?: MapLibreMap;
  private startMarker?: Marker;
  private endMarker?: Marker;
  private readonly ready = signal(false);

  // When the backend proposes an arrival (propose-destination flow) the trace's last
  // coord is the arrival city — pin it automatically even if the user hasn't clicked one.
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
    const map = this.map!;
    const existing = map.getSource(TRACK_SOURCE_ID) as GeoJSONSource | undefined;

    if (!track) {
      if (map.getLayer(TRACK_LAYER_ID)) map.removeLayer(TRACK_LAYER_ID);
      if (existing) map.removeSource(TRACK_SOURCE_ID);
      return;
    }

    if (existing) {
      existing.setData(track.geojson);
    } else {
      map.addSource(TRACK_SOURCE_ID, { type: 'geojson', data: track.geojson });
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

    if (track.bbox) {
      const [minLon, minLat, maxLon, maxLat] = track.bbox;
      const bounds: LngLatBoundsLike = [
        [minLon, minLat],
        [maxLon, maxLat],
      ];
      map.fitBounds(bounds, { padding: 60, duration: 600 });
    }
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
