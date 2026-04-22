import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type {
  GenerateTrackRequest,
  PointWeather,
  TrackProfile,
  TrackResponse,
  WeatherRequest,
} from './track.models';

const API_BASE = '';

@Injectable({ providedIn: 'root' })
export class TrackService {
  private readonly http = inject(HttpClient);

  importGpx(file: File, profile: TrackProfile = 'foot'): Observable<TrackResponse> {
    const form = new FormData();
    form.append('gpx', file, file.name);
    const params = { profile };
    return this.http.post<TrackResponse>(`${API_BASE}/tracks/import`, form, { params });
  }

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
