import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import type { FraudCase } from '../../core/models';
import { FraudCaseCardComponent } from './fraud-case-card.component';

describe('FraudCaseCardComponent', () => {
  let fixture: ComponentFixture<FraudCaseCardComponent>;
  let component: FraudCaseCardComponent;

  const investigatingCase: FraudCase = {
    id: 'case-1',
    transactionId: 'tx-1',
    userName: 'Alex Morgan',
    amount: 1250,
    currency: 'NZD',
    riskScore: 65,
    status: 'Investigating',
    summary: 'Review decision for NZD 1,250.00',
    createdAt: new Date().toISOString(),
    closedAt: null
  };
  const closedCase: FraudCase = {
    ...investigatingCase,
    status: 'ClosedApproved',
    reviewNote: 'Evidence reviewed and accepted.',
    closedAt: new Date().toISOString()
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FraudCaseCardComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(FraudCaseCardComponent);
    component = fixture.componentInstance;
    component.item = investigatingCase;
  });

  it('shows Saving only on the pending action while disabling all row actions', () => {
    component.pendingStatus = 'ClosedApproved';

    fixture.detectChanges();

    const buttons = Array.from(
      fixture.nativeElement.querySelectorAll('.actions button')
    ) as HTMLButtonElement[];

    expect(buttons.length).toBe(3);
    expect(buttons.every((button) => button.disabled)).toBeTrue();
    expect(buttons.filter((button) => button.textContent?.includes('Saving...')).length).toBe(1);

    const savingButton = buttons.find((button) => button.textContent?.includes('Saving...'));
    expect(savingButton?.getAttribute('aria-busy')).toBe('true');

    const siblingButtons = buttons.filter((button) => !button.textContent?.includes('Saving...'));
    expect(siblingButtons.every((button) => button.getAttribute('aria-busy') === null)).toBeTrue();
  });

  it('emits the selected status when an action is clicked', () => {
    spyOn(component.statusChange, 'emit');

    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('.actions button') as HTMLButtonElement;
    button.click();

    expect(component.statusChange.emit).toHaveBeenCalledWith('Open');
  });

  it('ignores sibling action clicks while one action is pending', () => {
    spyOn(component.statusChange, 'emit');
    component.pendingStatus = 'ClosedApproved';

    fixture.detectChanges();

    const button = fixture.nativeElement.querySelector('.actions button') as HTMLButtonElement;
    button.click();

    expect(component.statusChange.emit).not.toHaveBeenCalled();
  });

  it('shows a read-only review note on closed cases', () => {
    component.item = closedCase;

    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('input')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Review note');
    expect(fixture.nativeElement.textContent).toContain('Evidence reviewed and accepted.');
  });

  it('shows the existing review note in the action note input after reopening', async () => {
    component.item = {
      ...closedCase,
      status: 'Investigating',
      closedAt: null
    };
    component.note = closedCase.reviewNote ?? '';

    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    expect(input.value).toBe('Evidence reviewed and accepted.');
  });
});
