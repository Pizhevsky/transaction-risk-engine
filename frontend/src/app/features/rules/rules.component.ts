import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';
import type { RiskRule } from '../../core/models';
import { RiskApiService } from '../../core/risk-api.service';
import { RiskRuleRowComponent } from './risk-rule-row.component';

const MinRuleWeight = 0;
const MaxRuleWeight = 100;

@Component({
  selector: 'app-rules',
  standalone: true,
  imports: [RiskRuleRowComponent],
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
    if (!draft || !this.isDraftValid(draft) || !this.isDirty(code)) {
      return;
    }

    this.savingCode.set(code);
    this.message.set('');
    this.error.set('');
    const payload = normaliseDraft(draft);

    this.api.updateRule(code, {
      description: payload.description,
      weight: payload.weight,
      enabled: payload.enabled
    }).pipe(
      finalize(() => this.savingCode.set(null))
    ).subscribe({
      next: (response) => {
        this.rules.update((rules) => rules.map((item) => 
          item.code === response.code ? response : item
        ));
        this.drafts.update((drafts) => ({
          ...drafts,
          [response.code]: { ...response }
        }));
        this.message.set(response.message);
      },
      error: () => {
        this.error.set(`Could not save ${code}.`);
      }
    });
  }

  evaluate(): void {
    this.evaluating.set(true);
    this.message.set('');
    this.error.set('');

    this.api.evaluateRules().pipe(
      finalize(() => this.evaluating.set(false))
    ).subscribe({
      next: (result) => {
        this.message.set(`Rule evaluation job ${result.jobId} processed ${result.processedCount} transactions and changed ${result.changedCount}.`);
      },
      error: () => {
        this.error.set('Could not start rule evaluation.');
      }
    });
  }

  draftFor(code: string): RiskRule | null {
    return this.drafts()[code] ?? null;
  }

  updateDraft(code: string, draft: RiskRule): void {
    this.drafts.update((drafts) => ({
      ...drafts,
      [code]: {
        ...draft,
        code
      }
    }));
  }

  resetDraft(code: string): void {
    const rule = this.rules().find((item) => item.code === code);
    if (!rule) {
      return;
    }

    this.drafts.update((drafts) => ({
      ...drafts,
      [code]: { ...rule }
    }));
  }

  isDirty(code: string): boolean {
    const rule = this.rules().find((item) => item.code === code);
    const draft = this.drafts()[code];

    return !!rule &&
      !!draft &&
      (rule.description !== draft.description ||
        rule.weight !== draft.weight ||
        rule.enabled !== draft.enabled);
  }

  isDraftValid(draft: RiskRule): boolean {
    return draft.description.trim().length > 0 &&
      Number.isFinite(draft.weight) &&
      Number.isInteger(draft.weight) &&
      draft.weight >= MinRuleWeight &&
      draft.weight <= MaxRuleWeight;
  }
}

function normaliseDraft(rule: RiskRule): RiskRule {
  return {
    ...rule,
    description: rule.description.trim(),
    weight: Math.max(MinRuleWeight, Math.min(MaxRuleWeight, Math.round(Number(rule.weight) || 0)))
  };
}
