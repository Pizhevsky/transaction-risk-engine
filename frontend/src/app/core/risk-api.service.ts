import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL, API_ROOT_URL } from './api.config';
import type {
  AnalyseTransactionRequest,
  AnalyseTransactionResponse,
  FraudCase,
  FraudCaseStatusFilter,
  GraphResponse,
  HealthStatus,
  PagedResult,
  RiskRule,
  RiskRuleUpdateResponse,
  RiskEvaluationJob,
  RiskEvaluationResponse,
  UpdateFraudCaseStatusRequest,
  UpdateRiskRuleRequest,
  TransactionDetail,
  TransactionSummary,
  UserSummary
} from './models';

export type RiskLevelFilter = 'all' | 'approved' | 'review' | 'blocked';
export type StatusFilter = 'all' | 'success' | 'failed';

@Injectable({ providedIn: 'root' })
export class RiskApiService {
  private readonly http = inject(HttpClient);

  getTransactions(filters: {
    search: string;
    riskLevel: RiskLevelFilter;
    status: StatusFilter;
    limit: number;
    offset: number;
  }): Observable<PagedResult<TransactionSummary>> {
    let params = new HttpParams()
      .set('limit', filters.limit)
      .set('offset', filters.offset);

    if (filters.search.trim()) {
      params = params.set('search', filters.search.trim());
    }

    if (filters.riskLevel !== 'all') {
      params = params.set('riskLevel', filters.riskLevel);
    }

    if (filters.status !== 'all') {
      params = params.set('status', filters.status);
    }

    return this.http.get<TransactionSummary[]>(`${API_BASE_URL}/transactions`, {
      params,
      observe: 'response'
    }).pipe(
      map((response) => toPagedResult(response, filters))
    );
  }

  getTransaction(id: string): Observable<TransactionDetail> {
    return this.http.get<TransactionDetail>(`${API_BASE_URL}/transactions/${id}`);
  }

  analyseTransaction(payload: AnalyseTransactionRequest): Observable<AnalyseTransactionResponse> {
    return this.http.post<AnalyseTransactionResponse>(`${API_BASE_URL}/transactions/analyse`, payload, {
      observe: 'response'
    }).pipe(
      map((response) => ({
        ...requireResponseBody(response, 'analyse transaction'),
        idempotentReplay: response.headers.get('X-Idempotent-Replay') === 'true'
      }))
    );
  }

  getUsers(): Observable<UserSummary[]> {
    return this.http.get<UserSummary[]>(`${API_BASE_URL}/users`);
  }

  getConnections(userId: string): Observable<GraphResponse> {
    return this.http.get<GraphResponse>(`${API_BASE_URL}/users/${userId}/connections`);
  }

  getTransactionConnections(transactionId: string): Observable<GraphResponse> {
    return this.http.get<GraphResponse>(`${API_BASE_URL}/transactions/${transactionId}/connections`);
  }

  getRules(): Observable<RiskRule[]> {
    return this.http.get<RiskRule[]>(`${API_BASE_URL}/rules`);
  }

  updateRule(code: string, payload: UpdateRiskRuleRequest): Observable<RiskRuleUpdateResponse> {
    return this.http.put<RiskRuleUpdateResponse>(`${API_BASE_URL}/rules/${encodeURIComponent(code)}`, payload);
  }

  evaluateRules(
    batchSize = 250,
    reason = 'Manual evaluation from analyst dashboard'
  ): Observable<RiskEvaluationResponse> {
    return this.http.post<RiskEvaluationResponse>(`${API_BASE_URL}/rules/evaluate`, {
      batchSize,
      reason: reason.trim() || 'Manual evaluation from analyst dashboard'
    });
  }

  getEvaluationJobs(): Observable<RiskEvaluationJob[]> {
    return this.http.get<RiskEvaluationJob[]>(`${API_BASE_URL}/rules/evaluation-jobs`);
  }

  getFraudCases(filters: {
    search: string;
    status: FraudCaseStatusFilter;
    limit: number;
    offset: number;
  }): Observable<PagedResult<FraudCase>> {
    let params = new HttpParams()
      .set('limit', filters.limit)
      .set('offset', filters.offset);

    if (filters.search.trim()) {
      params = params.set('search', filters.search.trim());
    }

    if (filters.status !== 'all') {
      params = params.set('status', filters.status);
    }

    return this.http.get<FraudCase[]>(`${API_BASE_URL}/fraud-cases`, {
      params,
      observe: 'response'
    }).pipe(
      map((response) => toPagedResult(response, filters))
    );
  }

  updateFraudCaseStatus(
    id: string,
    payload: UpdateFraudCaseStatusRequest
  ): Observable<FraudCase> {
    return this.http.patch<FraudCase>(`${API_BASE_URL}/fraud-cases/${encodeURIComponent(id)}/status`, payload);
  }

  getHealthStatus(): Observable<HealthStatus> {
    return this.http.get<HealthStatus>(`${API_ROOT_URL}/health/status`);
  }
}

function toPagedResult<T>(
  response: HttpResponse<T[]>,
  fallback: Pick<PagedResult<T>, 'limit' | 'offset'>
): PagedResult<T> {
  return {
    items: response.body ?? [],
    totalCount: parseHeaderNumber(response, 'X-Total-Count', 0),
    limit: parseHeaderNumber(response, 'X-Limit', fallback.limit),
    offset: parseHeaderNumber(response, 'X-Offset', fallback.offset)
  };
}

function parseHeaderNumber(
  response: HttpResponse<unknown>,
  headerName: string,
  fallback: number
): number {
  const value = Number(response.headers.get(headerName));
  return Number.isFinite(value) ? value : fallback;
}

function requireResponseBody<T>(response: HttpResponse<T>, action: string): T {
  if (response.body === null) {
    throw new Error(`API returned an empty response while trying to ${action}.`);
  }

  return response.body;
}
