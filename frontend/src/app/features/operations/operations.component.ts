import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription, finalize, timeout } from 'rxjs';
import type { HealthStatus, RiskEvaluationJob } from '../../core/models';
import { RiskApiService } from '../../core/risk-api.service';

@Component({
  selector: 'app-operations',
  standalone: true,
  imports: [DatePipe, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './operations.component.html',
  styleUrls: ['./operations.component.css']
})

export class OperationsComponent implements OnInit, OnDestroy {
  private static readonly RefreshTimeoutMs = 10000;
  private static readonly EvaluationStartTimeoutMs = 30000;
  private readonly api = inject(RiskApiService);
  private refreshSubscription?: Subscription;

  readonly health = signal<HealthStatus | null>(null);
  readonly jobs = signal<RiskEvaluationJob[]>([]);
  readonly healthLoading = signal(false);
  readonly jobsLoading = signal(false);
  readonly loading = computed(() => this.healthLoading() || this.jobsLoading());
  readonly evaluating = signal(false);
  readonly healthError = signal('');
  readonly jobsError = signal('');
  readonly evaluationError = signal('');
  readonly error = computed(() =>
    [this.healthError(), this.jobsError(), this.evaluationError()]
      .filter(Boolean)
      .join(' ')
  );
  readonly evaluationMessage = signal('');
  readonly lastRefreshedAt = signal<Date | null>(null);
  evaluationBatchSize = 250;
  evaluationReason = 'Manual evaluation from operations dashboard';

  ngOnInit(): void {
    this.refresh();
  }

  ngOnDestroy(): void {
    this.refreshSubscription?.unsubscribe();
  }

  refresh(): void {
    this.refreshSubscription?.unsubscribe();
    this.healthError.set('');
    this.jobsError.set('');
    this.evaluationError.set('');

    const refreshSubscription = new Subscription();
    this.refreshSubscription = refreshSubscription;

    this.loadHealth(refreshSubscription);
    this.loadJobs(refreshSubscription);
  }

  private loadHealth(refreshSubscription: Subscription): void {
    this.healthLoading.set(true);

    refreshSubscription.add(this.api.getHealthStatus().pipe(
      timeout(OperationsComponent.RefreshTimeoutMs),
      finalize(() => this.healthLoading.set(false))
    ).subscribe({
      next: (health) => {
        this.health.set(health);
        this.lastRefreshedAt.set(new Date());
      },
      error: (error) => {
        console.error('Could not load runtime status', error);
        this.healthError.set('Could not load runtime status.');
      }
    }));
  }

  private loadJobs(refreshSubscription: Subscription): void {
    this.jobsLoading.set(true);

    refreshSubscription.add(this.api.getEvaluationJobs().pipe(
      timeout(OperationsComponent.RefreshTimeoutMs),
      finalize(() => this.jobsLoading.set(false))
    ).subscribe({
      next: (jobs) => {
        this.jobs.set(jobs);
        this.lastRefreshedAt.set(new Date());
      },
      error: (error) => {
        console.error('Could not load evaluation jobs', error);
        this.jobsError.set('Could not load evaluation jobs.');
      }
    }));
  }

  runEvaluate(): void {
    if (this.evaluating()) {
      return;
    }

    this.evaluating.set(true);
    this.evaluationError.set('');
    this.evaluationMessage.set('Running rule evaluation...');

    const batchSize = Math.max(1, Math.min(1000, Number(this.evaluationBatchSize) || 250));
    const reason = this.evaluationReason.trim() || 'Manual evaluation from operations dashboard';

    this.api.evaluateRules(batchSize, reason).pipe(
      timeout(OperationsComponent.EvaluationStartTimeoutMs),
      finalize(() => this.evaluating.set(false))
    ).subscribe({
      next: (result) => {
        this.evaluationMessage.set(`Finished evaluation job ${result.jobId}. Processed ${result.processedCount} and changed ${result.changedCount}.`);
        this.refresh();
      },
      error: (error) => {
        console.error('Could not start rule evaluation', error);
        this.evaluationError.set('Could not start rule evaluation.');
        this.evaluationMessage.set('');
      }
    });
  }
}
