import { Component, input, output } from '@angular/core';
import type { StageDto, TrackStats } from './track.models';

@Component({
  selector: 'app-stages-panel',
  standalone: true,
  template: `
    @if (stages(); as stageList) {
      @if (stageList.length > 0) {
        <div class="flex max-h-72 flex-col rounded-lg border border-slate-700 bg-slate-950/90 p-3 shadow-xl backdrop-blur-sm">
          <p class="mb-1 shrink-0 text-xs font-medium text-slate-300">
            Étapes ({{ stageList.length }})
          </p>
          <p class="mb-2 shrink-0 text-[10px] leading-snug text-slate-500">
            Les étapes s'arrêtent dans des communes proches de la trace.
            Les liens ouvrent une recherche pour vérifier l'offre d'hébergement.
          </p>
          <ul class="flex flex-1 flex-col gap-1 overflow-y-auto pr-0.5">
            @for (s of stageList; track s.index) {
              <li class="overflow-hidden rounded border border-slate-800 bg-slate-900/60 hover:border-slate-700">
                <button
                  type="button"
                  class="flex w-full items-center gap-2 px-2 py-1.5 text-left text-xs hover:bg-slate-800/50"
                  (click)="onStageClick(s)"
                >
                  <span
                    class="flex h-5 w-5 shrink-0 items-center justify-center rounded-full text-[10px] font-bold text-slate-950"
                    [style.background-color]="stageColor(s.index)"
                  >
                    {{ s.index }}
                  </span>
                  <span class="flex-1 truncate text-slate-100">
                    {{ s.endSleepSpot.name }}
                    @if (s.endSleepSpot.kind === 'arrival') {
                      <span class="ml-1 text-[10px] uppercase tracking-wide text-slate-500">arrivée</span>
                    } @else if (s.endSleepSpot.kind === 'refuge') {
                      <span class="ml-1 text-[10px] uppercase tracking-wide text-slate-500">refuge</span>
                    }
                  </span>
                  <span class="font-mono text-slate-300">{{ formatKm(s.stats) }} km</span>
                  <span class="font-mono text-emerald-300">+{{ formatGain(s.stats) }}</span>
                  <span class="font-mono text-slate-400">{{ formatDuration(s.stats) }}</span>
                </button>
                @if (hasLodgingLinks(s)) {
                  <div class="flex items-center gap-2 border-t border-slate-800/60 px-2 py-1 text-[10px] text-slate-500">
                    <span>Hébergement :</span>
                    <a [href]="airbnbUrl(s.endSleepSpot.name)" target="_blank" rel="noopener"
                       class="text-slate-300 hover:text-emerald-300">Airbnb</a>
                    <span class="text-slate-700">·</span>
                    <a [href]="bookingUrl(s.endSleepSpot.name)" target="_blank" rel="noopener"
                       class="text-slate-300 hover:text-emerald-300">Booking</a>
                    <span class="text-slate-700">·</span>
                    <a [href]="abritelUrl(s.endSleepSpot.name)" target="_blank" rel="noopener"
                       class="text-slate-300 hover:text-emerald-300">Abritel</a>
                  </div>
                }
              </li>
            }
          </ul>
        </div>
      }
    }
  `,
})
export class StagesPanelComponent {
  readonly stages = input<StageDto[] | null>(null);
  readonly stageFocus = output<[number, number, number, number]>();

  onStageClick(stage: StageDto): void {
    if (stage.bbox) this.stageFocus.emit(stage.bbox);
  }

  stageColor(index: number): string {
    return index % 2 === 1 ? '#10b981' : '#0ea5e9';
  }

  hasLodgingLinks(stage: StageDto): boolean {
    const spot = stage.endSleepSpot;
    if (spot.kind === 'town' || spot.kind === 'refuge') return true;
    return spot.kind === 'arrival' && spot.name !== 'Arrivée';
  }

  formatKm(stats: TrackStats): string {
    return (stats.distanceMeters / 1000).toFixed(2);
  }

  formatGain(stats: TrackStats): number {
    return Math.round(stats.elevationGainMeters);
  }

  formatDuration(stats: TrackStats): string {
    const sec = Math.round(stats.estimatedDurationSeconds);
    const h = Math.floor(sec / 3600);
    const m = Math.round((sec % 3600) / 60);
    return h > 0 ? `${h}h${m.toString().padStart(2, '0')}` : `${m} min`;
  }

  airbnbUrl(name: string): string {
    return `https://www.airbnb.fr/s/${encodeURIComponent(name)}--France/homes`;
  }

  bookingUrl(name: string): string {
    return `https://www.booking.com/searchresults.fr.html?ss=${encodeURIComponent(name + ', France')}`;
  }

  abritelUrl(name: string): string {
    return `https://www.abritel.fr/search?q=${encodeURIComponent(name + ', France')}`;
  }
}
