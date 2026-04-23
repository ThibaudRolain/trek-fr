import { Component, computed, effect, inject, input, signal } from '@angular/core';
import type { CompositionDto, CompositionEntry, DestinationInfo, TrackResponse, TrackStats, WarningDto } from './track.models';
import { SavedTracksService } from './saved-tracks.service';

const WAYTYPE_LABELS: Record<number, string> = {
  0: 'Autre',
  1: 'Route nationale',
  2: 'Route',
  3: 'Rue',
  4: 'Sentier',
  5: 'Piste',
  6: 'Piste cyclable',
  7: 'Chemin agricole',
  8: 'Escaliers',
};

const SURFACE_LABELS: Record<number, string> = {
  0: 'Autre',
  1: 'Asphalte',
  2: 'Béton',
  3: 'Pavé (irrégulier)',
  4: 'Pavé',
  5: 'Gravier',
  6: 'Terre',
  7: 'Herbe',
  8: 'Sable',
  9: 'Bois',
  10: 'Pierre',
  11: 'Sel',
  12: 'Neige',
  13: 'Glace',
};

const BAR_COLORS_WAYTYPE: Record<number, string> = {
  0: '#64748b',
  1: '#dc2626',
  2: '#f97316',
  3: '#facc15',
  4: '#22c55e',
  5: '#86efac',
  6: '#38bdf8',
  7: '#a78bfa',
  8: '#f472b6',
};

const BAR_COLORS_SURFACE: Record<number, string> = {
  0: '#64748b',
  1: '#6b7280',
  2: '#9ca3af',
  3: '#d97706',
  4: '#b45309',
  5: '#a16207',
  6: '#65a30d',
  7: '#16a34a',
  8: '#ca8a04',
  9: '#78350f',
  10: '#1e293b',
  11: '#e2e8f0',
  12: '#bae6fd',
  13: '#7dd3fc',
};

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
        @if (destinationInfo(); as info) {
          <div class="mb-2 overflow-hidden rounded-md border border-slate-800">
            @if (heroImageUrl(); as imgUrl) {
              <img [src]="imgUrl" [alt]="info.name" class="h-24 w-full object-cover">
            }
            <div class="px-2 py-1.5">
              <div class="flex items-center justify-between">
                <span class="text-xs font-medium text-slate-100">{{ info.name }}</span>
                <a [href]="wikipediaUrl(info.name)" target="_blank" rel="noopener" class="text-[10px] text-sky-400 hover:text-sky-300">Wikipedia →</a>
              </div>
              <div class="mt-1 flex flex-wrap gap-1">
                @if (info.isPlusBeauVillage) {
                  <span class="rounded bg-amber-900/60 px-1.5 py-0.5 text-[10px] font-medium text-amber-200 ring-1 ring-amber-700/50">Plus Beau Village ✦</span>
                }
                @if (info.isVilleArtHistoire) {
                  <span class="rounded bg-sky-900/60 px-1.5 py-0.5 text-[10px] font-medium text-sky-200 ring-1 ring-sky-700/50">Ville d'art et d'histoire</span>
                }
                @if (info.monumentsHistoriques != null && info.monumentsHistoriques > 0) {
                  <span class="rounded bg-slate-800 px-1.5 py-0.5 text-[10px] text-slate-300 ring-1 ring-slate-700">{{ info.monumentsHistoriques }} MH</span>
                }
                @if (!info.isPlusBeauVillage && !info.isVilleArtHistoire && (info.monumentsHistoriques == null || info.monumentsHistoriques === 0)) {
                  <span class="text-[10px] text-slate-500">Pas de label patrimonial</span>
                }
              </div>
            </div>
          </div>
        } @else if (t.proposedDestinationName; as dest) {
          <p class="mb-2 text-xs text-slate-400">
            Arrivée proposée :
            <span class="font-medium text-slate-100">{{ dest }}</span>
          </p>
        }
        @if (t.warnings && t.warnings.length > 0) {
          <div class="mb-2 rounded border border-amber-600/40 bg-amber-900/20 px-2 py-1.5 text-xs text-amber-200">
            @for (w of t.warnings; track w.message) {
              <p class="leading-snug">⚠️ {{ w.message }}</p>
              @if (w.nearbyPlace) {
                <p class="mt-1 text-[10px] text-amber-100/80">
                  Commune la plus proche : <span class="font-medium">{{ w.nearbyPlace }}</span>
                  @if (w.nearbyPlaceDistanceMeters != null) {
                    ({{ formatKmFromMeters(w.nearbyPlaceDistanceMeters) }} km)
                  }
                </p>
                <div class="mt-1 flex items-center gap-2 text-[10px]">
                  <span class="text-amber-100/60">Cherche manuellement :</span>
                  <a
                    [href]="airbnbUrl(w.nearbyPlace)"
                    target="_blank"
                    rel="noopener"
                    class="text-amber-200 underline hover:text-emerald-300"
                  >Airbnb</a>
                  <span class="text-amber-100/40">·</span>
                  <a
                    [href]="bookingUrl(w.nearbyPlace)"
                    target="_blank"
                    rel="noopener"
                    class="text-amber-200 underline hover:text-emerald-300"
                  >Booking</a>
                  <span class="text-amber-100/40">·</span>
                  <a
                    [href]="abritelUrl(w.nearbyPlace)"
                    target="_blank"
                    rel="noopener"
                    class="text-amber-200 underline hover:text-emerald-300"
                  >Abritel</a>
                </div>
              }
            }
          </div>
        }
        @if (t.seed !== null) {
          <p class="mb-2 text-xs text-slate-500">
            Variante #<span class="font-mono text-slate-400">{{ t.seed }}</span>
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

        @if (t.composition; as comp) {
          <div class="mt-3 border-t border-slate-800 pt-2">
            @if (comp.wayTypes.length > 0) {
              <p class="mb-1 text-[10px] font-medium uppercase tracking-wide text-slate-400">Type de voie</p>
              <div class="mb-1 flex h-3 w-full overflow-hidden rounded-sm">
                @for (entry of sortedEntries(comp.wayTypes); track entry.typeId) {
                  <div
                    [style.width.%]="entry.amount"
                    [style.background-color]="waytypeColor(entry.typeId)"
                    [title]="waytypeLabel(entry.typeId) + ' ' + entry.amount.toFixed(1) + '%'"
                  ></div>
                }
              </div>
              <div class="flex flex-wrap gap-x-3 gap-y-0.5">
                @for (entry of sortedEntries(comp.wayTypes); track entry.typeId) {
                  <span class="flex items-center gap-1 text-[10px] text-slate-400">
                    <span class="inline-block h-2 w-2 rounded-sm" [style.background-color]="waytypeColor(entry.typeId)"></span>
                    {{ waytypeLabel(entry.typeId) }}
                    <span class="font-mono text-slate-500">{{ entry.amount.toFixed(0) }}%</span>
                  </span>
                }
              </div>
            }
            @if (comp.surface.length > 0) {
              <p class="mb-1 mt-2 text-[10px] font-medium uppercase tracking-wide text-slate-400">Surface</p>
              <div class="mb-1 flex h-3 w-full overflow-hidden rounded-sm">
                @for (entry of sortedEntries(comp.surface); track entry.typeId) {
                  <div
                    [style.width.%]="entry.amount"
                    [style.background-color]="surfaceColor(entry.typeId)"
                    [title]="surfaceLabel(entry.typeId) + ' ' + entry.amount.toFixed(1) + '%'"
                  ></div>
                }
              </div>
              <div class="flex flex-wrap gap-x-3 gap-y-0.5">
                @for (entry of sortedEntries(comp.surface); track entry.typeId) {
                  <span class="flex items-center gap-1 text-[10px] text-slate-400">
                    <span class="inline-block h-2 w-2 rounded-sm" [style.background-color]="surfaceColor(entry.typeId)"></span>
                    {{ surfaceLabel(entry.typeId) }}
                    <span class="font-mono text-slate-500">{{ entry.amount.toFixed(0) }}%</span>
                  </span>
                }
              </div>
            }
          </div>
        }

        <button
          type="button"
          (click)="saveTrack(t)"
          [disabled]="justSaved()"
          class="mt-3 w-full rounded border border-slate-700 bg-slate-900 px-2 py-1 text-xs text-slate-200 hover:border-emerald-500 hover:text-emerald-400 disabled:cursor-not-allowed disabled:text-slate-500"
        >
          @if (justSaved()) {
            Sauvegardée ✓
          } @else {
            Sauver cette trace
          }
        </button>
      </div>
    }
  `,
})
export class TrackStatsPanelComponent {
  private readonly saved = inject(SavedTracksService);
  readonly track = input<TrackResponse | null>(null);
  readonly justSaved = signal(false);
  readonly heroImageUrl = signal<string | null>(null);

  readonly destinationInfo = computed<DestinationInfo | null>(() => this.track()?.destinationInfo ?? null);

  constructor() {
    effect(() => {
      const info = this.destinationInfo();
      if (!info) { this.heroImageUrl.set(null); return; }
      fetch(`https://fr.wikipedia.org/api/rest_v1/page/summary/${encodeURIComponent(info.name)}`)
        .then(r => r.ok ? r.json() : null)
        .then(data => this.heroImageUrl.set(data?.thumbnail?.source ?? null))
        .catch(() => this.heroImageUrl.set(null));
    });
  }

  wikipediaUrl(name: string): string {
    return `https://fr.wikipedia.org/wiki/${encodeURIComponent(name)}`;
  }

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

  sortedEntries(entries: CompositionEntry[]): CompositionEntry[] {
    return [...entries].sort((a, b) => b.amount - a.amount);
  }

  waytypeLabel(typeId: number): string {
    return WAYTYPE_LABELS[typeId] ?? `Type ${typeId}`;
  }

  surfaceLabel(typeId: number): string {
    return SURFACE_LABELS[typeId] ?? `Surface ${typeId}`;
  }

  waytypeColor(typeId: number): string {
    return BAR_COLORS_WAYTYPE[typeId] ?? '#64748b';
  }

  surfaceColor(typeId: number): string {
    return BAR_COLORS_SURFACE[typeId] ?? '#64748b';
  }

  formatKmFromMeters(meters: number): string {
    return (meters / 1000).toFixed(1);
  }

  saveTrack(track: TrackResponse): void {
    const suggested = track.proposedDestinationName
      ?? track.name
      ?? `Trace ${(track.stats.distanceMeters / 1000).toFixed(0)} km`;
    const name = window.prompt('Nom de la trace', suggested);
    if (name === null) return;
    this.saved.save(track, name);
    this.justSaved.set(true);
    setTimeout(() => this.justSaved.set(false), 2000);
  }
}
