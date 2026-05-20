import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { catchError, finalize, of, switchMap, tap } from 'rxjs';
import type { FraudCase, FraudCaseStatus, FraudCaseStatusFilter, PagedResult } from '../../core/models';
import { RiskApiService } from '../../core/risk-api.service';
import { FraudCaseCardComponent } from './fraud-case-card.component';
import { FraudCaseFiltersComponent } from './fraud-case-filters.component';

interface CaseActionFeedback {
  message: string;
  tone: 'success' | 'error';
}

const EmptyFeedback: CaseActionFeedback = { message: '', tone: 'success' };
const PageSize = 5;
const EmptyPage: PagedResult<FraudCase> = {
  items: [],
  totalCount: 0,
  limit: PageSize,
  offset: 0
};

@Component({
  selector: 'app-fraud-cases',
  standalone: true,
  imports: [FraudCaseCardComponent, FraudCaseFiltersComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './fraud-cases.component.html',
  styleUrls: ['./fraud-cases.component.css']
})
export class FraudCasesComponent {
  private readonly api = inject(RiskApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly search = signal('');
  readonly status = signal<FraudCaseStatusFilter>('all');
  readonly limit = signal(PageSize);
  readonly offset = signal(0);
  readonly loading = signal(false);
  readonly pendingStatusByCaseId = signal<Readonly<Record<string, FraudCaseStatus>>>({});
  readonly caseFeedback = signal<Readonly<Record<string, CaseActionFeedback>>>({});
  readonly statusNotes = signal<Readonly<Record<string, string>>>({});
  readonly updatedCasesById = signal<Readonly<Record<string, FraudCase>>>({});
  readonly hiddenCaseIds = signal<ReadonlySet<string>>(new Set<string>());
  readonly error = signal('');

  private readonly appliedSearch = signal('');
  private readonly appliedStatus = signal<FraudCaseStatusFilter>('all');
  private readonly refreshKey = signal(0);
  private readonly query = computed(() => ({
    search: this.appliedSearch().trim(),
    status: this.appliedStatus(),
    limit: this.limit(),
    offset: this.offset(),
    refreshKey: this.refreshKey()
  }));

  private readonly page = toSignal(
    toObservable(this.query).pipe(
      tap(() => {
        this.error.set('');
        this.updatedCasesById.set({});
        this.hiddenCaseIds.set(new Set<string>());
      }),
      switchMap((query) => {
        this.loading.set(true);

        return this.api.getFraudCases({
          search: query.search,
          status: query.status,
          limit: PageSize,
          offset: query.offset
        }).pipe(
          tap((result) => {
            this.limit.set(Math.min(result.limit, PageSize));
            this.offset.set(result.offset);
          }),
          catchError((error) => {
            console.error('Could not load fraud cases', error);
            this.error.set('Could not load fraud cases.');
            return of({
              ...EmptyPage,
              limit: query.limit,
              offset: query.offset
            });
          }),
          finalize(() => this.loading.set(false))
        )
      })
    ),
    { initialValue: EmptyPage }
  );

  readonly cases = computed(() => {
    const hidden = this.hiddenCaseIds();
    const updates = this.updatedCasesById();

    return this.page().items
      .map((item) => updates[item.id] ?? item)
      .filter((item) => !hidden.has(item.id));
  });

  readonly totalCount = computed(() =>
    Math.max(0, this.page().totalCount - this.hiddenCaseIds().size)
  );

  readonly rangeText = computed(() => {
    if (this.totalCount() === 0) {
      return '0 cases';
    }

    return `${this.offset() + 1}-${this.offset() + this.cases().length} of ${this.totalCount()}`;
  });

  applyFilters(): void {
    this.appliedSearch.set(this.search());
    this.appliedStatus.set(this.status());
    this.offset.set(0);
    this.refresh();
  }

  previousPage(): void {
    this.offset.update((offset) => Math.max(0, offset - this.limit()));
  }

  nextPage(): void {
    if (this.offset() + this.limit() >= this.totalCount()) {
      return;
    }

    this.offset.update((offset) => offset + this.limit());
  }

  updateStatus(item: FraudCase, status: FraudCaseStatus): void {
    if (this.isUpdating(item)) {
      return;
    }

    this.setPendingStatus(item.id, status);
    this.clearCaseFeedback(item.id);

    const note = this.allowsActionNote(item)
      ? this.noteFor(item).trim()
      : null;

    this.api.updateFraudCaseStatus(item.id, { status, note }).pipe(
      finalize(() => this.setPendingStatus(item.id, null)),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe({
      next: (updated) => {
        this.clearNote(item.id);
        this.applyUpdatedCase(updated);
      },
      error: (error) => {
        console.error('Could not update fraud case status', error);
        this.setCaseFeedback(item.id, 'Could not update status.', 'error');
      }
    });
  }

  pendingStatusFor(item: FraudCase): FraudCaseStatus | null {
    return this.pendingStatusByCaseId()[item.id] ?? null;
  }

  isUpdating(item: FraudCase): boolean {
    return this.pendingStatusFor(item) !== null;
  }

  updateNote(caseId: string, note: string): void {
    this.statusNotes.update((current) => ({
      ...current,
      [caseId]: note
    }));
  }

  noteFor(item: FraudCase): string {
    return this.statusNotes()[item.id] ?? item.reviewNote ?? '';
  }

  feedbackFor(caseId: string): CaseActionFeedback {
    return this.caseFeedback()[caseId] ?? EmptyFeedback;
  }

  private setPendingStatus(caseId: string, status: FraudCaseStatus | null): void {
    this.pendingStatusByCaseId.update((current) => {
      const next: Record<string, FraudCaseStatus> = { ...current };

      if (status === null) {
        delete next[caseId];
      } else {
        next[caseId] = status;
      }

      return next;
    });
  }

  private clearCaseFeedback(caseId: string): void {
    this.caseFeedback.update((current) => {
      if (!current[caseId]) {
        return current;
      }

      const next = { ...current };
      delete next[caseId];
      return next;
    });
  }

  private setCaseFeedback(
    caseId: string,
    message: string,
    tone: CaseActionFeedback['tone']
  ): void {
    this.caseFeedback.update((current) => ({
      ...current,
      [caseId]: { message, tone }
    }));
  }

  private clearNote(caseId: string): void {
    this.statusNotes.update((current) => {
      if (!current[caseId]) {
        return current;
      }

      const next = { ...current };
      delete next[caseId];
      return next;
    });
  }

  private applyUpdatedCase(updated: FraudCase): void {
    if (!this.matchesCurrentFilters(updated)) {
      this.hiddenCaseIds.update((current) => new Set(current).add(updated.id));
      return;
    }

    this.updatedCasesById.update((current) => ({
      ...current,
      [updated.id]: updated
    }));
    this.setCaseFeedback(updated.id, `Moved to ${updated.status}.`, 'success');
  }

  private matchesCurrentFilters(item: FraudCase): boolean {
    return this.matchesCurrentStatus(item.status) &&
      this.matchesCurrentSearch(item);
  }

  private matchesCurrentStatus(status: FraudCaseStatus): boolean {
    switch (this.appliedStatus()) {
      case 'all':
        return true;
      case 'open':
        return status === 'Open';
      case 'investigating':
        return status === 'Investigating';
      case 'closed':
        return status === 'ClosedApproved' || status === 'ClosedBlocked';
      case 'closed-approved':
        return status === 'ClosedApproved';
      case 'closed-blocked':
        return status === 'ClosedBlocked';
    }

    return true;
  }

  private matchesCurrentSearch(item: FraudCase): boolean {
    const search = this.appliedSearch().trim().toLocaleLowerCase();

    if (!search) {
      return true;
    }

    return item.userName.toLocaleLowerCase().includes(search) ||
      item.summary.toLocaleLowerCase().includes(search);
  }

  private allowsActionNote(item: FraudCase): boolean {
    return item.status === 'Open' || item.status === 'Investigating';
  }

  private refresh(): void {
    this.refreshKey.update((value) => value + 1);
  }
}
