import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import type { FraudCase, FraudCaseStatus } from '../../core/models';
import { actionsForFraudCase } from './fraud-case-actions';

@Component({
  selector: 'app-fraud-case-card',
  standalone: true,
  imports: [DatePipe, DecimalPipe, FormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './fraud-case-card.component.html',
  styleUrls: ['./fraud-case-card.component.css']
})
export class FraudCaseCardComponent {
  @Input({ required: true }) item!: FraudCase;
  @Input() note = '';
  @Input() pendingStatus: FraudCaseStatus | null = null;
  @Input() feedback = '';
  @Input() feedbackTone: 'success' | 'error' = 'success';

  @Output() readonly noteChange = new EventEmitter<string>();
  @Output() readonly statusChange = new EventEmitter<FraudCaseStatus>();

  readonly actionsForFraudCase = actionsForFraudCase;

  handleActionClick(status: FraudCaseStatus): void {
    if (this.isUpdating()) {
      return;
    }

    this.statusChange.emit(status);
  }

  isUpdating(): boolean {
    return this.pendingStatus !== null;
  }

  isActionUpdating(status: FraudCaseStatus): boolean {
    return this.pendingStatus === status;
  }

  allowsActionNote(): boolean {
    return this.item.status === 'Open' || this.item.status === 'Investigating';
  }
}
