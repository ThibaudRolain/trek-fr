import { Component, computed, inject, input, output, signal } from '@angular/core';
import type { StageDto, TrackResponse, TrackStats, WarningDto } from './track.models';
import { SavedTracksService } from './saved-tracks.service';

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

        @if (stages(); as stages) {
          <div class="mt-3 border-t border-slate-800 pt-2">
            <p class="mb-1 text-xs font-medium text-slate-300">
              Étapes ({{ stages.length }})
            </p>
            <p class="mb-2 text-[10px] leading-snug text-slate-500">
              Les étapes s'arrêtent dans des communes proches de la trace.
              L'app ne cherche pas sur Airbnb / Booking / Abritel — les liens
              ouvrent une recherche sur chaque site pour que tu vérifies l'offre.
            </p>
            <ul class="flex flex-col gap-1">
              @for (s of stages; track s.index) {
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
                      <a
                        [href]="airbnbUrl(s.endSleepSpot.name)"
                        target="_blank"
                        rel="noopener"
                        class="text-slate-300 hover:text-emerald-300"
                      >Airbnb</a>
                      <span class="text-slate-700">·</span>
                      <a
                        [href]="bookingUrl(s.endSleepSpot.name)"
                        target="_blank"
                        rel="noopener"
                        class="text-slate-300 hover:text-emerald-300"
                      >Booking</a>
                      <span class="text-slate-700">·</span>
                      <a
                        [href]="abritelUrl(s.endSleepSpot.name)"
                        target="_blank"
                        rel="noopener"
                        class="text-slate-300 hover:text-emerald-300"
                      >Abritel</a>
                    </div>
                  }
                </li>
              }
            </ul>
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
  readonly stageFocus = output<[number, number, number, number]>();
  readonly justSaved = signal(false);

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

  hasLodgingLinks(stage: StageDto): boolean {
    const spot = stage.endSleepSpot;
    if (spot.kind === 'town' || spot.kind === 'refuge') return true;
    return spot.kind === 'arrival' && spot.name !== 'Arrivée';
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
