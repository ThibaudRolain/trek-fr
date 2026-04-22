import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TrackGenerateComponent } from './track-generate.component';
import type { TrackResponse } from './track.models';

const stubTrack: TrackResponse = {
  name: null,
  profile: 'foot',
  stats: { distanceMeters: 10_000, elevationGainMeters: 50, elevationLossMeters: 50, estimatedDurationSeconds: 7200 },
  geojson: { type: 'Feature', geometry: { type: 'LineString', coordinates: [] }, properties: {} },
  bbox: null,
  proposedDestinationName: null,
};

function setup() {
  TestBed.configureTestingModule({
    imports: [TrackGenerateComponent],
    providers: [provideHttpClient(), provideHttpClientTesting()],
  });
  const fixture = TestBed.createComponent(TrackGenerateComponent);
  const http = TestBed.inject(HttpTestingController);
  return { fixture, http };
}

function click(fixture: ComponentFixture<TrackGenerateComponent>, selector: string) {
  const el = fixture.nativeElement.querySelector(selector) as HTMLElement;
  el.click();
  fixture.detectChanges();
}

describe('TrackGenerateComponent', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('disables the submit button until a start point is provided', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    const btn = fixture.nativeElement.querySelector('button[type=submit]') as HTMLButtonElement;
    expect(btn.disabled).toBe(true);

    fixture.componentRef.setInput('startPoint', { lat: 48.85, lon: 2.35 });
    fixture.detectChanges();
    expect(btn.disabled).toBe(false);
  });

  it('emits modeChange when clicking the aToB tab', () => {
    const { fixture } = setup();
    fixture.detectChanges();
    const emissions: string[] = [];
    fixture.componentRef.instance.modeChange.subscribe((m) => emissions.push(m));

    // 2e bouton = A → B
    const buttons = fixture.nativeElement.querySelectorAll('button[type=button]');
    (buttons[1] as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(emissions).toEqual(['aToB']);
  });

  it('refuses submission with distance out of range in roundTrip and shows error', () => {
    const { fixture, http } = setup();
    fixture.componentRef.setInput('startPoint', { lat: 48.85, lon: 2.35 });
    fixture.detectChanges();

    const cmp = fixture.componentRef.instance;
    cmp.distanceKm = 150;
    (fixture.nativeElement.querySelector('button[type=submit]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(cmp.error()).toContain('entre 1 et 100');
    http.expectNone('/tracks/generate');
  });

  it('submits generate with start point and emits generated on success', async () => {
    const { fixture, http } = setup();
    fixture.componentRef.setInput('startPoint', { lat: 48.85, lon: 2.35 });
    fixture.detectChanges();

    const received: TrackResponse[] = [];
    fixture.componentRef.instance.generated.subscribe((r) => received.push(r));

    (fixture.nativeElement.querySelector('button[type=submit]') as HTMLButtonElement).click();

    const req = http.expectOne('/tracks/generate');
    expect(req.request.body.latitude).toBe(48.85);
    expect(req.request.body.mode).toBe('roundTrip');
    req.flush(stubTrack);
    fixture.detectChanges();

    expect(received.length).toBe(1);
    expect(fixture.componentRef.instance.loading()).toBe(false);
  });

  it('propagates backend error detail into the error signal', async () => {
    const { fixture, http } = setup();
    fixture.componentRef.setInput('startPoint', { lat: 48.85, lon: 2.35 });
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('button[type=submit]') as HTMLButtonElement).click();

    const req = http.expectOne('/tracks/generate');
    req.flush({ detail: 'ORS 500' }, { status: 502, statusText: 'Bad Gateway' });
    fixture.detectChanges();

    expect(fixture.componentRef.instance.error()).toBe('ORS 500');
    expect(fixture.componentRef.instance.loading()).toBe(false);
  });

  it('shows "Autre proposition" only when aToB mode had a proposed destination', () => {
    const { fixture } = setup();
    fixture.componentRef.setInput('startPoint', { lat: 48.85, lon: 2.35 });
    fixture.componentRef.setInput('mode', 'aToB');
    fixture.componentRef.setInput('track', { ...stubTrack, proposedDestinationName: 'Vézelay' });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Autre proposition');
  });

  it('does not show "Autre proposition" when end point was set by user', () => {
    const { fixture } = setup();
    fixture.componentRef.setInput('startPoint', { lat: 48.85, lon: 2.35 });
    fixture.componentRef.setInput('endPoint', { lat: 49, lon: 2.5 });
    fixture.componentRef.setInput('mode', 'aToB');
    fixture.componentRef.setInput('track', { ...stubTrack, proposedDestinationName: 'Vézelay' });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('Autre proposition');
  });

  it('aToB submit forwards endLatitude/endLongitude when end point is set', () => {
    const { fixture, http } = setup();
    fixture.componentRef.setInput('startPoint', { lat: 48.85, lon: 2.35 });
    fixture.componentRef.setInput('endPoint', { lat: 49, lon: 2.5 });
    fixture.componentRef.setInput('mode', 'aToB');
    fixture.detectChanges();

    (fixture.nativeElement.querySelector('button[type=submit]') as HTMLButtonElement).click();

    const req = http.expectOne('/tracks/generate');
    expect(req.request.body.mode).toBe('aToB');
    expect(req.request.body.endLatitude).toBe(49);
    expect(req.request.body.endLongitude).toBe(2.5);
    // seed doit être null en aToB
    expect(req.request.body.seed).toBeNull();
    req.flush(stubTrack);
  });
});
