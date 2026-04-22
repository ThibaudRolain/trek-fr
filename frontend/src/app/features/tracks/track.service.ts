import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import type {
  GenerateTrackRequest,
  PointWeather,
  TrackResponse,
  WeatherRequest,
} from './track.models';

const API_BASE = environment.apiBase;

@Injectable({ providedIn: 'root' })
export class TrackService {
  private readonly http = inject(HttpClient);

  generate(request: GenerateTrackRequest): Observable<TrackResponse> {
    return this.http.post<TrackResponse>(`${API_BASE}/tracks/generate`, {
      latitude: request.latitude,
      longitude: request.longitude,
      distanceKm: request.distanceKm,
      profile: request.profile,
      seed: request.seed ?? null,
      mode: request.mode,
      endLatitude: request.endLatitude ?? null,
      endLongitude: request.endLongitude ?? null,
      splitStages: request.splitStages ?? false,
      stageDistanceKm: request.stageDistanceKm ?? null,
      stageElevationGain: request.stageElevationGain ?? null,
      minElevationGainMeters: request.minElevationGainMeters ?? null,
      maxElevationGainMeters: request.maxElevationGainMeters ?? null,
    });
  }

  getWeather(request: WeatherRequest): Observable<PointWeather[]> {
    return this.http.post<PointWeather[]>(`${API_BASE}/tracks/weather`, {
      points: request.points,
      startDate: request.startDate ?? null,
      days: request.days ?? 7,
    });
  }
}
