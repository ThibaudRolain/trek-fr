import { Component, inject, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TrackService } from './track.service';
import type { TrackProfile, TrackResponse } from './track.models';

@Component({
  selector: 'app-track-upload',
  standalone: true,
  imports: [FormsModule],
  template: `
    <div
      class="flex flex-col gap-3 rounded-lg border border-slate-800 bg-slate-950/70 p-4 text-sm text-slate-200 backdrop-blur"
      (dragover)="onDragOver($event)"
      (dragleave)="onDragLeave($event)"
      (drop)="onDrop($event)"
      [class.border-emerald-400]="isDragging()"
    >
      <div class="flex items-center justify-between">
        <label class="font-medium text-slate-100">Importer un GPX</label>
        <select
          [(ngModel)]="profile"
          class="rounded border border-slate-700 bg-slate-900 px-2 py-1 text-xs text-slate-200"
          [disabled]="loading()"
        >
          <option value="foot">À pied</option>
          <option value="mtb">VTT</option>
          <option value="road">Vélo route</option>
        </select>
      </div>

      <label
        class="flex cursor-pointer items-center justify-center rounded border border-dashed border-slate-700 px-4 py-6 text-center text-xs text-slate-400 hover:border-emerald-400 hover:text-emerald-300"
      >
        <input
          type="file"
          accept=".gpx,application/gpx+xml,application/xml,text/xml"
          class="hidden"
          (change)="onFileSelected($event)"
          [disabled]="loading()"
        />
        @if (loading()) {
          <span>Import en cours…</span>
        } @else {
          <span>Glisse un fichier .gpx ici ou clique pour choisir</span>
        }
      </label>

      @if (error(); as err) {
        <p class="text-xs text-rose-400">{{ err }}</p>
      }
    </div>
  `,
})
export class TrackUploadComponent {
  private readonly service = inject(TrackService);

  readonly imported = output<TrackResponse>();

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly isDragging = signal(false);

  profile: TrackProfile = 'foot';

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) this.upload(file);
    input.value = '';
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.isDragging.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.isDragging.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragging.set(false);
    const file = event.dataTransfer?.files?.[0];
    if (file) this.upload(file);
  }

  private upload(file: File): void {
    this.error.set(null);
    this.loading.set(true);
    this.service.importGpx(file, this.profile).subscribe({
      next: (res) => {
        this.loading.set(false);
        this.imported.emit(res);
      },
      error: (err) => {
        this.loading.set(false);
        this.error.set(err?.error?.error ?? 'Échec de l’import GPX');
      },
    });
  }
}
