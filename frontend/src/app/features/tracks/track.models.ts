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
  proposedDestinationName: string | null;
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

export interface WeatherPointInput {
  label: string;
  latitude: number;
  longitude: number;
}

export interface WeatherRequest {
  points: WeatherPointInput[];
  startDate?: string; // ISO yyyy-MM-dd
  days?: number;
}

export interface WeatherDay {
  date: string; // yyyy-MM-dd
  tempMinC: number;
  tempMaxC: number;
  precipitationMm: number;
  windKmh: number;
  wmoCode: number;
  summary: string;
}

export interface PointWeather {
  label: string;
  communeName: string | null;
  latitude: number;
  longitude: number;
  forecast: WeatherDay[];
}
