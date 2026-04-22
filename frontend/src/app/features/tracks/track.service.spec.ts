import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { TrackService } from './track.service';
import type { TrackResponse } from './track.models';

const stubTrack: TrackResponse = {
  name: null,
  profile: 'foot',
  stats: { distanceMeters: 0, elevationGainMeters: 0, elevationLossMeters: 0, estimatedDurationSeconds: 0 },
  geojson: { type: 'Feature', geometry: { type: 'LineString', coordinates: [] }, properties: {} },
  bbox: null,
  proposedDestinationName: null,
};

describe('TrackService', () => {
  let service: TrackService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    service = TestBed.inject(TrackService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('generate posts to /tracks/generate with the expected body', () => {
    service
      .generate({
        latitude: 48.85,
        longitude: 2.35,
        distanceKm: 10,
        profile: 'foot',
        seed: 42,
        mode: 'roundTrip',
      })
      .subscribe();

    const req = http.expectOne('/tracks/generate');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      latitude: 48.85,
      longitude: 2.35,
      distanceKm: 10,
      profile: 'foot',
      seed: 42,
      mode: 'roundTrip',
      endLatitude: null,
      endLongitude: null,
    });
    req.flush(stubTrack);
  });

  it('generate sends explicit nulls for missing optional fields', () => {
    service
      .generate({
        latitude: 48.85,
        longitude: 2.35,
        distanceKm: 10,
        profile: 'foot',
        mode: 'roundTrip',
      })
      .subscribe();

    const req = http.expectOne('/tracks/generate');
    expect(req.request.body.seed).toBeNull();
    expect(req.request.body.endLatitude).toBeNull();
    expect(req.request.body.endLongitude).toBeNull();
    req.flush(stubTrack);
  });

  it('generate forwards explicit end coordinates for aToB mode', () => {
    service
      .generate({
        latitude: 48.85,
        longitude: 2.35,
        distanceKm: 50,
        profile: 'mtb',
        mode: 'aToB',
        endLatitude: 49.0,
        endLongitude: 2.5,
      })
      .subscribe();

    const req = http.expectOne('/tracks/generate');
    expect(req.request.body.mode).toBe('aToB');
    expect(req.request.body.endLatitude).toBe(49.0);
    expect(req.request.body.endLongitude).toBe(2.5);
    req.flush(stubTrack);
  });

  it('getWeather posts points with defaulted startDate null and days 7', () => {
    service
      .getWeather({
        points: [{ label: 'A', latitude: 48.85, longitude: 2.35 }],
      })
      .subscribe();

    const req = http.expectOne('/tracks/weather');
    expect(req.request.body).toEqual({
      points: [{ label: 'A', latitude: 48.85, longitude: 2.35 }],
      startDate: null,
      days: 7,
    });
    req.flush([]);
  });

  it('getWeather forwards explicit startDate and days', () => {
    service
      .getWeather({
        points: [{ label: 'A', latitude: 48.85, longitude: 2.35 }],
        startDate: '2026-06-01',
        days: 3,
      })
      .subscribe();

    const req = http.expectOne('/tracks/weather');
    expect(req.request.body.startDate).toBe('2026-06-01');
    expect(req.request.body.days).toBe(3);
    req.flush([]);
  });
});
