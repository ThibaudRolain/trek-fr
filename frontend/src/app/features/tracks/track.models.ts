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

export interface TrackResponse {
  name: string | null;
  profile: TrackProfile;
  stats: TrackStats;
  geojson: Feature<LineString>;
  bbox: [number, number, number, number] | null;
  proposedDestinationName: string | null;
  stages: StageDto[] | null;
  warnings: string[] | null;
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
}
