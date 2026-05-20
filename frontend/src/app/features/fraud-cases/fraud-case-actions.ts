import type { FraudCase, FraudCaseStatus } from '../../core/models';

export interface FraudCaseAction {
  readonly status: FraudCaseStatus;
  readonly label: string;
  readonly style: 'primary' | 'secondary' | 'danger';
}

const ActionsByStatus: Record<FraudCaseStatus, readonly FraudCaseAction[]> = {
  Open: [
    { status: 'Investigating', label: 'Start investigating', style: 'primary' },
    { status: 'ClosedApproved', label: 'Close approved', style: 'secondary' },
    { status: 'ClosedBlocked', label: 'Close blocked', style: 'danger' }
  ],
  Investigating: [
    { status: 'Open', label: 'Return to open', style: 'secondary' },
    { status: 'ClosedApproved', label: 'Close approved', style: 'secondary' },
    { status: 'ClosedBlocked', label: 'Close blocked', style: 'danger' }
  ],
  ClosedApproved: [
    { status: 'Investigating', label: 'Reopen investigation', style: 'primary' }
  ],
  ClosedBlocked: [
    { status: 'Investigating', label: 'Reopen investigation', style: 'primary' }
  ]
};

export function actionsForFraudCase(item: FraudCase): readonly FraudCaseAction[] {
  return ActionsByStatus[item.status] ?? [];
}
