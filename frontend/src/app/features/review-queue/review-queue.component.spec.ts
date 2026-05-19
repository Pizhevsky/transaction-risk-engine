import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { RiskApiService } from '../../core/risk-api.service';
import type { TransactionDetail, TransactionSummary } from '../../core/models';
import { ReviewQueueComponent } from './review-queue.component';

describe('ReviewQueueComponent', () => {
  let fixture: ComponentFixture<ReviewQueueComponent>;

  const summary: TransactionSummary = {
    id: 'tx-1',
    userId: 'user-1',
    userName: 'Alex Morgan',
    amount: 1250,
    currency: 'NZD',
    merchant: 'Online Store',
    successful: true,
    riskScore: 82,
    decision: 'Review',
    topReason: 'High amount',
    createdAt: new Date().toISOString()
  };
  const secondSummary: TransactionSummary = {
    ...summary,
    id: 'tx-2',
    userId: 'user-2',
    userName: 'Hana Patel',
    amount: 500,
    successful: false,
    riskScore: 45,
    topReason: 'Graph link'
  };

  const detail: TransactionDetail = {
    ...summary,
    deviceFingerprint: 'device-1',
    cardFingerprint: 'card-1',
    ipAddress: '203.0.113.99',
    signals: [
      { code: 'HIGH_AMOUNT', baseScore: 30, score: 30, reason: 'High amount', evidence: 'Current amount 1250' }
    ]
  };
  const secondDetail: TransactionDetail = {
    ...secondSummary,
    deviceFingerprint: 'device-2',
    cardFingerprint: 'card-2',
    ipAddress: '203.0.113.100',
    signals: [
      { code: 'GRAPH_RISK', baseScore: 25, score: 25, reason: 'Graph link', evidence: 'Shared device' }
    ]
  };

  const apiMock = {
    getTransactions: jasmine.createSpy('getTransactions'),
    getTransaction: jasmine.createSpy('getTransaction'),
    getConnections: jasmine.createSpy('getConnections').and.returnValue(of({
      userId: 'user-1',
      nodes: [],
      edges: [],
      riskPaths: []
    })),
    getTransactionConnections: jasmine.createSpy('getTransactionConnections').and.returnValue(of({
      userId: 'user-1',
      nodes: [],
      edges: [],
      riskPaths: []
    }))
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ReviewQueueComponent],
      providers: [{ provide: RiskApiService, useValue: apiMock }]
    }).compileComponents();

    apiMock.getTransactions.calls.reset();
    apiMock.getTransaction.calls.reset();
    apiMock.getConnections.calls.reset();
    apiMock.getTransactionConnections.calls.reset();
    apiMock.getTransactions.and.returnValue(of({
      items: [summary, secondSummary],
      totalCount: 2,
      limit: 10,
      offset: 0
    }));
    apiMock.getTransaction.and.callFake((id: string) => of(id === secondSummary.id ? secondDetail : detail));

    fixture = TestBed.createComponent(ReviewQueueComponent);
    fixture.detectChanges();
    await settleReviewQueue();
  });

  it('loads transactions through the reactive query stream', () => {
    expect(apiMock.getTransactions.calls.count()).toBeGreaterThan(0);
    expect(fixture.nativeElement.textContent).toContain('Alex Morgan');
    expect(fixture.nativeElement.textContent).toContain('Payment status');
    expect(fixture.nativeElement.textContent).toContain('Successful');
    expect(fixture.nativeElement.textContent).toContain('Failed');
  });

  it('selects the first transaction automatically', () => {
    expect(apiMock.getTransaction.calls.allArgs()).toEqual([['tx-1']]);
    expect(fixture.componentInstance.selectedId()).toBe('tx-1');
  });

  it('selects a transaction when the row is clicked', async () => {
    const rows = fixture.nativeElement.querySelectorAll('tbody tr.transaction-row') as NodeListOf<HTMLTableRowElement>;

    rows[1].click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.componentInstance.selectedId()).toBe('tx-2');
    expect(apiMock.getTransaction).toHaveBeenCalledWith('tx-2');
    expect(fixture.nativeElement.textContent).toContain('Hana Patel');
    expect(fixture.nativeElement.textContent).toContain('device-2');
    expect(apiMock.getTransactionConnections).not.toHaveBeenCalledWith('tx-2');
  });

  it('opens a large graph dialog from the selected transaction panel', async () => {
    const rows = fixture.nativeElement.querySelectorAll('tbody tr.transaction-row') as NodeListOf<HTMLTableRowElement>;

    rows[1].click();
    await fixture.whenStable();
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('button.graph-open') as HTMLButtonElement;
    button.click();
    await fixture.whenStable();
    await Promise.resolve();
    fixture.detectChanges();

    expect(fixture.componentInstance.graphOpen()).toBeTrue();
    expect(fixture.nativeElement.querySelector('[role="dialog"]')).not.toBeNull();
    expect(apiMock.getTransactionConnections).toHaveBeenCalledWith('tx-2');
  });

  async function settleReviewQueue(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, 300));
    await fixture.whenStable();
    fixture.detectChanges();
  }
});
