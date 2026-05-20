import { of, throwError } from 'rxjs';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { OperationsComponent } from './operations.component';
import { RiskApiService } from '../../core/risk-api.service';

describe('OperationsComponent', () => {
  let fixture: ComponentFixture<OperationsComponent>;
  let component: OperationsComponent;
  const healthStatus = {
    status: 'ok',
    database: 'reachable',
    riskRules: 5,
    outbox: { pending: 1, processing: 0, failed: 0, oldestPendingAgeSeconds: 2 }
  };
  const evaluationResult = {
    jobId: 'job-1',
    processedCount: 20,
    changedCount: 4
  };
  const apiStub = {
    getHealthStatus: jasmine.createSpy('getHealthStatus').and.returnValue(of(healthStatus)),
    getEvaluationJobs: jasmine.createSpy('getEvaluationJobs').and.returnValue(of([])),
    evaluateRules: jasmine.createSpy('evaluateRules').and.returnValue(of(evaluationResult))
  };

  beforeEach(async () => {
    apiStub.getHealthStatus.calls.reset();
    apiStub.getEvaluationJobs.calls.reset();
    apiStub.evaluateRules.calls.reset();
    apiStub.getHealthStatus.and.returnValue(of(healthStatus));
    apiStub.getEvaluationJobs.and.returnValue(of([]));
    apiStub.evaluateRules.and.returnValue(of(evaluationResult));

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

  it('keeps runtime health visible when evaluation jobs fail to load', () => {
    spyOn(console, 'error');
    apiStub.getEvaluationJobs.and.returnValue(throwError(() => new Error('jobs offline')));

    component.refresh();

    expect(component.health()?.status).toBe('ok');
    expect(component.error()).toContain('Could not load evaluation jobs.');
  });
});
