import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { RiskRule } from '../../core/models';

@Component({
  selector: 'app-risk-rule-row',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './risk-rule-row.component.html',
  styleUrls: ['./risk-rule-row.component.css']
})
export class RiskRuleRowComponent {
  @Input({ required: true }) rule!: RiskRule;
  @Input({ required: true }) draft!: RiskRule;
  @Input({ required: true }) saving = false;
  @Input({ required: true }) dirty = false;
  @Input({ required: true }) valid = false;

  @Output() readonly draftChange = new EventEmitter<RiskRule>();
  @Output() readonly saveRule = new EventEmitter<void>();
  @Output() readonly resetRule = new EventEmitter<void>();

  updateDescription(description: string): void {
    this.emitDraft({ description });
  }

  updateWeight(weight: number | string): void {
    this.emitDraft({ weight: weight === '' ? Number.NaN : Number(weight) });
  }

  updateEnabled(enabled: boolean): void {
    this.emitDraft({ enabled });
  }

  private emitDraft(update: Partial<RiskRule>): void {
    this.draftChange.emit({
      ...this.draft,
      ...update
    });
  }
}
