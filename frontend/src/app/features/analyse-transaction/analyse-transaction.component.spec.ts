import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { RiskApiService } from '../../core/risk-api.service';
import { AnalyseTransactionComponent } from './analyse-transaction.component';

describe('AnalyseTransactionComponent', () => {
  let fixture: ComponentFixture<AnalyseTransactionComponent>;

  const apiMock = {
    getUsers: jasmine.createSpy('getUsers').and.returnValue(of([
      { id: '11111111-1111-1111-1111-111111111111', displayName: 'Alex Morgan', email: 'alex@example.test', isFlagged: false }
    ])),
    analyseTransaction: jasmine.createSpy('analyseTransaction').and.returnValue(of({
      transactionId: 'tx-1',
      userId: '11111111-1111-1111-1111-111111111111',
      userName: 'Alex Morgan',
      amount: 1250,
      currency: 'NZD',
      merchant: 'Online Electronics Store',
      riskScore: 82,
      decision: 'Review',
      signals: [{ code: 'HIGH_AMOUNT', baseScore: 30, score: 30, reason: 'High amount', evidence: 'Current amount 1250' }],
      createdAt: new Date().toISOString()
    }))
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AnalyseTransactionComponent],
      providers: [{ provide: RiskApiService, useValue: apiMock }]
    }).compileComponents();

    fixture = TestBed.createComponent(AnalyseTransactionComponent);
    fixture.detectChanges();
    apiMock.analyseTransaction.calls.reset();
  });

  it('creates a valid seeded demo form', () => {
    expect(fixture.componentInstance.form.valid).toBeTrue();
  });

  it('submits the transaction for analysis', () => {
    fixture.componentInstance.submit();

    expect(apiMock.analyseTransaction).toHaveBeenCalledTimes(1);
    expect(fixture.componentInstance.result()?.decision).toBe('Review');
  });

  it('renders the latest analysis result below the form', () => {
    fixture.componentInstance.submit();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Current result');
    expect(text).toContain('82/100');
    expect(text).toContain('Review');
    expect(text).toContain('HIGH_AMOUNT');
  });
});
