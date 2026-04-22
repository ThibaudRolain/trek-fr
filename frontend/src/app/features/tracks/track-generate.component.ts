import { Component, computed, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TrackService } from './track.service';
import type { LatLon, TrackMode, TrackProfile, TrackResponse } from './track.models';

@Component({
  selector: 'app-track-generate',
  standalone: true,
  imports: [FormsModule],
  template: `
    <form
      class="flex flex-col gap-3 rounded-lg border border-slate-800 bg-slate-950/70 p-4 text-sm text-slate-200 backdrop-blur"
      (submit)="submit($event)"
    >
      <div class="flex items-center justify-between">
        <h2 class="font-medium text-slate-100">Générer une trace</h2>
        <span class="text-xs text-slate-500">ORS</span>
      </div>

      <div class="grid grid-cols-2 overflow-hidden rounded border border-slate-700 text-xs">
        <button
          type="button"
          (click)="setMode('roundTrip')"
          [class]="tabClass(mode() === 'roundTrip')"
        >
          Round-trip
        </button>
        <button
          type="button"
          (click)="setMode('aToB')"
          [class]="tabClass(mode() === 'aToB')"
        >
          A → B
        </button>
      </div>

      <div class="rounded border border-slate-800 bg-slate-900/60 p-2 text-xs">
        @if (startPoint(); as p) {
          <div class="flex items-center justify-between">
            <span class="flex items-center gap-1 text-slate-400">
              <span class="inline-block h-2 w-2 rounded-full bg-amber-500"></span>
              Départ
            </span>
            <span class="font-mono text-slate-100">{{ p.lat.toFixed(5) }}, {{ p.lon.toFixed(5) }}</span>
          </div>
        } @else {
          <p class="text-slate-400">Clique sur la carte pour poser le départ.</p>
        }

        @if (mode() === 'aToB') {
          <div class="mt-1 border-t border-slate-800 pt-1">
            @if (endPoint(); as p) {
              <div class="flex items-center justify-between">
                <span class="flex items-center gap-1 text-slate-400">
                  <span class="inline-block h-2 w-2 rounded-full bg-red-500"></span>
                  Arrivée
                </span>
                <span class="font-mono text-slate-100">{{ p.lat.toFixed(5) }}, {{ p.lon.toFixed(5) }}</span>
              </div>
            } @else if (startPoint()) {
              <p class="text-slate-400">
                Arrivée (optionnelle) — clique à nouveau pour la poser,
                <span class="text-slate-500">
                  ou laisse l'app te proposer une ville cohérente.
                </span>
              </p>
            } @else {
              <p class="text-slate-500">Arrivée : pose le départ d'abord.</p>
            }
          </div>
        }
      </div>

      <label class="flex flex-col gap-1 text-xs">
        <span class="text-slate-400">
          Distance cible (km)
          @if (mode() === 'aToB') {
            <span class="text-slate-500">— indicative en A→B</span>
          } @else {
            <span class="text-slate-500">— max 100</span>
          }
        </span>
        <input
          type="number"
          min="1"
          max="100"
          step="1"
          [(ngModel)]="distanceKm"
          name="distanceKm"
          class="rounded border border-slate-700 bg-slate-900 px-2 py-1 text-slate-100"
          [disabled]="loading()"
        />
      </label>

      <label class="flex flex-col gap-1 text-xs">
        <span class="text-slate-400">Profil</span>
        <select
          [(ngModel)]="profile"
          name="profile"
          class="rounded border border-slate-700 bg-slate-900 px-2 py-1 text-slate-100"
          [disabled]="loading()"
        >
          <option value="foot">À pied</option>
          <option value="mtb">VTT</option>
          <option value="road">Vélo route</option>
        </select>
      </label>

      @if (mode() === 'roundTrip') {
        <label class="flex flex-col gap-1 text-xs">
          <span class="text-slate-400">Seed (optionnel, pour varier)</span>
          <input
            type="number"
            [(ngModel)]="seed"
            name="seed"
            placeholder="aléatoire"
            class="rounded border border-slate-700 bg-slate-900 px-2 py-1 text-slate-100"
            [disabled]="loading()"
          />
        </label>
      }

      <div class="rounded border border-slate-800 bg-slate-900/60 p-2 text-xs">
        <label class="flex items-center gap-2">
          <input
            type="checkbox"
            [(ngModel)]="splitStages"
            name="splitStages"
            class="h-3 w-3 accent-emerald-500"
            [disabled]="loading()"
          />
          <span class="text-slate-300">Découper en étapes</span>
        </label>

        @if (splitStages) {
          <div class="mt-2 grid grid-cols-2 gap-2">
            <label class="flex flex-col gap-1">
              <span class="text-slate-400">km / jour</span>
              <input
                type="number"
                min="1"
                max="100"
                step="1"
                [(ngModel)]="stageDistanceKm"
                name="stageDistanceKm"
                class="rounded border border-slate-700 bg-slate-900 px-2 py-1 text-slate-100"
                [disabled]="loading()"
              />
            </label>
            <label class="flex flex-col gap-1">
              <span class="text-slate-400">D+ / jour (m)</span>
              <input
                type="number"
                min="1"
                max="10000"
                step="50"
                [(ngModel)]="stageElevationGain"
                name="stageElevationGain"
                class="rounded border border-slate-700 bg-slate-900 px-2 py-1 text-slate-100"
                [disabled]="loading()"
              />
            </label>
          </div>
        }
      </div>

      <button
        type="submit"
        [disabled]="!canSubmit()"
        class="rounded bg-emerald-500 px-3 py-2 text-sm font-medium text-slate-950 hover:bg-emerald-400 disabled:cursor-not-allowed disabled:bg-slate-700 disabled:text-slate-500"
      >
        @if (loading()) {
          Génération…
        } @else {
          Générer
        }
      </button>

      @if (canProposeAnother()) {
        <button
          type="button"
          (click)="submit($event)"
          [disabled]="loading()"
          class="rounded border border-slate-700 bg-slate-900 px-3 py-2 text-xs text-slate-200 hover:border-slate-600 hover:bg-slate-800 disabled:cursor-not-allowed disabled:text-slate-500"
        >
          Autre proposition
        </button>
      }

      @if (error(); as err) {
        <p class="text-xs text-rose-400">{{ err }}</p>
      }
    </form>
  `,
})
export class TrackGenerateComponent {
  private readonly service = inject(TrackService);

  readonly startPoint = input<LatLon | null>(null);
  readonly endPoint = input<LatLon | null>(null);
  readonly mode = input<TrackMode>('roundTrip');
  readonly track = input<TrackResponse | null>(null);
  readonly modeChange = output<TrackMode>();
  readonly generated = output<TrackResponse>();

  distanceKm = 15;
  profile: TrackProfile = 'foot';
  seed: number | null = null;
  splitStages = false;
  stageDistanceKm = 22;
  stageElevationGain = 1000;

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly canSubmit = computed(() => {
    if (this.loading() || this.startPoint() === null) return false;
    return true;
  });

  readonly canProposeAnother = computed(() =>
    this.mode() === 'aToB' &&
    this.endPoint() === null &&
    this.track()?.proposedDestinationName != null
  );

  setMode(mode: TrackMode): void {
    if (mode === this.mode()) return;
    this.error.set(null);
    this.modeChange.emit(mode);
  }

  tabClass(active: boolean): string {
    const base = 'px-3 py-1 transition';
    return active
      ? `${base} bg-emerald-500 text-slate-950`
      : `${base} bg-slate-900 text-slate-300 hover:bg-slate-800`;
  }

  submit(event: Event): void {
    event.preventDefault();
    const start = this.startPoint();
    if (!start) return;

    const mode = this.mode();
    if (mode === 'roundTrip' && (this.distanceKm <= 0 || this.distanceKm > 100)) {
      this.error.set('La distance doit être entre 1 et 100 km.');
      return;
    }
    if (this.splitStages) {
      if (this.stageDistanceKm <= 0 || this.stageDistanceKm > 100) {
        this.error.set('km / jour doit être entre 1 et 100.');
        return;
      }
      if (this.stageElevationGain <= 0 || this.stageElevationGain > 10000) {
        this.error.set('D+ / jour doit être entre 1 et 10000 m.');
        return;
      }
    }

    const end = this.endPoint();

    this.error.set(null);
    this.loading.set(true);
    this.service
      .generate({
        latitude: start.lat,
        longitude: start.lon,
        distanceKm: this.distanceKm,
        profile: this.profile,
        seed: mode === 'roundTrip' ? (this.seed ?? undefined) : undefined,
        mode,
        endLatitude: mode === 'aToB' && end ? end.lat : undefined,
        endLongitude: mode === 'aToB' && end ? end.lon : undefined,
        splitStages: this.splitStages,
        stageDistanceKm: this.splitStages ? this.stageDistanceKm : undefined,
        stageElevationGain: this.splitStages ? this.stageElevationGain : undefined,
      })
      .subscribe({
        next: (res) => {
          this.loading.set(false);
          this.generated.emit(res);
        },
        error: (err) => {
          this.loading.set(false);
          const detail = err?.error?.detail ?? err?.error?.error ?? err?.message;
          this.error.set(detail ?? 'Échec de la génération.');
        },
      });
  }
}
