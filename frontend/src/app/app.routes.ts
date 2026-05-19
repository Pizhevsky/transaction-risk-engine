import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'review' },
  {
    path: 'review',
    data: { menuLabel: 'Review queue', menuOrder: 10 },
    loadComponent: () => import('./features/review-queue/review-queue.component')
      .then((m) => m.ReviewQueueComponent)
  },
  {
    path: 'analyse',
    data: { menuLabel: 'Analyse transaction', menuOrder: 20 },
    loadComponent: () => import('./features/analyse-transaction/analyse-transaction.component')
      .then((m) => m.AnalyseTransactionComponent)
  },
  {
    path: 'rules',
    data: { menuLabel: 'Risk rules', menuOrder: 30 },
    loadComponent: () => import('./features/rules/rules.component')
      .then((m) => m.RulesComponent)
  },
  {
    path: 'operations',
    data: { menuLabel: 'Operations', menuOrder: 40 },
    loadComponent: () => import('./features/operations/operations.component')
      .then((m) => m.OperationsComponent)
  },
  { path: '**', redirectTo: 'review' }
];
