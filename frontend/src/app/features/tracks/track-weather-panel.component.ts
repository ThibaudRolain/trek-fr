import { Component, input } from '@angular/core';
import type { PointWeather, WeatherDay } from './track.models';

function emojiForWmo(code: number): string {
  if (code === 0) return '☀️';
  if (code === 1) return '🌤️';
  if (code === 2) return '⛅';
  if (code === 3) return '☁️';
  if (code === 45 || code === 48) return '🌫️';
  if (code === 56 || code === 57 || code === 66 || code === 67) return '🌧️';
  if (code === 75 || code === 86) return '❄️';
  if (code === 82 || code === 95 || code === 96 || code === 99) return '⛈️';
  if (code >= 51 && code <= 61) return '🌦️';
  if (code === 63 || code === 65 || code === 81) return '🌧️';
  if ((code >= 71 && code <= 77) || code === 85) return '🌨️';
  if (code === 80) return '🌦️';
  return '❓';
}

function weekdayLabel(dateIso: string): string {
  const d = new Date(dateIso + 'T00:00:00');
  const days = ['Dim', 'Lun', 'Mar', 'Mer', 'Jeu', 'Ven', 'Sam'];
  return days[d.getDay()];
}

function dayLabel(dateIso: string): string {
  return dateIso.slice(-2);
}

@Component({
  selector: 'app-track-weather-panel',
  standalone: true,
  template: `
    @if (forecasts(); as list) {
      @if (list.length > 0) {
        <div class="rounded-lg border border-slate-800 bg-slate-950/70 text-sm text-slate-200 backdrop-blur">
          @for (p of list; track p.label) {
            <details class="group border-b border-slate-800 last:border-b-0 [&[open]>summary>.chev]:rotate-90">
              <summary class="flex cursor-pointer list-none items-center justify-between gap-3 px-4 py-2 hover:bg-slate-900/60">
                <span class="flex items-center gap-2">
                  <span class="chev inline-block text-slate-500 transition-transform">▸</span>
                  <span class="text-xs uppercase tracking-wide text-slate-400">{{ p.label }}</span>
                  <span class="font-medium text-slate-100">{{ p.communeName ?? '—' }}</span>
                  <a
                    [href]="windyUrl(p.latitude, p.longitude)"
                    target="_blank"
                    rel="noopener noreferrer"
                    (click)="$event.stopPropagation()"
                    class="text-slate-500 hover:text-sky-300"
                    title="Voir sur Windy"
                  >↗</a>
                </span>
                @if (p.forecast.length > 0) {
                  <span class="flex items-center gap-2 text-xs">
                    <span class="text-base leading-none">{{ emoji(p.forecast[0].wmoCode) }}</span>
                    <span class="font-mono text-slate-100">{{ p.forecast[0].tempMaxC.toFixed(0) }}°</span>
                  </span>
                }
              </summary>
              <div class="flex gap-2 overflow-x-auto px-3 pb-3 pt-1">
                @for (d of p.forecast; track d.date) {
                  <div class="flex min-w-14 flex-col items-center gap-0.5 rounded border border-slate-800 bg-slate-900/60 px-2 py-1.5">
                    <span class="text-[10px] uppercase tracking-wide text-slate-400">{{ weekday(d.date) }} {{ dayOfMonth(d.date) }}</span>
                    <span class="text-lg leading-none" [title]="d.summary">{{ emoji(d.wmoCode) }}</span>
                    <span class="font-mono text-xs text-slate-100">{{ d.tempMaxC.toFixed(0) }}°</span>
                    <span class="font-mono text-[10px] text-slate-500">{{ d.tempMinC.toFixed(0) }}°</span>
                    @if (d.precipitationMm > 0) {
                      <span class="font-mono text-[10px] text-sky-300">{{ d.precipitationMm.toFixed(1) }}mm</span>
                    }
                  </div>
                }
              </div>
            </details>
          }
        </div>
      }
    }
  `,
})
export class TrackWeatherPanelComponent {
  readonly forecasts = input<PointWeather[] | null>(null);

  emoji(code: number): string { return emojiForWmo(code); }
  weekday(iso: string): string { return weekdayLabel(iso); }
  dayOfMonth(iso: string): string { return dayLabel(iso); }
  windyUrl(lat: number, lon: number): string {
    return `https://www.windy.com/?${lat.toFixed(4)},${lon.toFixed(4)},10`;
  }
}
