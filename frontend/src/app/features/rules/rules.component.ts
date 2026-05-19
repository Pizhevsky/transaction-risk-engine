import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import type { RiskRule } from '../../core/models';
import { RiskApiService } from '../../core/risk-api.service';

@Component({
  selector: 'app-rules',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './rules.component.html',
  styleUrls: ['./rules.component.css']
})

export class RulesComponent implements OnInit {
  private readonly api = inject(RiskApiService);

  readonly rules = signal<RiskRule[]>([]);
  readonly drafts = signal<Record<string, RiskRule>>({});
  readonly loading = signal(false);
  readonly savingCode = signal<string | null>(null);
  readonly evaluating = signal(false);
  readonly message = signal('');
  readonly error = signal('');

  ngOnInit(): void {
    this.loadRules();
  }

  loadRules(): void {
    this.loading.set(true);
    this.error.set('');

    this.api.getRules().pipe(
      finalize(() => this.loading.set(false))
    ).subscribe({
      next: (rules) => {
        if (!Array.isArray(rules)) {
          this.error.set('Risk rules response was not valid.');
          this.rules.set([]);
          this.drafts.set({});
          return;
        }

        this.rules.set(rules);
        this.drafts.set(Object.fromEntries(rules.map((rule) => [rule.code, { ...rule }])));
      },
      error: () => {
        this.error.set('Could not load risk rules.');
      }
    });
  }

  save(code: string): void {
    const draft = this.drafts()[code];
    if (!draft) {
      return;
    }

    this.savingCode.set(code);
    this.message.set('');
    this.error.set('');

    this.api.updateRule(code, {
      description: draft.description,
      weight: Number(draft.weight),
      enabled: draft.enabled
    }).subscribe({
      next: (response) => {
        this.rules.update((rules) => rules.map((item) => 
          item.code === response.code ? response : item
        ));
        this.drafts.update((drafts) => ({
          ...drafts,
          [response.code]: { ...response }
        }));
        this.message.set(response.message);
        this.savingCode.set(null);
      },
      error: () => {
        this.error.set(`Could not save ${code}.`);
        this.savingCode.set(null);
      }
    });
  }

  evaluate(): void {
    this.evaluating.set(true);
    this.message.set('');
    this.error.set('');

    this.api.evaluateRules().subscribe({
      next: (result) => {
        this.message.set(`Rule evaluation job ${result.jobId} processed ${result.processedCount} transactions and changed ${result.changedCount}.`);
        this.evaluating.set(false);
      },
      error: () => {
        this.error.set('Could not start rule evaluation.');
        this.evaluating.set(false);
      }
    });
  }
}
