import { AsyncPipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { catchError, finalize, of } from 'rxjs';
import type { AnalyseTransactionResponse } from '../../core/models';
import { RiskApiService } from '../../core/risk-api.service';
import { DecisionBadgeComponent } from '../../shared/decision-badge.component';

@Component({
  selector: 'app-analyse-transaction',
  standalone: true,
  imports: [AsyncPipe, DecimalPipe, ReactiveFormsModule, DecisionBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './analyse-transaction.component.html',
  styleUrls: ['./analyse-transaction.component.css']
})

export class AnalyseTransactionComponent {
  private readonly api = inject(RiskApiService);
  private readonly fb = inject(FormBuilder);

  readonly result = signal<AnalyseTransactionResponse | null>(null);
  readonly analysing = signal(false);
  readonly errorMessage = signal('');

  readonly users$ = this.api.getUsers().pipe(
    catchError((error) => {
      console.error('Could not load users', error);
      return of([]);
    })
  );

  readonly form = this.fb.nonNullable.group({
    userId: ['11111111-1111-1111-1111-111111111111', Validators.required],
    amount: [1250, [Validators.required, Validators.min(1)]],
    currency: ['NZD', [Validators.required, Validators.minLength(3), Validators.maxLength(3)]],
    merchant: ['Online Electronics Store', Validators.required],
    deviceFingerprint: ['device-shared-risk-001', Validators.required],
    cardFingerprint: ['card-shared-risk-001', Validators.required],
    cardLast4: ['4242', [Validators.required, Validators.minLength(4), Validators.maxLength(4)]],
    ipAddress: ['203.0.113.99', Validators.required],
    successful: [true]
  });

  submit(): void {
    if (this.form.invalid) {
      return;
    }

    this.analysing.set(true);
    this.errorMessage.set('');
    this.result.set(null);

    this.api.analyseTransaction(this.form.getRawValue())
      .pipe(finalize(() => this.analysing.set(false)))
      .subscribe({
        next: (result) => this.result.set(result),
        error: (error) => {
          console.error('Could not analyse transaction', error);
          this.errorMessage.set('Could not analyse transaction.');
        }
      });
  }
}
