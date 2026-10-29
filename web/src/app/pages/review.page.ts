import { DatePipe } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { api, call, type Schemas } from '../api/client';
import { ApiError } from '../core/api-error';
import { Session } from '../core/session';
import { ProblemComponent } from '../shared/problem.component';

type Conflict = Schemas['ConflictDto'];

@Component({
  selector: 'app-review',
  imports: [FormsModule, DatePipe, ProblemComponent],
  template: `
    <h2>Review</h2>
    <p class="muted">Conflicts are new file values that disagree with approved data. Nothing changes until a person decides.</p>
    @if (session.user(); as u) {
      <p class="muted">You are acting as <b>{{ u.displayName }}</b>. Decisions you make are recorded under this name.</p>
    }

    <section class="card">
      <div class="row">
        <div class="field">
          <label for="vote">Vote</label>
          <input id="vote" placeholder="MA/2024/003" [ngModel]="voteFilter()" (ngModelChange)="voteFilter.set($event)" (keyup.enter)="load()" />
        </div>
        <div class="field">
          <label for="status">Show</label>
          <select id="status" [ngModel]="status()" (ngModelChange)="status.set($event); load()">
            <option value="Open">Waiting for a decision</option>
            <option value="Accepted">Decided: new value accepted</option>
            <option value="Rejected">Decided: approved value kept</option>
            <option value="">All conflicts</option>
          </select>
        </div>
        <button (click)="load()">Filter</button>
      </div>
    </section>

    <app-problem [error]="error()" />
    @if (notice(); as n) {
      <div class="notice">{{ n }}</div>
    }
    @if (lostRace(); as r) {
      <section class="card">
        <h3>Current state of {{ r.voteId }}: {{ describe(r) }}</h3>
        <div class="compare">
          <div>
            <div class="label">Value now</div>
            <div class="value">{{ r.status === 'Accepted' ? r.proposedValue : r.approvedValue }}</div>
            <small>{{ r.status }} by {{ r.resolvedBy }} on {{ r.resolvedAt | date: 'yyyy-MM-dd HH:mm' }}@if (r.resolutionNote) { : "{{ r.resolutionNote }}" }</small>
          </div>
          <div>
            <div class="label">Previous value</div>
            <div class="value">{{ r.status === 'Accepted' ? r.approvedValue : r.proposedValue }}</div>
            <small>{{ r.status === 'Accepted' ? 'The approved value before the decision' : 'The value that was proposed and not accepted' }}</small>
          </div>
        </div>
      </section>
    }

    @if (!error()) {
      @if (conflicts().length === 0) {
        <p class="empty">
          @if (status() === 'Open') {
            No conflict is waiting for a decision. Choose "All conflicts" to see the ones that were already decided.
          } @else {
            No conflicts to show with this filter.
          }
        </p>
      } @else {
        <p class="muted">{{ total() }} conflicts. {{ conflicts().length === total() ? '' : 'Showing the first ' + conflicts().length + '.' }}</p>
      }
    }

    @for (c of conflicts(); track c.id) {
      <section class="card">
        <div class="row">
          <strong>{{ c.voteId }}</strong>
          <span>{{ c.voteTitle }}</span>
          <span class="pill">{{ describe(c) }}</span>
          <span class="pill" [class]="'pill ' + c.status">{{ c.status }}</span>
        </div>

        <div class="compare">
          <div>
            <div class="label">Approved now</div>
            <div class="value">{{ c.approvedValue }}</div>
            <small>{{ c.approvedSource.fileName }}, uploaded by {{ c.approvedSource.uploadedBy }} on {{ c.approvedSource.uploadedAt | date: 'yyyy-MM-dd HH:mm' }}</small>
          </div>
          <div>
            <div class="label">New value</div>
            <div class="value">{{ c.proposedValue }}</div>
            <small>{{ c.proposedSource.fileName }} line {{ c.proposedLine }}, uploaded by {{ c.proposedSource.uploadedBy }} on {{ c.proposedSource.uploadedAt | date: 'yyyy-MM-dd HH:mm' }}</small>
          </div>
        </div>

        @if (c.status === 'Open') {
          <div class="row">
            <div class="field" [class.invalid]="rowError(c.id)?.field('note')">
              <label [for]="'note' + c.id">Note (optional)</label>
              <input [id]="'note' + c.id" [ngModel]="notes()[c.id] || ''" (ngModelChange)="setNote(c.id, $event)" maxlength="1000" />
              @if (rowError(c.id)?.field('note'); as message) {
                <span class="field-error">{{ message }}</span>
              }
            </div>
            <button (click)="decide(c, 'Accept')" [disabled]="busy() === c.id">Accept new value</button>
            <button class="danger" (click)="decide(c, 'Reject')" [disabled]="busy() === c.id">Keep approved value</button>
          </div>
          @if (rowError(c.id); as e) {
            @if (!e.hasFieldErrors) {
              <app-problem [error]="e" />
            }
          }
        } @else {
          <p class="muted">
            {{ c.status }} by {{ c.resolvedBy }} on {{ c.resolvedAt | date: 'yyyy-MM-dd HH:mm' }}@if (c.resolutionNote) { : "{{ c.resolutionNote }}" }
          </p>
        }
      </section>
    }
  `,
})
export class ReviewPage {
  protected readonly session = inject(Session);

  protected readonly conflicts = signal<Conflict[]>([]);
  protected readonly voteFilter = signal('');
  protected readonly status = signal<'Open' | 'Accepted' | 'Rejected' | ''>('Open');
  protected readonly total = signal(0);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly notice = signal<string | null>(null);
  /** The conflict as the server sees it now, shown when another analyst decided first (README CON-05). */
  protected readonly lostRace = signal<Conflict | null>(null);
  protected readonly notes = signal<Record<number, string>>({});
  protected readonly busy = signal<number | null>(null);
  private readonly rowErrors = signal<Record<number, ApiError>>({});

  constructor() {
    effect(() => {
      this.session.user();
      void this.load();
    });
  }

  protected rowError(id: number): ApiError | undefined {
    return this.rowErrors()[id];
  }

  protected setNote(id: number, value: string): void {
    this.notes.update((n) => ({ ...n, [id]: value }));
  }

  protected describe(c: Conflict): string {
    return c.kind === 'Ballot' ? `Ballot of ${c.countryCode}` : c.kind === 'Title' ? 'Vote title' : 'Vote date';
  }

  protected async load(): Promise<void> {
    try {
      const page = await call(
        api.GET('/api/conflicts', {
          params: { query: { status: this.status() || undefined, voteId: this.voteFilter().trim() || undefined, pageSize: 100 } },
        }),
      );
      this.conflicts.set(page.items);
      this.total.set(page.total);
      this.error.set(null);
    } catch (e) {
      this.error.set(e as ApiError);
    }
  }

  protected async decide(c: Conflict, decision: 'Accept' | 'Reject'): Promise<void> {
    this.busy.set(c.id);
    this.notice.set(null);
    this.lostRace.set(null);
    this.rowErrors.update((r) => {
      const { [c.id]: _, ...rest } = r;
      return rest;
    });
    try {
      await call(
        api.POST('/api/conflicts/{id}/resolve', {
          params: { path: { id: c.id } },
          body: { decision, note: this.notes()[c.id]?.trim() || undefined },
        }),
      );
      this.notice.set(`${decision === 'Accept' ? 'Accepted' : 'Kept the approved value for'} ${this.describe(c)} in ${c.voteId}.`);
      await this.load();
    } catch (e) {
      const problem = e as ApiError;
      if (problem.status === 409) {
        // Someone decided first (README CON-05): show a clear message and refresh to the current state.
        await this.load();
        this.error.set(problem);
        this.lostRace.set((problem.extensions['conflict'] as Conflict | undefined) ?? null);
      } else {
        this.rowErrors.update((r) => ({ ...r, [c.id]: problem }));
      }
    } finally {
      this.busy.set(null);
    }
  }
}
