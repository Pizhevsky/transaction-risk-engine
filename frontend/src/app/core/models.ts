export interface RiskSignal {
  code: string;
  baseScore: number;
  score: number;
  reason: string;
  evidence: string;
}

export interface TransactionSummary {
  id: string;
  userId: string;
  userName: string;
  amount: number;
  currency: string;
  merchant: string;
  successful: boolean;
  riskScore: number;
  decision: 'Approved' | 'Review' | 'Blocked';
  topReason?: string | null;
  createdAt: string;
}

export interface TransactionDetail extends TransactionSummary {
  deviceFingerprint?: string | null;
  cardFingerprint?: string | null;
  ipAddress?: string | null;
  signals: RiskSignal[];
}

export interface UserSummary {
  id: string;
  displayName: string;
  email: string;
  isFlagged: boolean;
}

export interface AnalyseTransactionRequest {
  userId: string;
  amount: number;
  currency: string;
  merchant: string;
  cardFingerprint: string;
  cardLast4: string;
  deviceFingerprint: string;
  ipAddress: string;
  successful: boolean;
}

export interface AnalyseTransactionResponse {
  transactionId: string;
  userId: string;
  userName: string;
  amount: number;
  currency: string;
  merchant: string;
  riskScore: number;
  decision: string;
  signals: RiskSignal[];
  createdAt: string;
  idempotentReplay?: boolean;
}

export interface GraphNode {
  id: string;
  label: string;
  type: string;
  isRisky: boolean;
}

export interface GraphEdge {
  id: string;
  source: string;
  target: string;
  label: string;
}

export interface GraphResponse {
  userId: string;
  nodes: GraphNode[];
  edges: GraphEdge[];
  riskPaths: string[];
}

export interface RiskRule {
  code: string;
  description: string;
  weight: number;
  enabled: boolean;
}

export interface UpdateRiskRuleRequest {
  description?: string | null;
  weight: number;
  enabled: boolean;
}

export interface RiskRuleUpdateResponse extends RiskRule {
  message: string;
}

export interface RiskEvaluationResponse {
  jobId: string;
  processedCount: number;
  changedCount: number;
}


export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  limit: number;
  offset: number;
}

export interface HealthOutboxStatus {
  pending: number;
  processing?: number;
  failed: number;
  oldestPendingAgeSeconds?: number;
}

export interface HealthStatus {
  status: string;
  database: string;
  riskRules?: number;
  outbox: HealthOutboxStatus;
}

export interface RiskEvaluationJob {
  id: string;
  reason: string;
  requestedBatchSize: number;
  processedCount: number;
  changedCount: number;
  createdAt: string;
  completedAt?: string | null;
}

export type FraudCaseStatus = 'Open' | 'Investigating' | 'ClosedApproved' | 'ClosedBlocked';

export interface FraudCase {
  id: string;
  transactionId: string;
  userName: string;
  amount: number;
  currency: string;
  riskScore: number;
  status: FraudCaseStatus;
  summary: string;
  reviewNote?: string | null;
  createdAt: string;
  closedAt?: string | null;
}

export interface UpdateFraudCaseStatusRequest {
  status: FraudCaseStatus;
  note?: string | null;
}

export type FraudCaseStatusFilter = 'all' | 'open' | 'investigating' | 'closed' | 'closed-approved' | 'closed-blocked';
