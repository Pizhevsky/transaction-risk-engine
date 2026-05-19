import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { RiskApiService } from '../../core/risk-api.service';
import { RulesComponent } from './rules.component';

describe('RulesComponent', () => {
  let fixture: ComponentFixture<RulesComponent>;
  let component: RulesComponent;

  const apiMock = {
    getRules: jasmine.createSpy('getRules').and.returnValue(of([
      { code: 'HIGH_AMOUNT', description: 'Amount is above baseline.', weight: 30, enabled: true }
    ])),
    updateRule: jasmine.createSpy('updateRule').and.returnValue(of({
      code: 'HIGH_AMOUNT', description: 'Amount is above baseline.', weight: 35, enabled: true, message: 'Rule updated.'
    })),
    evaluateRules: jasmine.createSpy('evaluateRules').and.returnValue(of({
      jobId: 'job-1', processedCount: 10, changedCount: 2
    }))
  };

  beforeEach(async () => {
    apiMock.getRules.calls.reset();
    apiMock.updateRule.calls.reset();
    apiMock.evaluateRules.calls.reset();

    await TestBed.configureTestingModule({
      imports: [RulesComponent],
      providers: [{ provide: RiskApiService, useValue: apiMock }]
    }).compileComponents();

    fixture = TestBed.createComponent(RulesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('renders configured risk rules', () => {
    expect(fixture.nativeElement.textContent).toContain('HIGH_AMOUNT');
    expect(apiMock.getRules).toHaveBeenCalledTimes(1);
  });

  it('saves edited rules', () => {
    component.drafts.set({
      HIGH_AMOUNT: { code: 'HIGH_AMOUNT', description: 'Amount is above baseline.', weight: 35, enabled: true }
    });
    component.save('HIGH_AMOUNT');

    expect(apiMock.updateRule.calls.allArgs()).toEqual([['HIGH_AMOUNT', {
      description: 'Amount is above baseline.',
      weight: 35,
      enabled: true
    }]]);
  });

  it('can trigger rule evaluation', () => {
    component.evaluate();

    expect(apiMock.evaluateRules).toHaveBeenCalledTimes(1);
    expect(component.message()).toContain('processed 10 transactions');
  });
});
