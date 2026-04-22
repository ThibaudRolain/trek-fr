import { Component, computed, input, output } from '@angular/core';
import type { StageDto, TrackResponse, TrackStats } from './track.models';

@Component({
  selector: 'app-track-stats-panel',
  standalone: true,
  template: `
    @if (track(); as t) {
      <div class="rounded-lg border border-slate-800 bg-slate-950/70 p-4 text-sm text-slate-200 backdrop-blur">
        <div class="mb-2 flex items-baseline justify-between">
          <h2 class="font-medium text-slate-100">{{ t.name ?? 'Trace générée' }}</h2>
          <span class="text-xs uppercase tracking-wide text-slate-400">{{ t.profile }}</span>
        </div>
        @if (t.proposedDestinationName; as dest) {
          <p class="mb-2 text-xs text-slate-400">
            Arrivée proposée :
            <span class="font-medium text-slate-100">{{ dest }}</span>
          </p>
        }
        <dl class="grid grid-cols-2 gap-x-4 gap-y-2 text-xs">
          <dt class="text-slate-400">Distance</dt>
          <dd class="text-right font-mono text-slate-100">{{ formatKm(t.stats) }} km</dd>

          <dt class="text-slate-400">Dénivelé +</dt>
          <dd class="text-right font-mono text-emerald-300">+{{ formatGain(t.stats) }} m</dd>

          <dt class="text-slate-400">Dénivelé −</dt>
          <dd class="text-right font-mono text-rose-300">−{{ formatLoss(t.stats) }} m</dd>

          <dt class="text-slate-400">Durée estimée</dt>
          <dd class="text-right font-mono text-slate-100">{{ formatDuration(t.stats) }}</dd>
        </dl>

        @if (stages(); as stages) {
          <div class="mt-3 border-t border-slate-800 pt-2">
            <p class="mb-1 text-xs font-medium text-slate-300">
              Étapes ({{ stages.length }})
            </p>
            <ul class="flex flex-col gap-1">
              @for (s of stages; track s.index) {
                <li>
                  <button
                    type="button"
                    class="flex w-full items-center gap-2 rounded border border-slate-800 bg-slate-900/60 px-2 py-1.5 text-left text-xs hover:border-slate-700 hover:bg-slate-800"
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
                </li>
              }
            </ul>
          </div>
        }
      </div>
    }
  `,
})
export class TrackStatsPanelComponent {
  readonly track = input<TrackResponse | null>(null);
  readonly stageFocus = output<[number, number, number, number]>();

  readonly stages = computed(() => this.track()?.stages ?? null);

  formatKm(stats: TrackStats): string {
    return (stats.distanceMeters / 1000).toFixed(2);
  }

  formatGain(stats: TrackStats): number {
    return Math.round(stats.elevationGainMeters);
  }

  formatLoss(stats: TrackStats): number {
    return Math.round(stats.elevationLossMeters);
  }

  formatDuration(stats: TrackStats): string {
    const sec = Math.round(stats.estimatedDurationSeconds);
    const h = Math.floor(sec / 3600);
    const m = Math.round((sec % 3600) / 60);
    return h > 0 ? `${h}h${m.toString().padStart(2, '0')}` : `${m} min`;
  }

  stageColor(index: number): string {
    return index % 2 === 1 ? '#10b981' : '#0ea5e9';
  }

  onStageClick(stage: StageDto): void {
    if (stage.bbox) this.stageFocus.emit(stage.bbox);
  }
}
