import { ChangeDetectionStrategy, Component, EventEmitter, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import type { FraudCaseStatusFilter } from '../../core/models';

@Component({
  selector: 'app-fraud-case-filters',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './fraud-case-filters.component.html',
  styleUrls: ['./fraud-case-filters.component.css']
})
export class FraudCaseFiltersComponent {
  @Input() search = '';
  @Input() status: FraudCaseStatusFilter = 'all';
  @Input() loading = false;

  @Output() readonly searchChange = new EventEmitter<string>();
  @Output() readonly statusChange = new EventEmitter<FraudCaseStatusFilter>();
  @Output() readonly applyFilters = new EventEmitter<void>();
}
