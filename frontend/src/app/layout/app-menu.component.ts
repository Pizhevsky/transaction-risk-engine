import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive, type Route } from '@angular/router';
import { routes } from '../app.routes';

interface MenuItem {
  label: string;
  path: string;
  order: number;
}

@Component({
  selector: 'app-menu',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './app-menu.component.html',
  styleUrls: ['./app-menu.component.css']
})

export class AppMenuComponent {
  readonly items = routes
    .map(toMenuItem)
    .filter((item): item is MenuItem => item !== null)
    .sort((left, right) => left.order - right.order);
}

function toMenuItem(route: Route): MenuItem | null {
  const label = route.data?.['menuLabel'];
  if (typeof route.path !== 'string' || route.path.length === 0 || typeof label !== 'string') {
    return null;
  }

  return {
    label,
    path: `/${route.path}`,
    order: Number(route.data?.['menuOrder'] ?? 0)
  };
}
