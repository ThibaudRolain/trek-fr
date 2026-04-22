import { Injectable, signal } from '@angular/core';
import type { TrackResponse } from './track.models';

export interface SavedTrack {
  id: string;
  name: string;
  savedAt: string;
  track: TrackResponse;
}

const STORAGE_KEY = 'trek-fr:saved-tracks:v1';

@Injectable({ providedIn: 'root' })
export class SavedTracksService {
  private readonly _tracks = signal<SavedTrack[]>(this.load());
  readonly tracks = this._tracks.asReadonly();

  save(track: TrackResponse, name: string): SavedTrack {
    const entry: SavedTrack = {
      id: crypto.randomUUID(),
      name: name.trim() || track.proposedDestinationName || 'Trace sans nom',
      savedAt: new Date().toISOString(),
      track,
    };
    const next = [entry, ...this._tracks()];
    this._tracks.set(next);
    this.persist(next);
    return entry;
  }

  remove(id: string): void {
    const next = this._tracks().filter((t) => t.id !== id);
    this._tracks.set(next);
    this.persist(next);
  }

  clear(): void {
    this._tracks.set([]);
    this.persist([]);
  }

  private load(): SavedTrack[] {
    if (typeof localStorage === 'undefined') return [];
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return [];
    try {
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      // Corrupted storage (manual edit, version drift) — reset silently.
      return [];
    }
  }

  private persist(tracks: SavedTrack[]): void {
    if (typeof localStorage === 'undefined') return;
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(tracks));
    } catch {
      // Over quota (5 MB default) — drop the oldest until it fits.
      const trimmed = tracks.slice(0, Math.max(1, Math.floor(tracks.length / 2)));
      localStorage.setItem(STORAGE_KEY, JSON.stringify(trimmed));
      this._tracks.set(trimmed);
    }
  }
}
