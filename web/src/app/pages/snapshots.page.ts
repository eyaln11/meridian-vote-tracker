import { DatePipe } from '@angular/common';
import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { api, call, num, type Schemas } from '../api/client';
import { ApiError } from '../core/api-error';
import { Session } from '../core/session';
import { ProblemComponent } from '../shared/problem.component';

type Snapshot = Schemas['SnapshotDto'];

@Component({
  selector: 'app-snapshots',
  imports: [FormsModule, DatePipe, RouterLink, ProblemComponent],
  template: `
    <h2>Snapshots</h2>

    <section class="card">
      <h3>Create a snapshot</h3>
      <p class="muted">Choose votes by dates and/or session, and add specific votes if you like. Choosing nothing uses every vote. The calculation runs in the background.</p>
      <p class="muted">A vote without a position is left out of the ranking and listed in the result. Set positions on the <a routerLink="/votes">Votes</a> screen (filter: "No position yet").</p>
      <div class="row">
        <div class="field" [class.invalid]="error()?.field('name')">
          <label for="name">Name</label>
          <input id="name" [(ngModel)]="name" maxlength="100" />
          @if (error()?.field('name'); as m) { <span class="field-error">{{ m }}</span> }
        </div>
        <div class="field" [class.invalid]="error()?.field('dateFrom')">
          <label for="from">From</label>
          <input id="from" type="date" [(ngModel)]="dateFrom" />
          @if (error()?.field('dateFrom'); as m) { <span class="field-error">{{ m }}</span> }
        </div>
        <div class="field">
          <label for="to">To</label>
          <input id="to" type="date" [(ngModel)]="dateTo" />
        </div>
        <div class="field" [class.invalid]="error()?.field('session')">
          <label for="session">Session</label>
          <input id="session" type="number" min="1" [(ngModel)]="sessionNumber" />
          @if (error()?.field('session'); as m) { <span class="field-error">{{ m }}</span> }
        </div>
      </div>
      <div class="field" [class.invalid]="error()?.field('voteIds')">
        <label for="ids">Specific votes (one per line or separated by commas)</label>
        <textarea id="ids" rows="2" [(ngModel)]="voteIds" placeholder="MA/2024/003, MA/2024/010"></textarea>
        @if (error()?.field('voteIds'); as m) { <span class="field-error">{{ m }}</span> }
      </div>
      <div class="spacer"></div>
      <button (click)="create()" [disabled]="creating()">{{ creating() ? 'Starting…' : 'Create snapshot' }}</button>
      @if (error() && !error()!.hasFieldErrors) {
        <app-problem [error]="error()" />
      }
    </section>

    <app-problem [error]="loadError()" />

    <section class="card">
      <h3>All snapshots</h3>
      @if (snapshots().length === 0) {
        <p class="empty">No snapshot has been created yet.</p>
      } @else {
        <table>
          <thead><tr><th>Name</th><th>Created</th><th>By</th><th>Status</th><th>Progress</th><th class="num">Votes</th><th>Published</th></tr></thead>
          <tbody>
            @for (s of snapshots(); track s.id) {
              <tr class="clickable" (click)="open(s)">
                <td>{{ s.name }}</td>
                <td>{{ s.createdAt | date: 'yyyy-MM-dd HH:mm' }}</td>
                <td>{{ s.createdBy }}</td>
                <td><span class="pill" [class]="'pill ' + s.status">{{ s.status }}</span></td>
                <td>
                  @if (s.status === 'Processing' || s.status === 'Queued') {
                    <progress [value]="percent(s)" max="100"></progress>
                    <div class="muted">{{ s.stage ?? 'Waiting' }}</div>
                  } @else if (s.status === 'Failed') {
                    <span class="field-error">{{ s.error }}</span>
                  } @else {
                    {{ s.countriesScored }} countries scored
                  }
                </td>
                <td class="num">{{ s.votesIncluded }} of {{ s.votesSelected }}</td>
                <td>{{ s.publishedQuarter ?? '' }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    </section>
  `,
})
export class SnapshotsPage {
  private readonly session = inject(Session);
  private readonly router = inject(Router);

  protected readonly snapshots = signal<Snapshot[]>([]);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly creating = signal(false);

  protected name = '';
  protected dateFrom = '';
  protected dateTo = '';
  protected sessionNumber: number | null = null;
  protected voteIds = '';

  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    effect(() => {
      this.session.user();
      void this.refresh();
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
  }

  protected percent(s: Snapshot): number {
    const total = num(s.totalRows);
    return total === 0 ? 0 : Math.min(100, (num(s.rowsProcessed) / total) * 100);
  }

  protected open(s: Snapshot): void {
    void this.router.navigate(['/snapshots', s.id]);
  }

  protected async create(): Promise<void> {
    this.creating.set(true);
    this.error.set(null);
    try {
      const ids = this.voteIds.split(/[\s,;]+/).filter((v) => v.length > 0);
      await call(
        api.POST('/api/snapshots', {
          body: {
            name: this.name,
            dateFrom: this.dateFrom || undefined,
            dateTo: this.dateTo || undefined,
            session: this.sessionNumber ?? undefined,
            voteIds: ids.length > 0 ? ids : undefined,
          },
        }),
      );
      this.name = '';
      await this.refresh();
    } catch (e) {
      this.error.set(e as ApiError);
    } finally {
      this.creating.set(false);
    }
  }

  /** Keeps refreshing every second while a snapshot is being calculated, so the screen shows live progress and is never stuck waiting (README SNP-02). */
  private async refresh(): Promise<void> {
    clearTimeout(this.timer);
    try {
      const list = await call(api.GET('/api/snapshots'));
      this.snapshots.set(list);
      this.loadError.set(null);
      if (list.some((s) => s.status === 'Queued' || s.status === 'Processing')) {
        this.timer = setTimeout(() => void this.refresh(), 1000);
      }
    } catch (e) {
      this.loadError.set(e as ApiError);
    }
  }
}
