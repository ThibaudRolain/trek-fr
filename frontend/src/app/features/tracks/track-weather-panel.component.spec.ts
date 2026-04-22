import { TestBed } from '@angular/core/testing';
import { TrackWeatherPanelComponent } from './track-weather-panel.component';
import type { PointWeather } from './track.models';

function point(
  label: string,
  forecast: Partial<PointWeather['forecast'][number]>[] = [],
): PointWeather {
  return {
    label,
    communeName: 'Paris',
    latitude: 48.8566,
    longitude: 2.3522,
    forecast: forecast.map((f, i) => ({
      date: f.date ?? `2026-06-0${i + 1}`,
      tempMinC: f.tempMinC ?? 10,
      tempMaxC: f.tempMaxC ?? 22,
      precipitationMm: f.precipitationMm ?? 0,
      windKmh: f.windKmh ?? 10,
      wmoCode: f.wmoCode ?? 0,
      summary: f.summary ?? 'Ciel clair',
    })),
  };
}

describe('TrackWeatherPanelComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [TrackWeatherPanelComponent] }).compileComponents();
  });

  function render(forecasts: PointWeather[] | null) {
    const fixture = TestBed.createComponent(TrackWeatherPanelComponent);
    fixture.componentRef.setInput('forecasts', forecasts);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders nothing when forecasts are null', () => {
    const el = render(null);
    expect(el.querySelector('details')).toBeNull();
  });

  it('renders nothing when forecasts is empty', () => {
    const el = render([]);
    expect(el.querySelector('details')).toBeNull();
  });

  it('renders one <details> per point with commune name', () => {
    const el = render([
      point('Départ', [{ wmoCode: 0, tempMaxC: 22 }]),
      point('Arrivée', [{ wmoCode: 3, tempMaxC: 18 }]),
    ]);
    const details = el.querySelectorAll('details');
    expect(details.length).toBe(2);
    expect(details[0].textContent).toContain('Départ');
    expect(details[0].textContent).toContain('Paris');
  });

  it('shows emoji and max temperature for the first day in the summary row', () => {
    const el = render([point('Départ', [{ wmoCode: 0, tempMaxC: 21.7 }])]);
    const text = el.textContent ?? '';
    expect(text).toContain('☀️');
    // .toFixed(0) → 22
    expect(text).toContain('22°');
  });

  it('renders one card per day in the strip with weekday + day number', () => {
    const el = render([
      point('Départ', [
        { date: '2026-06-01', wmoCode: 0, tempMaxC: 22, tempMinC: 12 },
        { date: '2026-06-02', wmoCode: 61, tempMaxC: 18, tempMinC: 10, precipitationMm: 2.4 },
      ]),
    ]);
    // 1 card de summary + 2 cards dans la strip = strip cards = container divs with min-w-14
    const dayCards = el.querySelectorAll('.flex.min-w-14');
    expect(dayCards.length).toBe(2);
    // jour 2 = 02, jour 1 = 01
    expect(dayCards[0].textContent).toContain('01');
    expect(dayCards[1].textContent).toContain('02');
  });

  it('shows precipitation only when > 0', () => {
    const el = render([
      point('Départ', [
        { date: '2026-06-01', precipitationMm: 0 },
        { date: '2026-06-02', precipitationMm: 2.4 },
      ]),
    ]);
    const text = el.textContent ?? '';
    expect(text).toContain('2.4mm');
    // precip 0 → pas affiché
    const precipNodes = el.querySelectorAll('.text-sky-300');
    expect(precipNodes.length).toBe(1);
  });

  it('builds a Windy deep link with 4 decimals, zoom 11 and picker overlay', () => {
    const el = render([point('Départ', [{ wmoCode: 0 }])]);
    const link = el.querySelector('a[href*="windy.com"]') as HTMLAnchorElement;
    expect(link).toBeTruthy();
    expect(link.href).toBe('https://www.windy.com/?48.8566,2.3522,11,d:picker');
    expect(link.target).toBe('_blank');
    expect(link.rel).toContain('noopener');
  });

  it('falls back to em-dash when commune name is null', () => {
    const base = point('Départ', [{ wmoCode: 0 }]);
    base.communeName = null;
    const el = render([base]);
    expect(el.textContent).toContain('—');
  });
});
