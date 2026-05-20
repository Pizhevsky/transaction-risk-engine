import cytoscape from 'cytoscape';
import type { Core, ElementDefinition, LayoutOptions, Stylesheet } from 'cytoscape';
import type { GraphResponse } from '../../../core/models';

export function renderRelationshipGraph(container: HTMLElement, graph: GraphResponse): Core {
  const focusedNodeId = `U:${graph.userId}`;
  const cy = cytoscape({
    container,
    elements: toElements(graph, focusedNodeId),
    style: graphStyle,
    layout: {
      ...graphLayout,
      roots: `[id = "${focusedNodeId}"]`
    } as unknown as LayoutOptions
  });

  return cy;
}

function toElements(graph: GraphResponse, focusedNodeId: string): ElementDefinition[] {
  return [
    ...graph.nodes.map((node) => ({
      data: {
        id: node.id,
        label: node.label,
        type: node.type,
        risky: node.isRisky,
        focused: node.id === focusedNodeId
      }
    })),
    ...graph.edges.map((edge) => ({
      data: {
        id: edge.id,
        source: edge.source,
        target: edge.target,
        label: edge.label
      }
    }))
  ];
}

const graphStyle: Stylesheet[] = [
  {
    selector: 'node',
    style: {
      label: 'data(label)',
      'font-size': '10px',
      'text-wrap': 'wrap',
      'text-max-width': '100px',
      'text-valign': 'bottom',
      'text-halign': 'center',
      'text-margin-y': 8,
      'text-background-color': '#ffffff',
      'text-background-opacity': 0.82,
      'text-background-padding': '3px',
      'background-color': '#60a5fa',
      color: '#0f172a',
      width: '42px',
      height: '42px'
    }
  },
  {
    selector: 'node[type = "Device"], node[type = "Card"], node[type = "IP"]',
    style: {
      'background-color': '#0f9f8f'
    }
  },
  {
    selector: 'node[?risky]',
    style: {
      'background-color': '#dc2626',
      color: '#7f1d1d'
    }
  },
  {
    selector: 'node[?focused]',
    style: {
      label: 'data(label)',
      'background-color': '#073f87',
      'border-width': '4px',
      'border-color': '#38bdf8',
      width: '56px',
      height: '56px',
      'font-weight': 700,
      'text-margin-y': 10,
      'text-max-width': '120px'
    }
  },
  {
    selector: 'node[?risky][?focused]',
    style: {
      'border-color': '#dc2626',
      color: '#0f172a'
    }
  },
  {
    selector: 'edge',
    style: {
      width: '2px',
      'line-color': '#94a3b8',
      'target-arrow-color': '#94a3b8',
      'curve-style': 'bezier'
    }
  }
];

const graphLayout: LayoutOptions = {
  name: 'breadthfirst',
  directed: false,
  padding: 60,
  spacingFactor: 1.25
};
