import { Component, computed, input } from '@angular/core';
import type { TrackResponse } from './track.models';

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
          <dd class="text-right font-mono text-slate-100">{{ distanceKm() }} km</dd>

          <dt class="text-slate-400">Dénivelé +</dt>
          <dd class="text-right font-mono text-emerald-300">+{{ gainM() }} m</dd>

          <dt class="text-slate-400">Dénivelé −</dt>
          <dd class="text-right font-mono text-rose-300">−{{ lossM() }} m</dd>

          <dt class="text-slate-400">Durée estimée</dt>
          <dd class="text-right font-mono text-slate-100">{{ duration() }}</dd>
        </dl>
      </div>
    }
  `,
})
export class TrackStatsPanelComponent {
  readonly track = input<TrackResponse | null>(null);

  readonly distanceKm = computed(() => {
    const t = this.track();
    return t ? (t.stats.distanceMeters / 1000).toFixed(2) : '0';
  });

  readonly gainM = computed(() => {
    const t = this.track();
    return t ? Math.round(t.stats.elevationGainMeters) : 0;
  });

  readonly lossM = computed(() => {
    const t = this.track();
    return t ? Math.round(t.stats.elevationLossMeters) : 0;
  });

  readonly duration = computed(() => {
    const t = this.track();
    if (!t) return '—';
    const sec = Math.round(t.stats.estimatedDurationSeconds);
    const h = Math.floor(sec / 3600);
    const m = Math.round((sec % 3600) / 60);
    return h > 0 ? `${h}h${m.toString().padStart(2, '0')}` : `${m} min`;
  });
}
