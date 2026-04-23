import { Component, computed, input } from '@angular/core';
import type { DestinationInfo, TrackResponse } from './track.models';

@Component({
  selector: 'app-arrival-city-panel',
  standalone: true,
  template: `
    @if (info(); as info) {
      <div class="rounded-lg border border-slate-800 bg-slate-950/70 p-3 text-sm text-slate-200 backdrop-blur">
        <div class="mb-2 flex items-center justify-between">
          <h3 class="text-xs font-medium uppercase tracking-wide text-slate-400">Ville d'arrivée</h3>
          <a
            [href]="wikipediaUrl(info.name)"
            target="_blank"
            rel="noopener"
            class="text-xs text-sky-400 hover:text-sky-300"
          >Wikipedia →</a>
        </div>
        <p class="mb-2 font-medium text-slate-100">{{ info.name }}</p>
        <div class="flex flex-wrap gap-1.5">
          @if (info.isPlusBeauVillage) {
            <span class="rounded bg-amber-900/60 px-2 py-0.5 text-[11px] font-medium text-amber-200 ring-1 ring-amber-700/50">
              Plus Beau Village ✦
            </span>
          }
          @if (info.isVilleArtHistoire) {
            <span class="rounded bg-sky-900/60 px-2 py-0.5 text-[11px] font-medium text-sky-200 ring-1 ring-sky-700/50">
              Ville d'art et d'histoire
            </span>
          }
          @if (info.monumentsHistoriques != null && info.monumentsHistoriques > 0) {
            <span class="rounded bg-slate-800 px-2 py-0.5 text-[11px] text-slate-300 ring-1 ring-slate-700">
              {{ info.monumentsHistoriques }} MH
            </span>
          }
          @if (!info.isPlusBeauVillage && !info.isVilleArtHistoire && (info.monumentsHistoriques == null || info.monumentsHistoriques === 0)) {
            <span class="text-[11px] text-slate-500">Pas de label patrimonial recensé</span>
          }
        </div>
      </div>
    }
  `,
})
export class ArrivalCityPanelComponent {
  readonly track = input<TrackResponse | null>(null);

  readonly info = computed<DestinationInfo | null>(() => this.track()?.destinationInfo ?? null);

  wikipediaUrl(name: string): string {
    return `https://fr.wikipedia.org/wiki/${encodeURIComponent(name)}`;
  }
}
