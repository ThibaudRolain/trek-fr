import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import type {
  GenerateTrackRequest,
  TrackProfile,
  TrackResponse,
} from './track.models';

const API_BASE = 'http://localhost:5179';

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
      splitStages: request.splitStages ?? false,
      stageDistanceKm: request.stageDistanceKm ?? null,
      stageElevationGain: request.stageElevationGain ?? null,
    });
  }
}
