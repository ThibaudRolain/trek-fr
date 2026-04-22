import { Component, inject, signal } from '@angular/core';
import { MapComponent } from './features/map/map.component';
import { TrackGenerateComponent } from './features/tracks/track-generate.component';
import { TrackStatsPanelComponent } from './features/tracks/track-stats-panel.component';
import { TrackWeatherPanelComponent } from './features/tracks/track-weather-panel.component';
import { TrackService } from './features/tracks/track.service';
import type {
  LatLon,
  PointWeather,
  TrackMode,
  TrackResponse,
  WeatherPointInput,
} from './features/tracks/track.models';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [
    MapComponent,
    TrackGenerateComponent,
    TrackStatsPanelComponent,
    TrackWeatherPanelComponent,
  ],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly service = inject(TrackService);

  readonly track = signal<TrackResponse | null>(null);
  readonly startPoint = signal<LatLon | null>(null);
  readonly endPoint = signal<LatLon | null>(null);
  readonly mode = signal<TrackMode>('roundTrip');
  readonly focusBbox = signal<[number, number, number, number] | null>(null);
  readonly weather = signal<PointWeather[] | null>(null);

  onMapClick(point: LatLon): void {
    this.focusBbox.set(null);
    if (this.mode() === 'roundTrip') {
      this.startPoint.set(point);
      this.endPoint.set(null);
      return;
    }
    if (this.startPoint() === null || this.endPoint() !== null) {
      this.startPoint.set(point);
      this.endPoint.set(null);
    } else {
      this.endPoint.set(point);
    }
  }

  onModeChange(mode: TrackMode): void {
    this.mode.set(mode);
    if (mode === 'roundTrip') {
      this.endPoint.set(null);
    }
  }

  onTrackReady(track: TrackResponse): void {
    this.focusBbox.set(null);
    this.track.set(track);
    this.weather.set(null);
    this.fetchWeather(track);
  }

  onStageFocus(bbox: [number, number, number, number]): void {
    this.focusBbox.set(bbox);
  }

  private fetchWeather(track: TrackResponse): void {
    const points = this.pointsForWeather(track);
    if (points.length === 0) return;
    this.service.getWeather({ points, days: 7 }).subscribe({
      next: (res) => this.weather.set(res),
      error: () => this.weather.set(null), // la météo est un bonus
    });
  }

  private pointsForWeather(track: TrackResponse): WeatherPointInput[] {
    const coords = track.geojson.geometry.coordinates;
    if (coords.length === 0) return [];

    // Multi-stage : on interroge la météo sur chaque sleep spot (intermédiaires + arrivée),
    // en repartant du départ de la trace pour que le panneau météo commence à J0.
    // Open-Meteo endpoint accepte ≤ 10 points → on tronque si le trek fait plus d'étapes.
    if (track.stages && track.stages.length > 0) {
      const first = coords[0];
      const points: WeatherPointInput[] = [
        { label: 'Départ', latitude: first[1], longitude: first[0] },
      ];
      for (const s of track.stages) {
        const label = s.endSleepSpot.kind === 'arrival'
          ? `J${s.index} · ${s.endSleepSpot.name}`
          : `J${s.index} · ${s.endSleepSpot.name}`;
        points.push({
          label,
          latitude: s.endSleepSpot.latitude,
          longitude: s.endSleepSpot.longitude,
        });
      }
      return points.slice(0, 10);
    }

    // Legacy : départ (+ arrivée si A→B).
    const first = coords[0];
    const last = coords[coords.length - 1];
    const start: WeatherPointInput = { label: 'Départ', latitude: first[1], longitude: first[0] };
    if (this.mode() === 'roundTrip') return [start];
    return [start, { label: 'Arrivée', latitude: last[1], longitude: last[0] }];
  }
}
