import { AsyncPipe, DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, finalize, of } from 'rxjs';
import type { AnalyseTransactionResponse } from '../../core/models';
import { RiskApiService } from '../../core/risk-api.service';
import { DecisionBadgeComponent } from '../../shared/decision-badge.component';

const DemoTransactionPreset = {
  userId: '11111111-1111-1111-1111-111111111111',
  amount: 1250,
  currency: 'NZD',
  merchant: 'Online Electronics Store',
  deviceFingerprint: 'device-shared-risk-001',
  cardFingerprint: 'card-shared-risk-001',
  cardLast4: '4242',
  ipAddress: '203.0.113.99',
  successful: true
} as const;

const RiskyDemoIdentifiers = [
  DemoTransactionPreset.deviceFingerprint,
  DemoTransactionPreset.cardFingerprint,
  DemoTransactionPreset.ipAddress
] as const;

@Component({
  selector: 'app-analyse-transaction',
  standalone: true,
  imports: [AsyncPipe, DatePipe, DecimalPipe, ReactiveFormsModule, RouterLink, DecisionBadgeComponent],
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
  readonly riskyDemoIdentifiers = RiskyDemoIdentifiers;

  readonly users$ = this.api.getUsers().pipe(
    catchError((error) => {
      console.error('Could not load users', error);
      return of([]);
    })
  );

  readonly form = this.fb.nonNullable.group({
    userId: [DemoTransactionPreset.userId, Validators.required],
    amount: [DemoTransactionPreset.amount, [Validators.required, Validators.min(1)]],
    currency: [DemoTransactionPreset.currency, [Validators.required, Validators.minLength(3), Validators.maxLength(3)]],
    merchant: [DemoTransactionPreset.merchant, Validators.required],
    deviceFingerprint: [DemoTransactionPreset.deviceFingerprint, Validators.required],
    cardFingerprint: [DemoTransactionPreset.cardFingerprint, Validators.required],
    cardLast4: [DemoTransactionPreset.cardLast4, [Validators.required, Validators.minLength(4), Validators.maxLength(4)]],
    ipAddress: [DemoTransactionPreset.ipAddress, Validators.required],
    successful: [DemoTransactionPreset.successful]
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
