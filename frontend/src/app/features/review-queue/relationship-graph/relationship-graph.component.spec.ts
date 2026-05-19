import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { RiskApiService } from '../../../core/risk-api.service';
import { RelationshipGraphComponent } from './relationship-graph.component';

describe('RelationshipGraphComponent', () => {
  let fixture: ComponentFixture<RelationshipGraphComponent>;

  const apiMock = {
    getTransactionConnections: jasmine.createSpy('getTransactionConnections').and.returnValue(of({
      userId: 'user-1',
      nodes: [
        { id: 'U:user-1', label: 'Alex Morgan', type: 'User', isRisky: false },
        { id: 'IP:1', label: '203.0.113.99', type: 'IP', isRisky: true }
      ],
      edges: [{ id: 'edge-1', source: 'U:user-1', target: 'IP:1', label: 'linked' }],
      riskPaths: ['Alex Morgan -> 203.0.113.99']
    }))
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RelationshipGraphComponent],
      providers: [{ provide: RiskApiService, useValue: apiMock }]
    }).compileComponents();

    apiMock.getTransactionConnections.calls.reset();
    fixture = TestBed.createComponent(RelationshipGraphComponent);
    fixture.componentRef.setInput('transactionId', 'tx-1');
    fixture.detectChanges();
  });

  it('loads and renders risk paths', async () => {
    await Promise.resolve();
    fixture.detectChanges();

    expect(apiMock.getTransactionConnections.calls.allArgs()).toEqual([['tx-1']]);
    expect(fixture.nativeElement.textContent).toContain('Alex Morgan -> 203.0.113.99');
  });

  it('does not create a DOM overlay for the focused graph label', async () => {
    await Promise.resolve();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('.graph-root-label')).toBeNull();
  });

  it('reloads when the selected transaction changes', async () => {
    await Promise.resolve();
    fixture.detectChanges();

    fixture.componentRef.setInput('transactionId', 'tx-2');
    fixture.detectChanges();
    await fixture.whenStable();

    expect(apiMock.getTransactionConnections.calls.allArgs()).toEqual([['tx-1'], ['tx-2']]);
  });
});
