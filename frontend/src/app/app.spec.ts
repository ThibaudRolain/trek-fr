import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { App } from './app';
import type { LatLon, PointWeather, TrackResponse } from './features/tracks/track.models';

function makeTrack(coordinates: [number, number][]): TrackResponse {
  return {
    name: null,
    profile: 'foot',
    stats: { distanceMeters: 0, elevationGainMeters: 0, elevationLossMeters: 0, estimatedDurationSeconds: 0 },
    geojson: { type: 'Feature', geometry: { type: 'LineString', coordinates }, properties: {} },
    bbox: null,
    proposedDestinationName: null,
    stages: null,
    warnings: null,
    seed: null,
  };
}

describe('App', () => {
  let http: HttpTestingController;
  let app: App;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    // Stub le template pour ne pas instancier MapComponent (MapLibre) dans le test runner.
    TestBed.overrideComponent(App, { set: { template: '', imports: [] } });
    app = TestBed.createComponent(App).componentInstance;
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  // ---- onMapClick ----

  it('onMapClick in roundTrip mode sets startPoint and clears endPoint', () => {
    app.mode.set('aToB');
    app.startPoint.set({ lat: 1, lon: 1 });
    app.endPoint.set({ lat: 2, lon: 2 });
    app.mode.set('roundTrip');

    app.onMapClick({ lat: 48.85, lon: 2.35 });

    expect(app.startPoint()).toEqual({ lat: 48.85, lon: 2.35 });
    expect(app.endPoint()).toBeNull();
  });

  it('onMapClick in aToB sets startPoint on first click', () => {
    app.mode.set('aToB');

    app.onMapClick({ lat: 48.85, lon: 2.35 });

    expect(app.startPoint()).toEqual({ lat: 48.85, lon: 2.35 });
    expect(app.endPoint()).toBeNull();
  });

  it('onMapClick in aToB sets endPoint on second click', () => {
    app.mode.set('aToB');
    app.startPoint.set({ lat: 48.85, lon: 2.35 });

    app.onMapClick({ lat: 49.0, lon: 2.5 });

    expect(app.startPoint()).toEqual({ lat: 48.85, lon: 2.35 });
    expect(app.endPoint()).toEqual({ lat: 49.0, lon: 2.5 });
  });

  it('onMapClick in aToB resets startPoint when both points already set', () => {
    app.mode.set('aToB');
    app.startPoint.set({ lat: 48.85, lon: 2.35 });
    app.endPoint.set({ lat: 49.0, lon: 2.5 });

    app.onMapClick({ lat: 44.0, lon: 1.0 });

    expect(app.startPoint()).toEqual({ lat: 44.0, lon: 1.0 });
    expect(app.endPoint()).toBeNull();
  });

  // ---- onModeChange ----

  it('onModeChange to roundTrip clears endPoint', () => {
    app.mode.set('aToB');
    app.startPoint.set({ lat: 48.85, lon: 2.35 });
    app.endPoint.set({ lat: 49.0, lon: 2.5 });

    app.onModeChange('roundTrip');

    expect(app.mode()).toBe('roundTrip');
    expect(app.startPoint()).toEqual({ lat: 48.85, lon: 2.35 });
    expect(app.endPoint()).toBeNull();
  });

  it('onModeChange to aToB keeps existing points', () => {
    app.startPoint.set({ lat: 48.85, lon: 2.35 });

    app.onModeChange('aToB');

    expect(app.mode()).toBe('aToB');
    expect(app.startPoint()).toEqual({ lat: 48.85, lon: 2.35 });
  });

  // ---- onTrackReady / fetchWeather ----

  it('onTrackReady stores track, clears weather, and fetches weather for a single point in roundTrip', () => {
    app.mode.set('roundTrip');
    const track = makeTrack([[2.35, 48.85], [2.36, 48.86]]);

    app.onTrackReady(track);

    expect(app.track()).toBe(track);
    expect(app.weather()).toBeNull();

    const req = http.expectOne('/tracks/weather');
    expect(req.request.body.points).toEqual([
      { label: 'Départ', latitude: 48.85, longitude: 2.35 },
    ]);
    expect(req.request.body.days).toBe(7);

    const forecast: PointWeather[] = [
      { label: 'Départ', communeName: null, latitude: 48.85, longitude: 2.35, forecast: [] },
    ];
    req.flush(forecast);
    expect(app.weather()).toEqual(forecast);
  });

  it('onTrackReady in aToB fetches weather for start and end', () => {
    app.mode.set('aToB');
    const track = makeTrack([[2.35, 48.85], [2.50, 49.00]]);

    app.onTrackReady(track);

    const req = http.expectOne('/tracks/weather');
    expect(req.request.body.points).toEqual([
      { label: 'Départ', latitude: 48.85, longitude: 2.35 },
      { label: 'Arrivée', latitude: 49.0, longitude: 2.5 },
    ]);
    req.flush([]);
  });

  it('onTrackReady does not call the weather endpoint when the track has no coordinates', () => {
    app.onTrackReady(makeTrack([]));

    http.expectNone('/tracks/weather');
    expect(app.weather()).toBeNull();
  });

  it('onTrackReady silently ignores weather errors (weather stays null)', () => {
    app.mode.set('roundTrip');
    app.onTrackReady(makeTrack([[2.35, 48.85]]));

    const req = http.expectOne('/tracks/weather');
    req.flush('boom', { status: 502, statusText: 'Bad Gateway' });

    expect(app.weather()).toBeNull();
    expect(app.track()).not.toBeNull(); // la trace reste affichée
  });
});
