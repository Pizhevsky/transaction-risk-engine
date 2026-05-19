import { NgClass } from '@angular/common';
import { ChangeDetectionStrategy, Component, Input } from '@angular/core';

@Component({
  selector: 'app-decision-badge',
  standalone: true,
  imports: [NgClass],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './decision-badge.component.html',
  styleUrls: ['./decision-badge.component.css']
})

export class DecisionBadgeComponent {
  @Input({ required: true }) decision!: string;
}
