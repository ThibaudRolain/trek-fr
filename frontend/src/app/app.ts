import { Component, signal } from '@angular/core';
import { MapComponent } from './features/map/map.component';
import { TrackGenerateComponent } from './features/tracks/track-generate.component';
import { TrackStatsPanelComponent } from './features/tracks/track-stats-panel.component';
import type { LatLon, TrackMode, TrackResponse } from './features/tracks/track.models';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [MapComponent, TrackGenerateComponent, TrackStatsPanelComponent],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  readonly track = signal<TrackResponse | null>(null);
  readonly startPoint = signal<LatLon | null>(null);
  readonly endPoint = signal<LatLon | null>(null);
  readonly mode = signal<TrackMode>('roundTrip');

  onMapClick(point: LatLon): void {
    if (this.mode() === 'roundTrip') {
      this.startPoint.set(point);
      this.endPoint.set(null);
      return;
    }
    // A→B: cycle start -> end -> reset+start
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
  }
}
