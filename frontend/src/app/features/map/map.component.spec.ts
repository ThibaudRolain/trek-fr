import { vi } from 'vitest';

// Hoisted : les classes fakes doivent être construites dans la factory de vi.mock,
// qui est remontée au top du module. On partage les tableaux d'instances pour pouvoir
// les inspecter depuis les tests.
const { FakeMap, FakeMarker, mapInstances, markerInstances } = vi.hoisted(() => {
  const mapInstances: FakeMapT[] = [];
  const markerInstances: FakeMarkerT[] = [];

  type FakeMapT = InstanceType<typeof FakeMap>;
  type FakeMarkerT = InstanceType<typeof FakeMarker>;

  class FakeMap {
    opts: any;
    listeners = new Map<string, Array<(e: any) => void>>();
    sources = new Map<string, { data: any; setData: (d: any) => void }>();
    layers = new Set<string>();
    fitBoundsCalls: Array<{ bounds: any; options: any }> = [];
    removed = false;
    resizeCount = 0;
    canvas = { style: { cursor: '' } } as HTMLCanvasElement;

    constructor(opts: any) {
      this.opts = opts;
      mapInstances.push(this);
    }
    on(event: string, handler: (e: any) => void) {
      if (!this.listeners.has(event)) this.listeners.set(event, []);
      this.listeners.get(event)!.push(handler);
      return this;
    }
    fireLoad() { this.listeners.get('load')?.forEach((h) => h({})); }
    fireClick(lngLat: { lat: number; lng: number }) {
      this.listeners.get('click')?.forEach((h) => h({ lngLat }));
    }
    getSource(id: string) { return this.sources.get(id); }
    addSource(id: string, cfg: { data: any }) {
      const src = {
        data: cfg.data,
        setData(d: any) { this.data = d; },
      };
      this.sources.set(id, src);
    }
    removeSource(id: string) { this.sources.delete(id); }
    getLayer(id: string) { return this.layers.has(id) ? { id } : undefined; }
    addLayer(layer: { id: string }) { this.layers.add(layer.id); }
    removeLayer(id: string) { this.layers.delete(id); }
    fitBounds(bounds: any, options: any) { this.fitBoundsCalls.push({ bounds, options }); }
    resize() { this.resizeCount++; }
    remove() { this.removed = true; }
    getCanvas() { return this.canvas; }
  }

  class FakeMarker {
    opts: any;
    lngLat?: [number, number];
    map?: any;
    removed = false;
    constructor(opts: any) {
      this.opts = opts;
      markerInstances.push(this);
    }
    setLngLat(pos: [number, number]) { this.lngLat = pos; return this; }
    addTo(map: any) { this.map = map; return this; }
    remove() { this.removed = true; }
  }

  return { FakeMap, FakeMarker, mapInstances, markerInstances };
});

vi.mock('maplibre-gl', () => ({
  Map: FakeMap,
  Marker: FakeMarker,
}));

import { TestBed } from '@angular/core/testing';
import { MapComponent } from './map.component';
import type { TrackResponse } from '../tracks/track.models';

function makeTrack(overrides: Partial<TrackResponse> = {}): TrackResponse {
  return {
    name: null,
    profile: 'foot',
    stats: { distanceMeters: 0, elevationGainMeters: 0, elevationLossMeters: 0, estimatedDurationSeconds: 0 },
    geojson: {
      type: 'Feature',
      geometry: { type: 'LineString', coordinates: [[2.35, 48.85], [2.36, 48.86]] },
      properties: {},
    },
    bbox: [2.35, 48.85, 2.36, 48.86],
    proposedDestinationName: null,
    ...overrides,
  };
}

function setup() {
  TestBed.configureTestingModule({ imports: [MapComponent] });
  const fixture = TestBed.createComponent(MapComponent);
  fixture.detectChanges(); // lance ngAfterViewInit → Map créée
  const map = mapInstances[mapInstances.length - 1];
  map.fireLoad(); // ready → effets track autorisés
  fixture.detectChanges();
  return { fixture, map };
}

describe('MapComponent', () => {
  beforeEach(() => {
    mapInstances.length = 0;
    markerInstances.length = 0;
  });

  it('creates a MapLibre map centered on France with crosshair cursor', () => {
    setup();
    expect(mapInstances).toHaveLength(1);
    expect(mapInstances[0].opts.center).toEqual([2.5, 46.5]);
    expect(mapInstances[0].opts.zoom).toBe(5.5);
    expect(mapInstances[0].canvas.style.cursor).toBe('crosshair');
  });

  it('emits mapClick as LatLon when the user clicks the map', () => {
    const { fixture, map } = setup();
    const received: Array<{ lat: number; lon: number }> = [];
    fixture.componentRef.instance.mapClick.subscribe((p) => received.push(p));

    map.fireClick({ lat: 48.85, lng: 2.35 });

    expect(received).toEqual([{ lat: 48.85, lon: 2.35 }]);
  });

  it('adds track source + layer and fits bounds on first track', () => {
    const { fixture, map } = setup();
    fixture.componentRef.setInput('track', makeTrack());
    fixture.detectChanges();

    expect(map.getSource('track')).toBeDefined();
    expect(map.getLayer('track-line')).toBeDefined();
    expect(map.fitBoundsCalls).toHaveLength(1);
    expect(map.fitBoundsCalls[0].bounds).toEqual([[2.35, 48.85], [2.36, 48.86]]);
  });

  it('updates existing track source on subsequent tracks (no duplicate layer)', () => {
    const { fixture, map } = setup();
    fixture.componentRef.setInput('track', makeTrack());
    fixture.detectChanges();

    const updated = makeTrack({
      geojson: {
        type: 'Feature',
        geometry: { type: 'LineString', coordinates: [[3.0, 45.0]] },
        properties: {},
      },
      bbox: [3.0, 45.0, 3.0, 45.0],
    });
    fixture.componentRef.setInput('track', updated);
    fixture.detectChanges();

    expect(map.sources.size).toBe(1);
    expect(map.layers.size).toBe(1);
    expect(map.getSource('track')!.data).toBe(updated.geojson);
    expect(map.fitBoundsCalls).toHaveLength(2);
  });

  it('removes track layer + source when track is cleared', () => {
    const { fixture, map } = setup();
    fixture.componentRef.setInput('track', makeTrack());
    fixture.detectChanges();
    expect(map.layers.has('track-line')).toBe(true);

    fixture.componentRef.setInput('track', null);
    fixture.detectChanges();

    expect(map.layers.has('track-line')).toBe(false);
    expect(map.sources.has('track')).toBe(false);
  });

  it('creates an orange start marker when startPoint is set, updates it on change, removes it when cleared', () => {
    const { fixture } = setup();
    fixture.componentRef.setInput('startPoint', { lat: 48.85, lon: 2.35 });
    fixture.detectChanges();

    expect(markerInstances).toHaveLength(1);
    expect(markerInstances[0].opts.color).toBe('#f59e0b');
    expect(markerInstances[0].lngLat).toEqual([2.35, 48.85]);

    fixture.componentRef.setInput('startPoint', { lat: 49.0, lon: 2.5 });
    fixture.detectChanges();
    expect(markerInstances).toHaveLength(1); // même instance réutilisée
    expect(markerInstances[0].lngLat).toEqual([2.5, 49.0]);

    fixture.componentRef.setInput('startPoint', null);
    fixture.detectChanges();
    expect(markerInstances[0].removed).toBe(true);
  });

  it('renders a red end marker from proposedDestinationName when endPoint is not set', () => {
    const { fixture } = setup();
    fixture.componentRef.setInput(
      'track',
      makeTrack({
        geojson: {
          type: 'Feature',
          geometry: { type: 'LineString', coordinates: [[2.35, 48.85], [1.21, 44.89]] },
          properties: {},
        },
        bbox: [1.21, 44.89, 2.35, 48.85],
        proposedDestinationName: 'Sarlat-la-Canéda',
      }),
    );
    fixture.detectChanges();

    const red = markerInstances.find((m) => m.opts.color === '#ef4444');
    expect(red).toBeDefined();
    expect(red!.lngLat).toEqual([1.21, 44.89]); // dernière coord
  });

  it('explicit endPoint takes precedence over proposedDestinationName', () => {
    const { fixture } = setup();
    fixture.componentRef.setInput('endPoint', { lat: 50.0, lon: 3.0 });
    fixture.componentRef.setInput(
      'track',
      makeTrack({
        geojson: {
          type: 'Feature',
          geometry: { type: 'LineString', coordinates: [[2.35, 48.85], [1.21, 44.89]] },
          properties: {},
        },
        bbox: [1.21, 44.89, 2.35, 48.85],
        proposedDestinationName: 'Sarlat-la-Canéda',
      }),
    );
    fixture.detectChanges();

    const red = markerInstances.find((m) => m.opts.color === '#ef4444');
    expect(red!.lngLat).toEqual([3.0, 50.0]); // explicit, pas la dernière coord
  });

  it('cleans up map and markers on destroy', () => {
    const { fixture, map } = setup();
    fixture.componentRef.setInput('startPoint', { lat: 48.85, lon: 2.35 });
    fixture.detectChanges();

    fixture.destroy();

    expect(map.removed).toBe(true);
    expect(markerInstances[0].removed).toBe(true);
  });
});
