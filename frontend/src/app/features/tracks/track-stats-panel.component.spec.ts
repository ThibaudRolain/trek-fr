import { TestBed } from '@angular/core/testing';
import { TrackStatsPanelComponent } from './track-stats-panel.component';
import type { TrackResponse } from './track.models';

const sampleTrack: TrackResponse = {
  name: 'Sample',
  profile: 'foot',
  stats: {
    distanceMeters: 15_240,
    elevationGainMeters: 612.7,
    elevationLossMeters: 601.2,
    estimatedDurationSeconds: 3 * 3600 + 45 * 60,
  },
  geojson: {
    type: 'Feature',
    geometry: { type: 'LineString', coordinates: [] },
    properties: {},
  },
  bbox: null,
  proposedDestinationName: null,
};

describe('TrackStatsPanelComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TrackStatsPanelComponent],
    }).compileComponents();
  });

  it('renders nothing when no track is provided', () => {
    const fixture = TestBed.createComponent(TrackStatsPanelComponent);
    fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('dl')).toBeNull();
  });

  it('formats distance, elevation and duration from track stats', () => {
    const fixture = TestBed.createComponent(TrackStatsPanelComponent);
    fixture.componentRef.setInput('track', sampleTrack);
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('15.24 km');
    expect(text).toContain('+613 m');
    expect(text).toContain('−601 m');
    expect(text).toContain('3h45');
  });

  it('shows the proposed destination name when the backend proposed one', () => {
    const fixture = TestBed.createComponent(TrackStatsPanelComponent);
    fixture.componentRef.setInput('track', { ...sampleTrack, proposedDestinationName: 'Sarlat-la-Canéda' });
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Arrivée proposée');
    expect(text).toContain('Sarlat-la-Canéda');
  });
});
