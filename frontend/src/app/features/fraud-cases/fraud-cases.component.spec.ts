import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import type { FraudCase } from '../../core/models';
import { RiskApiService } from '../../core/risk-api.service';
import { FraudCasesComponent } from './fraud-cases.component';

describe('FraudCasesComponent', () => {
  let fixture: ComponentFixture<FraudCasesComponent>;

  const openCase: FraudCase = {
    id: 'case-1',
    transactionId: 'tx-1',
    userName: 'Alex Morgan',
    amount: 1250,
    currency: 'NZD',
    riskScore: 65,
    status: 'Open',
    summary: 'Review decision for NZD 1,250.00',
    reviewNote: null,
    createdAt: new Date().toISOString(),
    closedAt: null
  };

  const apiMock = {
    getFraudCases: jasmine.createSpy('getFraudCases'),
    updateFraudCaseStatus: jasmine.createSpy('updateFraudCaseStatus')
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FraudCasesComponent],
      providers: [
        provideRouter([]),
        { provide: RiskApiService, useValue: apiMock }
      ]
    }).compileComponents();

    apiMock.getFraudCases.calls.reset();
    apiMock.updateFraudCaseStatus.calls.reset();
    apiMock.getFraudCases.and.returnValue(of({
      items: [openCase],
      totalCount: 1,
      limit: 5,
      offset: 0
    }));
    apiMock.updateFraudCaseStatus.and.callFake((_: string, payload: { status: FraudCase['status'] }) =>
      of({ ...openCase, status: payload.status })
    );

    fixture = TestBed.createComponent(FraudCasesComponent);
    fixture.detectChanges();
  });

  it('requests at most five cases per page', async () => {
    await fixture.whenStable();

    expect(apiMock.getFraudCases).toHaveBeenCalledWith({
      search: '',
      status: 'all',
      limit: 5,
      offset: 0
    });
  });

  it('renders case cards with an exact review queue link', async () => {
    await fixture.whenStable();
    fixture.detectChanges();

    expect(fixture.componentInstance).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Alex Morgan');
    expect(fixture.nativeElement.textContent).toContain('Open transaction in review queue');

    const link = fixture.nativeElement.querySelector('a.review-link') as HTMLAnchorElement;
    expect(link.href).toContain('/review');
    expect(link.href).toContain('transactionId=tx-1');
  });

  it('updates a case status from the card action rail', async () => {
    await fixture.whenStable();
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('app-fraud-case-card button.primary') as HTMLButtonElement;
    button.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(apiMock.updateFraudCaseStatus).toHaveBeenCalledWith('case-1', {
      status: 'Investigating',
      note: ''
    });
    expect(fixture.nativeElement.textContent).toContain('Moved to Investigating.');
  });

  it('removes an updated case when it no longer matches the active status filter', async () => {
    await fixture.whenStable();
    fixture.componentInstance.status.set('open');
    fixture.componentInstance.applyFilters();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('app-fraud-case-card button.primary') as HTMLButtonElement;
    button.click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(apiMock.updateFraudCaseStatus).toHaveBeenCalledWith('case-1', {
      status: 'Investigating',
      note: ''
    });
    expect(fixture.nativeElement.querySelector('app-fraud-case-card')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('0 cases');
  });

  it('does not send a note when reopening a closed case', async () => {
    const closedCase: FraudCase = {
      ...openCase,
      status: 'ClosedApproved',
      reviewNote: 'Evidence reviewed and accepted.',
      closedAt: new Date().toISOString()
    };
    apiMock.getFraudCases.and.returnValue(of({
      items: [closedCase],
      totalCount: 1,
      limit: 5,
      offset: 0
    }));
    apiMock.updateFraudCaseStatus.and.callFake((_: string, payload: { status: FraudCase['status'] }) =>
      of({ ...closedCase, status: payload.status, closedAt: null })
    );

    fixture = TestBed.createComponent(FraudCasesComponent);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('app-fraud-case-card button.primary') as HTMLButtonElement;
    button.click();
    await fixture.whenStable();

    expect(apiMock.updateFraudCaseStatus).toHaveBeenCalledWith('case-1', {
      status: 'Investigating',
      note: null
    });
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('app-fraud-case-card input') as HTMLInputElement;
    expect(input.value).toBe('Evidence reviewed and accepted.');
  });
});
