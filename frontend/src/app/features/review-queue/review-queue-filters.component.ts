import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import type { RiskLevelFilter, StatusFilter } from '../../core/risk-api.service';

@Component({
  selector: 'app-review-queue-filters',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './review-queue-filters.component.html',
  styleUrls: ['./review-queue-filters.component.css']
})

export class ReviewQueueFiltersComponent {
  @Input({ required: true }) search = '';
  @Input({ required: true }) riskLevel: RiskLevelFilter = 'all';
  @Input({ required: true }) status: StatusFilter = 'all';

  @Output() readonly searchChange = new EventEmitter<string>();
  @Output() readonly riskLevelChange = new EventEmitter<RiskLevelFilter>();
  @Output() readonly statusChange = new EventEmitter<StatusFilter>();

  protected onSearchInput(event: Event): void {
    this.searchChange.emit((event.target as HTMLInputElement).value);
  }

  protected onRiskLevelChange(event: Event): void {
    this.riskLevelChange.emit((event.target as HTMLSelectElement).value as RiskLevelFilter);
  }

  protected onStatusChange(event: Event): void {
    this.statusChange.emit((event.target as HTMLSelectElement).value as StatusFilter);
  }
}
