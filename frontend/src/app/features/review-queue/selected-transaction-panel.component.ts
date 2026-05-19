import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import type { TransactionDetail } from '../../core/models';
import { DecisionBadgeComponent } from '../../shared/decision-badge.component';

@Component({
  selector: 'app-selected-transaction-panel',
  standalone: true,
  imports: [
    DatePipe,
    DecimalPipe,
    DecisionBadgeComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './selected-transaction-panel.component.html',
  styleUrls: ['./selected-transaction-panel.component.css']
})

export class SelectedTransactionPanelComponent {
  @Input() detail: TransactionDetail | null = null;
  @Input() loading = false;
  @Input() error = '';
  @Output() viewGraph = new EventEmitter<void>();
}
