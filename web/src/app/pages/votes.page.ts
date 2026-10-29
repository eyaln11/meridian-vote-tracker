import { DatePipe } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { api, call, type Schemas } from '../api/client';
import { ApiError } from '../core/api-error';
import { Session } from '../core/session';
import { ProblemComponent } from '../shared/problem.component';

@Component({
  selector: 'app-votes',
  imports: [FormsModule, DatePipe, ProblemComponent],
  template: `
    <h2>Votes</h2>
    @if (session.isAdmin()) {
      <p class="muted">As the admin you can set the stance and weight of a vote: use "Set position" or "Change" in a row.</p>
    } @else {
      <p class="muted">Only the admin (Lena) can set positions. Open a vote to see its country votes and its timeline.</p>
    }

    <section class="card">
      <div class="row">
        <div class="field">
          <label for="search">Search</label>
          <input id="search" placeholder="Title or MA/2024/003" [(ngModel)]="search" (keyup.enter)="load(1)" />
        </div>
        <div class="field" [class.invalid]="error()?.field('session')">
          <label for="session">Session</label>
          <input id="session" type="number" min="1" [(ngModel)]="sessionFilter" />
        </div>
        <div class="field">
          <label for="from">From</label>
          <input id="from" type="date" [(ngModel)]="from" />
        </div>
        <div class="field">
          <label for="to">To</label>
          <input id="to" type="date" [(ngModel)]="to" />
        </div>
        <div class="field">
          <label for="pos">Position</label>
          <select id="pos" [(ngModel)]="position">
            <option value="">Any</option>
            <option value="yes">Has a position</option>
            <option value="no">No position yet</option>
          </select>
        </div>
        <button (click)="load(1)">Filter</button>
      </div>
    </section>

    <app-problem [error]="error()" />

    @if (page(); as p) {
      @if (p.items.length === 0) {
        <p class="empty">No votes match.</p>
      } @else {
        <section class="card">
          <table>
            <thead>
              <tr><th>Vote</th><th>Date</th><th>Title</th><th class="num">Session</th><th class="num">Country votes</th><th class="num">Open conflicts</th><th>Position</th></tr>
            </thead>
            <tbody>
              @for (v of p.items; track v.id) {
                <tr class="clickable" (click)="open(v.id)">
                  <td>{{ v.voteId }}</td>
                  <td class="nowrap">{{ v.voteDate | date: 'yyyy-MM-dd' }}</td>
                  <td>{{ v.title }}</td>
                  <td class="num">{{ v.session }}</td>
                  <td class="num">{{ v.countryVotes }}</td>
                  <td class="num">{{ v.openConflicts }}</td>
                  <td>
                   <div class="position-cell">
                    @if (v.position; as pos) {
                      {{ pos.stance }}, weight {{ pos.weight }}
                    } @else {
                      <span class="pill warn" title="This vote is left out of every ranking until it has a position">No position</span>
                    }
                    @if (session.isAdmin()) {
                      <button class="secondary small" (click)="$event.stopPropagation(); open(v.id)">{{ v.position ? 'Change' : 'Set position' }}</button>
                    }
                   </div>
                  </td>
                </tr>
              }
            </tbody>
          </table>
          <div class="pager">
            <button class="secondary" (click)="load(p.page - 1)" [disabled]="p.page <= 1">Previous</button>
            <span>Page {{ p.page }} of {{ pages(p.total, p.pageSize) }} ({{ p.total }} votes)</span>
            <button class="secondary" (click)="load(p.page + 1)" [disabled]="p.page >= pages(p.total, p.pageSize)">Next</button>
          </div>
        </section>
      }
    }
  `,
})
export class VotesPage {
  protected readonly session = inject(Session);
  private readonly router = inject(Router);

  protected readonly page = signal<Schemas['PagedResultOfVoteListItemDto'] | null>(null);
  protected readonly error = signal<ApiError | null>(null);

  protected search = '';
  protected sessionFilter: number | null = null;
  protected from = '';
  protected to = '';
  protected position: '' | 'yes' | 'no' = '';

  constructor() {
    effect(() => {
      this.session.user();
      void this.load(1);
    });
  }

  protected pages(total: number, size: number): number {
    return Math.max(1, Math.ceil(total / size));
  }

  protected open(id: number): void {
    void this.router.navigate(['/votes', id]);
  }

  protected async load(page: number): Promise<void> {
    try {
      this.page.set(
        await call(
          api.GET('/api/votes', {
            params: {
              query: {
                search: this.search.trim() || undefined,
                session: this.sessionFilter ?? undefined,
                from: this.from || undefined,
                to: this.to || undefined,
                hasPosition: this.position === '' ? undefined : this.position === 'yes',
                page,
                pageSize: 25,
              },
            },
          }),
        ),
      );
      this.error.set(null);
    } catch (e) {
      this.error.set(e as ApiError);
    }
  }
}
