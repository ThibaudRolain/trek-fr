import { Component, inject, signal } from '@angular/core';
import { MapComponent } from './features/map/map.component';
import { TrackGenerateComponent } from './features/tracks/track-generate.component';
import { TrackStatsPanelComponent } from './features/tracks/track-stats-panel.component';
import { TrackWeatherPanelComponent } from './features/tracks/track-weather-panel.component';
import { TrackService } from './features/tracks/track.service';
import type { LatLon, PointWeather, TrackMode, TrackResponse, WeatherPointInput } from './features/tracks/track.models';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [MapComponent, TrackGenerateComponent, TrackStatsPanelComponent, TrackWeatherPanelComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  private readonly service = inject(TrackService);

  readonly track = signal<TrackResponse | null>(null);
  readonly startPoint = signal<LatLon | null>(null);
  readonly endPoint = signal<LatLon | null>(null);
  readonly mode = signal<TrackMode>('roundTrip');
  readonly weather = signal<PointWeather[] | null>(null);

  onMapClick(point: LatLon): void {
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
    this.track.set(track);
    this.weather.set(null);
    this.fetchWeather(track);
  }

  private fetchWeather(track: TrackResponse): void {
    const points = this.pointsForWeather(track);
    if (points.length === 0) return;
    this.service.getWeather({ points, days: 7 }).subscribe({
      next: (res) => this.weather.set(res),
      error: () => this.weather.set(null), // silent — la météo est un bonus
    });
  }

  private pointsForWeather(track: TrackResponse): WeatherPointInput[] {
    const coords = track.geojson.geometry.coordinates;
    if (coords.length === 0) return [];
    const first = coords[0];
    const last = coords[coords.length - 1];
    const start: WeatherPointInput = { label: 'Départ', latitude: first[1], longitude: first[0] };
    if (this.mode() === 'roundTrip') return [start];
    return [start, { label: 'Arrivée', latitude: last[1], longitude: last[0] }];
  }
}
