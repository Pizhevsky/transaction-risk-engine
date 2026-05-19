import { AsyncPipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { BehaviorSubject, catchError, combineLatest, concat, debounceTime, distinctUntilChanged, map, of, startWith, switchMap, tap } from 'rxjs';
import { RiskApiService, RiskLevelFilter, StatusFilter } from '../../core/risk-api.service';
import type { PagedResult, TransactionDetail, TransactionSummary } from '../../core/models';
import { DecisionBadgeComponent } from '../../shared/decision-badge.component';
import { RelationshipGraphComponent } from './relationship-graph/relationship-graph.component';
import { SelectedTransactionPanelComponent } from './selected-transaction-panel.component';

@Component({
  selector: 'app-review-queue',
  standalone: true,
  imports: [
    AsyncPipe,
    DecimalPipe,
    ReactiveFormsModule,
    DecisionBadgeComponent,
    RelationshipGraphComponent,
    SelectedTransactionPanelComponent
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './review-queue.component.html',
  styleUrls: ['./review-queue.component.css']
})

export class ReviewQueueComponent {
  private readonly api = inject(RiskApiService);
  private readonly page$ = new BehaviorSubject<number>(0);
  readonly pageSize = 10;
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

  private readonly selectedId$ = new BehaviorSubject<string | null>(null);
  readonly selectedId = signal<string | null>(null);
  readonly selectedDetail = toSignal(
    this.selectedId$.pipe(
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
            tap(() => this.detailLoading.set(false)),
            catchError((error) => {
              console.error('Could not load transaction detail', error);
              this.detailError.set('Could not load transaction detail.');
              this.detailLoading.set(false);
              return of(null);
            })
          )
        );
      })
    ),
    { initialValue: null as TransactionDetail | null }
  );

  readonly transactions$ = combineLatest([
    this.search.valueChanges.pipe(startWith(this.search.value), debounceTime(250), distinctUntilChanged(), tap(() => this.resetPage())),
    this.riskLevel.valueChanges.pipe(startWith(this.riskLevel.value), tap(() => this.resetPage())),
    this.status.valueChanges.pipe(startWith(this.status.value), tap(() => this.resetPage())),
    this.page$
  ]).pipe(
    tap(() => {
      this.loading.set(true);
      this.error.set('');
    }),
    switchMap(([search, riskLevel, status, page]) =>
      this.api.getTransactions({
        search,
        riskLevel,
        status,
        limit: this.pageSize,
        offset: page * this.pageSize
      }).pipe(
        tap((result) => this.updatePagination(result)),
        map((result) => result.items),
        tap((transactions) => this.autoSelectFirst(transactions)),
        catchError((error) => {
          console.error('Could not load transactions', error);
          this.error.set('Could not load transactions.');
          this.loading.set(false);
          return of([]);
        })
      )
    )
  );

  nextPage(): void {
    if (this.currentPage() < this.totalPages()) {
      this.page$.next(this.page$.value + 1);
    }
  }

  previousPage(): void {
    if (this.page$.value > 0) {
      this.page$.next(this.page$.value - 1);
    }
  }

  select(transaction: TransactionSummary): void {
    this.error.set('');
    this.graphOpen.set(false);
    this.selectedId.set(transaction.id);
    this.selectedId$.next(transaction.id);
  }

  selectFromKeyboard(event: Event, transaction: TransactionSummary): void {
    event.preventDefault();
    this.select(transaction);
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
    if (this.page$.value !== 0) {
      this.page$.next(0);
    }
  }

  private autoSelectFirst(transactions: TransactionSummary[]): void {
    const selected = this.selectedId();

    if (selected && transactions.some((transaction) => transaction.id === selected)) {
      return;
    }

    if (transactions.length === 0) {
      this.selectedId.set(null);
      this.selectedId$.next(null);
      return;
    }

    this.select(transactions[0]);
  }
}
