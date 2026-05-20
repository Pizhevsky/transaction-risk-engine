import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import type { TransactionSummary } from '../../core/models';
import { DecisionBadgeComponent } from '../../shared/decision-badge.component';

@Component({
  selector: 'app-review-queue-table',
  standalone: true,
  imports: [DecimalPipe, DecisionBadgeComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './review-queue-table.component.html',
  styleUrls: ['./review-queue-table.component.css']
})

export class ReviewQueueTableComponent {
  @Input({ required: true }) transactions: TransactionSummary[] = [];
  @Input({ required: true }) loading = false;
  @Input({ required: true }) error = '';
  @Input({ required: true }) totalCount = 0;
  @Input({ required: true }) visibleStart = 0;
  @Input({ required: true }) visibleEnd = 0;
  @Input({ required: true }) selectedId: string | null = null;
  @Input({ required: true }) currentPage = 1;
  @Input({ required: true }) totalPages = 1;

  @Output() readonly selectTransaction = new EventEmitter<TransactionSummary>();
  @Output() readonly previousPage = new EventEmitter<void>();
  @Output() readonly nextPage = new EventEmitter<void>();

  selectFromKeyboard(event: Event, transaction: TransactionSummary): void {
    event.preventDefault();
    this.selectTransaction.emit(transaction);
  }
}
