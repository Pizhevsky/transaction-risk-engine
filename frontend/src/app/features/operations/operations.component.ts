import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription, finalize, forkJoin, timeout } from 'rxjs';
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
  readonly loading = signal(false);
  readonly evaluating = signal(false);
  readonly error = signal('');
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
    this.loading.set(true);
    this.error.set('');

    this.refreshSubscription = forkJoin({
      health: this.api.getHealthStatus(),
      jobs: this.api.getEvaluationJobs()
    }).pipe(
      timeout(OperationsComponent.RefreshTimeoutMs),
      finalize(() => {
        this.loading.set(false);
        this.refreshSubscription = undefined;
      })
    ).subscribe({
      next: ({ health, jobs }) => {
        this.health.set(health);
        this.jobs.set(jobs);
        this.lastRefreshedAt.set(new Date());
      },
      error: (error) => {
        console.error('Could not load operational status', error);
        this.error.set('Could not load operational status.');
      }
    });
  }

  runEvaluate(): void {
    if (this.evaluating()) {
      return;
    }

    this.evaluating.set(true);
    this.error.set('');
    this.evaluationMessage.set('Starting rule evaluation...');

    const batchSize = Math.max(1, Math.min(1000, Number(this.evaluationBatchSize) || 250));
    const reason = this.evaluationReason.trim() || 'Manual evaluation from operations dashboard';

    this.api.evaluateRules(batchSize, reason).pipe(
      timeout(OperationsComponent.EvaluationStartTimeoutMs),
      finalize(() => this.evaluating.set(false))
    ).subscribe({
      next: (result) => {
        this.evaluationMessage.set(`Started evaluation job ${result.jobId}. Processed ${result.processedCount} and changed ${result.changedCount}.`);
        this.refresh();
      },
      error: (error) => {
        console.error('Could not start rule evaluation', error);
        this.error.set('Could not start rule evaluation.');
        this.evaluationMessage.set('');
      }
    });
  }
}
