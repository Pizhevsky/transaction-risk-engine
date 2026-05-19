import { of } from 'rxjs';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { OperationsComponent } from './operations.component';
import { RiskApiService } from '../../core/risk-api.service';

describe('OperationsComponent', () => {
  let fixture: ComponentFixture<OperationsComponent>;
  let component: OperationsComponent;
  const apiStub = {
    getHealthStatus: jasmine.createSpy('getHealthStatus').and.returnValue(of({
      status: 'ok',
      database: 'reachable',
      riskRules: 5,
      outbox: { pending: 1, processing: 0, failed: 0, oldestPendingAgeSeconds: 2 }
    })),
    getEvaluationJobs: jasmine.createSpy('getEvaluationJobs').and.returnValue(of([])),
    evaluateRules: jasmine.createSpy('evaluateRules').and.returnValue(of({
      jobId: 'job-1',
      processedCount: 20,
      changedCount: 4
    }))
  };

  beforeEach(async () => {
    apiStub.getHealthStatus.calls.reset();
    apiStub.getEvaluationJobs.calls.reset();
    apiStub.evaluateRules.calls.reset();

    await TestBed.configureTestingModule({
      imports: [OperationsComponent],
      providers: [{ provide: RiskApiService, useValue: apiStub }]
    }).compileComponents();

    fixture = TestBed.createComponent(OperationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads operational status', () => {
    expect(component.health()?.status).toBe('ok');
    expect(apiStub.getHealthStatus).toHaveBeenCalledTimes(1);
    expect(apiStub.getEvaluationJobs).toHaveBeenCalledTimes(1);
  });

  it('starts rule evaluation with analyst supplied options', () => {
    component.evaluationBatchSize = 75;
    component.evaluationReason = 'Rule update after calibration';

    component.runEvaluate();

    expect(apiStub.evaluateRules.calls.allArgs()).toEqual([[75, 'Rule update after calibration']]);
    expect(component.evaluationMessage()).toContain('Processed 20');
  });
});
