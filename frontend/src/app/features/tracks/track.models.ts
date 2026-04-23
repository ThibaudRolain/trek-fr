import type { Feature, LineString } from 'geojson';

export type TrackProfile = 'foot' | 'mtb' | 'road';

export type TrackMode = 'roundTrip' | 'aToB';

export type SleepSpotKind = 'refuge' | 'town' | 'arrival';

export interface TrackStats {
  distanceMeters: number;
  elevationGainMeters: number;
  elevationLossMeters: number;
  estimatedDurationSeconds: number;
}

export interface SleepSpotDto {
  name: string;
  latitude: number;
  longitude: number;
  kind: SleepSpotKind;
}

export interface StageDto {
  index: number;
  stats: TrackStats;
  geojson: Feature<LineString>;
  bbox: [number, number, number, number] | null;
  endSleepSpot: SleepSpotDto;
  offTrackDistanceMeters: number | null;
}

export interface WarningDto {
  message: string;
  nearbyPlace: string | null;
  nearbyPlaceDistanceMeters: number | null;
}

export interface DestinationInfo {
  name: string;
  monumentsHistoriques: number | null;
  isPlusBeauVillage: boolean;
  isVilleArtHistoire: boolean;
}

export interface PoiOnRoute {
  communeName: string;
  monumentCount: number;
  latitude: number;
  longitude: number;
  distanceFromStartMeters: number;
  distanceFromTrackMeters: number;
}

export interface TrackVariantDto {
  geojson: Feature<LineString>;
  bbox: [number, number, number, number] | null;
  stats: TrackStats;
  seed: number | null;
}

export interface TrackResponse {
  name: string | null;
  profile: TrackProfile;
  stats: TrackStats;
  geojson: Feature<LineString>;
  bbox: [number, number, number, number] | null;
  proposedDestinationName: string | null;
  destinationInfo: DestinationInfo | null;
  stages: StageDto[] | null;
  warnings: WarningDto[] | null;
  seed: number | null;
  poisOnRoute: PoiOnRoute[] | null;
  variants: TrackVariantDto[] | null;
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
  splitStages?: boolean;
  stageDistanceKm?: number;
  stageElevationGain?: number;
  minElevationGainMeters?: number;
  maxElevationGainMeters?: number;
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
