import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { API_BASE_URL, API_ROOT_URL } from './api.config';
import type {
  AnalyseTransactionRequest,
  AnalyseTransactionResponse,
  GraphResponse,
  HealthStatus,
  PagedResult,
  RiskRule,
  RiskRuleUpdateResponse,
  RiskEvaluationJob,
  RiskEvaluationResponse,
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
      map((response) => ({
        items: response.body ?? [],
        totalCount: Number(response.headers.get('X-Total-Count') ?? 0),
        limit: Number(response.headers.get('X-Limit') ?? filters.limit),
        offset: Number(response.headers.get('X-Offset') ?? filters.offset)
      }))
    );
  }

  getTransaction(id: string): Observable<TransactionDetail> {
    return this.http.get<TransactionDetail>(`${API_BASE_URL}/transactions/${id}`);
  }

  analyseTransaction(payload: AnalyseTransactionRequest): Observable<AnalyseTransactionResponse> {
    return this.http.post<AnalyseTransactionResponse>(`${API_BASE_URL}/transactions/analyse`, payload);
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

  getHealthStatus(): Observable<HealthStatus> {
    return this.http.get<HealthStatus>(`${API_ROOT_URL}/health/status`);
  }
}
