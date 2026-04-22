import { Component, inject, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { SavedTracksService, SavedTrack } from './saved-tracks.service';
import type { TrackResponse } from './track.models';

@Component({
  selector: 'app-saved-tracks-panel',
  standalone: true,
  imports: [DatePipe],
  template: `
    @if (service.tracks().length > 0) {
      <div class="rounded-lg border border-slate-800 bg-slate-950/70 p-4 text-sm text-slate-200 backdrop-blur">
        <div class="mb-2 flex items-baseline justify-between">
          <h2 class="font-medium text-slate-100">Mes traces sauvées</h2>
          <span class="text-xs text-slate-500">{{ service.tracks().length }}</span>
        </div>
        <ul class="flex flex-col gap-1 text-xs">
          @for (t of service.tracks(); track t.id) {
            <li class="flex items-center justify-between rounded border border-slate-800 bg-slate-900/60 px-2 py-1">
              <button
                type="button"
                (click)="load(t)"
                class="flex-1 truncate text-left text-slate-100 hover:text-emerald-400"
                [title]="t.name + ' — ' + (t.savedAt | date: 'dd/MM/yyyy HH:mm')"
              >
                {{ t.name }}
                <span class="text-slate-500">· {{ distanceKm(t) }} km</span>
              </button>
              <button
                type="button"
                (click)="remove(t)"
                class="ml-2 text-slate-500 hover:text-rose-400"
                [attr.aria-label]="'Supprimer ' + t.name"
              >
                ×
              </button>
            </li>
          }
        </ul>
      </div>
    }
  `,
})
export class SavedTracksPanelComponent {
  readonly service = inject(SavedTracksService);
  readonly loadTrack = output<TrackResponse>();

  load(saved: SavedTrack): void {
    this.loadTrack.emit(saved.track);
  }

  remove(saved: SavedTrack): void {
    this.service.remove(saved.id);
  }

  distanceKm(saved: SavedTrack): string {
    return (saved.track.stats.distanceMeters / 1000).toFixed(1);
  }
}
