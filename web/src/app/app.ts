import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Session } from './core/session';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header class="top">
      <h1>Meridian Vote Tracker</h1>
      <nav>
        <a routerLink="/imports" routerLinkActive="active">Imports</a>
        <a routerLink="/review" routerLinkActive="active">Review</a>
        <a routerLink="/votes" routerLinkActive="active">Votes</a>
        <a routerLink="/snapshots" routerLinkActive="active">Snapshots</a>
      </nav>
      <label class="switcher">
        Acting as
        <select [value]="session.user()?.username" (change)="switchUser($event)" aria-label="Acting as">
          @for (u of session.users(); track u.id) {
            <option [value]="u.username" [selected]="u.username === session.user()?.username">
              {{ u.displayName }} ({{ u.role }})
            </option>
          }
        </select>
        <span class="role" [class]="'role ' + session.user()?.role">{{ session.user()?.role }}</span>
      </label>
    </header>
    <main>
      <router-outlet />
    </main>
  `,
})
export class App {
  protected readonly session = inject(Session);

  protected switchUser(event: Event): void {
    this.session.select((event.target as HTMLSelectElement).value);
  }
}
