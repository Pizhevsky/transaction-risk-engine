import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormControl } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { catchError, concat, debounceTime, distinctUntilChanged, finalize, map, of, startWith, switchMap, tap } from 'rxjs';
import { RiskApiService } from '../../core/risk-api.service';
import type { RiskLevelFilter, StatusFilter } from '../../core/risk-api.service';
import type { PagedResult, TransactionDetail, TransactionSummary } from '../../core/models';
import { RelationshipGraphComponent } from './relationship-graph/relationship-graph.component';
import { ReviewQueueFiltersComponent } from './review-queue-filters.component';
import { ReviewQueueTableComponent } from './review-queue-table.component';
import { SelectedTransactionPanelComponent } from './selected-transaction-panel.component';

interface ReviewQueueQuery {
  search: string;
  riskLevel: RiskLevelFilter;
  status: StatusFilter;
  page: number;
}

@Component({
  selector: 'app-review-queue',
  standalone: true,
  imports: [
    RelationshipGraphComponent,
    ReviewQueueFiltersComponent,
    ReviewQueueTableComponent,
    SelectedTransactionPanelComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './review-queue.component.html',
  styleUrls: ['./review-queue.component.css']
})

export class ReviewQueueComponent {
  private readonly api = inject(RiskApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly deepLinkedSelectedId = signal<string | null>(null);
  readonly pageSize = 10;
  readonly page = signal(0);
  readonly loading = signal(false);
  readonly detailLoading = signal(false);
  readonly error = signal('');
  readonly detailError = signal('');
  readonly graphOpen = signal(false);
  readonly totalCount = signal(0);
  readonly currentOffset = signal(0);
  readonly currentPage = computed(() => Math.floor(this.currentOffset() / this.pageSize) + 1);
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));
  readonly visibleStart = computed(() => this.totalCount() === 0 ? 0 : this.currentOffset() + 1);
  readonly visibleEnd = computed(() => Math.min(this.currentOffset() + this.pageSize, this.totalCount()));

  readonly search = new FormControl('', { nonNullable: true });
  readonly riskLevel = new FormControl<RiskLevelFilter>('all', { nonNullable: true });
  readonly status = new FormControl<StatusFilter>('all', { nonNullable: true });

  readonly selectedId = signal<string | null>(null);
  private readonly searchValue = toSignal(
    this.search.valueChanges.pipe(
      startWith(this.search.value),
      debounceTime(250),
      distinctUntilChanged(),
      tap(() => this.resetPage())
    ),
    { initialValue: this.search.value }
  );
  private readonly riskLevelValue = toSignal(
    this.riskLevel.valueChanges.pipe(
      startWith(this.riskLevel.value),
      distinctUntilChanged(),
      tap(() => this.resetPage())
    ),
    { initialValue: this.riskLevel.value }
  );
  private readonly statusValue = toSignal(
    this.status.valueChanges.pipe(
      startWith(this.status.value),
      distinctUntilChanged(),
      tap(() => this.resetPage())
    ),
    { initialValue: this.status.value }
  );
  private readonly query = computed<ReviewQueueQuery>(() => ({
    search: this.searchValue(),
    riskLevel: this.riskLevelValue(),
    status: this.statusValue(),
    page: this.page()
  }));

  readonly selectedDetail = toSignal(
    toObservable(this.selectedId).pipe(
      distinctUntilChanged(),
      switchMap((id) => {
        this.detailError.set('');

        if (!id) {
          this.detailLoading.set(false);
          return of(null);
        }

        this.detailLoading.set(true);
        return concat(
          of(null),
          this.api.getTransaction(id).pipe(
            catchError((error) => {
              console.error('Could not load transaction detail', error);
              this.detailError.set('Could not load transaction detail.');
              return of(null);
            })
          )
        ).pipe(
          finalize(() => this.detailLoading.set(false))
        );
      })
    ),
    { initialValue: null as TransactionDetail | null }
  );

  readonly transactions = toSignal(
    toObservable(this.query).pipe(
      tap(() => {
        this.loading.set(true);
        this.error.set('');
      }),
      switchMap((query) =>
        this.api.getTransactions({
          search: query.search,
          riskLevel: query.riskLevel,
          status: query.status,
          limit: this.pageSize,
          offset: query.page * this.pageSize
        }).pipe(
          tap((result) => this.updatePagination(result)),
          map((result) => result.items),
          tap((transactions) => this.autoSelectFirst(transactions)),
          catchError((error) => {
            console.error('Could not load transactions', error);
            this.totalCount.set(0);
            this.currentOffset.set(query.page * this.pageSize);
            this.error.set('Could not load transactions.');
            return of([]);
          }),
          finalize(() => this.loading.set(false))
        )
      )
    ),
    { initialValue: [] as TransactionSummary[] }
  );

  constructor() {
    this.route.queryParamMap.pipe(
      map((params) => params.get('transactionId')),
      distinctUntilChanged(),
      takeUntilDestroyed()
    ).subscribe((transactionId) => {
      this.deepLinkedSelectedId.set(transactionId);

      if (transactionId) {
        this.selectById(transactionId);
      }
    });
  }

  nextPage(): void {
    if (this.currentPage() < this.totalPages()) {
      this.page.update((page) => page + 1);
    }
  }

  previousPage(): void {
    if (this.page() > 0) {
      this.page.update((page) => page - 1);
    }
  }

  select(transaction: TransactionSummary): void {
    this.deepLinkedSelectedId.set(null);
    this.selectById(transaction.id);
  }

  private selectById(id: string): void {
    this.error.set('');
    this.graphOpen.set(false);
    this.selectedId.set(id);
  }

  openGraph(): void {
    if (this.selectedDetail()) {
      this.graphOpen.set(true);
    }
  }

  closeGraph(): void {
    this.graphOpen.set(false);
  }

  private updatePagination(result: PagedResult<TransactionSummary>): void {
    this.totalCount.set(result.totalCount);
    this.currentOffset.set(result.offset);
    this.loading.set(false);
  }

  private resetPage(): void {
    if (this.page() !== 0) {
      this.page.set(0);
    }
  }

  private autoSelectFirst(transactions: TransactionSummary[]): void {
    const selected = this.selectedId();

    if (selected && (
      selected === this.deepLinkedSelectedId() ||
      transactions.some((transaction) => transaction.id === selected)
    )) {
      return;
    }

    if (transactions.length === 0) {
      this.selectedId.set(null);
      return;
    }

    this.select(transactions[0]);
  }
}
