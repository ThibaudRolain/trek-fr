import type { Feature, LineString } from 'geojson';

export type TrackProfile = 'foot' | 'mtb' | 'road';

export type TrackMode = 'roundTrip' | 'aToB';

export interface TrackStats {
  distanceMeters: number;
  elevationGainMeters: number;
  elevationLossMeters: number;
  estimatedDurationSeconds: number;
}

export interface TrackResponse {
  name: string | null;
  profile: TrackProfile;
  stats: TrackStats;
  geojson: Feature<LineString>;
  bbox: [number, number, number, number] | null;
}

export interface LatLon {
  lat: number;
  lon: number;
}

export interface GenerateTrackRequest {
  latitude: number;
  longitude: number;
  distanceKm: number;
  profile: TrackProfile;
  seed?: number;
  mode: TrackMode;
  endLatitude?: number;
  endLongitude?: number;
}
