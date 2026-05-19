import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  SimpleChanges,
  ViewChild,
  inject,
  signal
} from '@angular/core';
import { Subscription } from 'rxjs';
import type { Core } from 'cytoscape';
import { RiskApiService } from '../../../core/risk-api.service';
import { renderRelationshipGraph } from './relationship-graph.renderer';

@Component({
  selector: 'app-relationship-graph',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './relationship-graph.component.html',
  styleUrls: ['./relationship-graph.component.css']
})

export class RelationshipGraphComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input({ required: true }) transactionId!: string;
  @Input() userName = '';
  @ViewChild('graphContainer') graphContainer!: ElementRef<HTMLDivElement>;

  private readonly api = inject(RiskApiService);
  private cy?: Core;
  private viewReady = false;
  private graphSubscription?: Subscription;

  readonly loading = signal(false);
  readonly error = signal('');
  readonly riskPaths = signal<string[]>([]);

  ngAfterViewInit(): void {
    this.viewReady = true;
    queueMicrotask(() => this.loadGraph());
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['transactionId'] && this.viewReady) {
      this.loadGraph();
    }
  }

  ngOnDestroy(): void {
    this.graphSubscription?.unsubscribe();
    this.cy?.destroy();
  }

  retry(): void {
    this.loadGraph();
  }

  private loadGraph(): void {
    if (!this.transactionId) {
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.riskPaths.set([]);
    this.graphSubscription?.unsubscribe();
    this.cy?.destroy();
    this.cy = undefined;
    this.graphContainer.nativeElement.replaceChildren();

    this.graphSubscription = this.api.getTransactionConnections(this.transactionId).subscribe({
      next: (graph) => {
        this.loading.set(false);
        this.riskPaths.set(graph.riskPaths);
        this.cy = renderRelationshipGraph(this.graphContainer.nativeElement, graph);
      },
      error: (error) => {
        console.error('Could not load graph', error);
        this.loading.set(false);
        this.error.set('Could not load relationship graph.');
      }
    });
  }
}
