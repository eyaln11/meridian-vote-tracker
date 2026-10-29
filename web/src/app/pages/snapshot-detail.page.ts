import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { api, call, num, type Schemas } from '../api/client';
import { ApiError } from '../core/api-error';
import { Session } from '../core/session';
import { ProblemComponent } from '../shared/problem.component';

@Component({
  selector: 'app-snapshot-detail',
  imports: [FormsModule, DatePipe, DecimalPipe, RouterLink, ProblemComponent],
  template: `
    <p><a routerLink="/snapshots">&larr; All snapshots</a></p>
    <app-problem [error]="error()" />

    @if (snapshot(); as s) {
      <h2>{{ s.name }} <span class="pill" [class]="'pill ' + s.status">{{ s.status }}</span></h2>
      <p class="muted">
        Created by {{ s.createdBy }} on {{ s.createdAt | date: 'yyyy-MM-dd HH:mm' }}.
        @if (s.filterFrom || s.filterTo) { Dates: {{ s.filterFrom ?? '…' }} to {{ s.filterTo ?? '…' }}. }
        @if (s.filterSession) { Session {{ s.filterSession }}. }
        @if (s.filterVoteIds?.length) { Plus {{ s.filterVoteIds!.length }} chosen votes. }
        @if (!s.filterFrom && !s.filterTo && !s.filterSession && !s.filterVoteIds?.length) { All votes. }
      </p>

      @if (s.status === 'Processing' || s.status === 'Queued') {
        <section class="card">
          <progress [value]="percent()" max="100"></progress>
          <span class="muted"> {{ s.stage ?? 'Waiting' }}</span>
        </section>
      } @else if (s.status === 'Failed') {
        <app-problem [error]="failure()" />
      } @else {
        @if (s.votesWithoutPosition > 0) {
          <div class="banner warning">
            <strong>{{ s.votesWithoutPosition }} of the {{ s.votesSelected }} selected votes were left out</strong>
            <div>They have no position, so they do not count. They are listed at the bottom of this page. Set their positions on the Votes screen and create a new snapshot to include them.</div>
          </div>
        }

        <section class="card">
          <h3>Scoring rules used</h3>
          <ul>
            <li>Only <b>Yes</b> and <b>No</b> ballots count. Abstentions and absences are shown but never counted.</li>
            <li>A ballot that agrees with the institute adds the vote's weight to both parts of the fraction; one that disagrees adds it to the denominator only.</li>
            <li>Score = 100 x matching weight / counted weight. A country for which nothing counted has no score and no rank.</li>
            <li>Equal scores share a rank (1, 2, 2, 4). Votes without a position are left out and listed below.</li>
          </ul>
          <div class="chips">
            <span class="chip">Votes selected <b>{{ s.votesSelected }}</b></span>
            <span class="chip">Included <b>{{ s.votesIncluded }}</b></span>
            <span class="chip">Left out (no position) <b>{{ s.votesWithoutPosition }}</b></span>
            <span class="chip">Countries scored <b>{{ s.countriesScored }}</b></span>
            <span class="chip">Without a score <b>{{ s.countriesUnscored }}</b></span>
          </div>
        </section>

        <section class="card">
          <h3>Publication</h3>
          @if (s.publishedQuarter) {
            <p>Published for <b>{{ s.publishedQuarter }}</b> by {{ s.publishedBy }} on {{ s.publishedAt | date: 'yyyy-MM-dd HH:mm' }}.</p>
            @if (session.isAdmin()) {
              <button class="secondary" (click)="unpublish()">Unpublish</button>
            }
          } @else if (session.isAdmin()) {
            <div class="row">
              <div class="field" [class.invalid]="publishError()?.field('quarter')">
                <label for="quarter">Quarter</label>
                <input id="quarter" placeholder="2024-Q1" [(ngModel)]="quarter" />
                @if (publishError()?.field('quarter'); as m) { <span class="field-error">{{ m }}</span> }
              </div>
              <button (click)="publish()">Mark as published</button>
            </div>
          } @else {
            <p class="muted">Not published. Only the admin can publish a snapshot.</p>
          }
          @if (publishError() && !publishError()!.hasFieldErrors) {
            <app-problem [error]="publishError()" />
          }
        </section>

        <section class="card">
          <h3>Ranking</h3>
          <table>
            <thead><tr><th class="num">Rank</th><th>Country</th><th class="num">Score</th><th class="num">Counted weight</th><th class="num">Matching weight</th><th class="num">Votes counted</th><th class="num">Not counted</th></tr></thead>
            <tbody>
              @for (e of entries(); track e.countryCode) {
                <tr class="clickable" (click)="showCountry(e.countryCode)">
                  <td class="num">{{ e.rank ?? '' }}</td>
                  <td>{{ e.countryCode }} {{ e.countryName }}</td>
                  <td class="num">@if (e.score !== null && e.score !== undefined) { {{ e.score | number: '1.1-1' }} } @else { <span class="muted">no score</span> }</td>
                  <td class="num">{{ rows(e.countedWeight) }}</td>
                  <td class="num">{{ rows(e.matchingWeight) }}</td>
                  <td class="num">{{ e.votesCounted }}</td>
                  <td class="num">{{ e.votesNotCounted }}</td>
                </tr>
              }
            </tbody>
          </table>
        </section>

        @if (country(); as c) {
          <section class="card">
            <h3>{{ c.entry.countryName }}: what each vote contributed</h3>
            <table>
              <thead><tr><th>Vote</th><th>Date</th><th>Title</th><th>Ballot</th><th>Institute</th><th class="num">Weight</th><th>Result</th></tr></thead>
              <tbody>
                @for (x of c.contributions.items; track x.voteId) {
                  <tr>
                    <td>{{ x.voteId }}</td>
                    <td>{{ x.voteDate | date: 'yyyy-MM-dd' }}</td>
                    <td>{{ x.title }}</td>
                    <td>{{ x.ballot }}</td>
                    <td>{{ x.stance }}</td>
                    <td class="num">{{ x.weight }}</td>
                    <td><span class="pill" [class]="'pill ' + x.outcome">{{ x.outcome }}</span></td>
                  </tr>
                }
              </tbody>
            </table>
            <div class="pager">
              <button class="secondary" (click)="loadCountry(c.entry.countryCode, c.contributions.page - 1)" [disabled]="c.contributions.page <= 1">Previous</button>
              <span>Page {{ c.contributions.page }} ({{ c.contributions.total }} votes)</span>
              <button class="secondary" (click)="loadCountry(c.entry.countryCode, c.contributions.page + 1)" [disabled]="c.contributions.page * c.contributions.pageSize >= c.contributions.total">Next</button>
            </div>
          </section>
        }

        @if (leftOut(); as l) {
          @if (l.items.length > 0) {
            <section class="card">
              <h3>Votes left out of the ranking</h3>
              <table>
                <thead><tr><th>Vote</th><th>Date</th><th>Title</th><th>Reason</th></tr></thead>
                <tbody>
                  @for (v of l.items; track v.voteId) {
                    <tr><td>{{ v.voteId }}</td><td>{{ v.voteDate | date: 'yyyy-MM-dd' }}</td><td>{{ v.title }}</td><td>{{ v.excludedReason }}</td></tr>
                  }
                </tbody>
              </table>
              @if (l.total > l.items.length) {
                <p class="muted">Showing the first {{ l.items.length }} of {{ l.total }}.</p>
              }
            </section>
          }
        }
      }
    }
  `,
})
export class SnapshotDetailPage {
  protected readonly session = inject(Session);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;

  protected readonly snapshot = signal<Schemas['SnapshotDto'] | null>(null);
  protected readonly entries = signal<Schemas['SnapshotEntryDto'][]>([]);
  protected readonly leftOut = signal<Schemas['PagedResultOfSnapshotVoteDto'] | null>(null);
  protected readonly country = signal<Schemas['CountryBreakdownDto'] | null>(null);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly publishError = signal<ApiError | null>(null);

  protected quarter = '';
  protected rows = num;

  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    effect(() => {
      this.session.user();
      void this.load();
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
  }

  protected percent(): number {
    const s = this.snapshot();
    const total = s ? num(s.totalRows) : 0;
    return total === 0 ? 0 : Math.min(100, (num(s!.rowsProcessed) / total) * 100);
  }

  protected failure(): ApiError {
    return new ApiError(500, 'The snapshot could not be calculated', this.snapshot()?.error ?? undefined);
  }

  private async load(): Promise<void> {
    clearTimeout(this.timer);
    try {
      const snapshot = await call(api.GET('/api/snapshots/{id}', { params: { path: { id: this.id } } }));
      this.snapshot.set(snapshot);
      this.error.set(null);

      if (snapshot.status === 'Queued' || snapshot.status === 'Processing') {
        this.timer = setTimeout(() => void this.load(), 1000);
      } else if (snapshot.status === 'Completed') {
        const [entries, leftOut] = await Promise.all([
          call(api.GET('/api/snapshots/{id}/entries', { params: { path: { id: this.id } } })),
          call(api.GET('/api/snapshots/{id}/votes', { params: { path: { id: this.id }, query: { included: false, pageSize: 50 } } })),
        ]);
        this.entries.set(entries);
        this.leftOut.set(leftOut);
      }
    } catch (e) {
      this.error.set(e as ApiError);
    }
  }

  protected showCountry(code: string): Promise<void> {
    return this.loadCountry(code, 1);
  }

  protected async loadCountry(code: string, page: number): Promise<void> {
    try {
      this.country.set(
        await call(api.GET('/api/snapshots/{id}/countries/{code}', { params: { path: { id: this.id, code }, query: { page, pageSize: 25 } } })),
      );
    } catch (e) {
      this.error.set(e as ApiError);
    }
  }

  protected async publish(): Promise<void> {
    this.publishError.set(null);
    try {
      await call(api.POST('/api/snapshots/{id}/publish', { params: { path: { id: this.id } }, body: { quarter: this.quarter.trim() } }));
      await this.load();
    } catch (e) {
      this.publishError.set(e as ApiError);
    }
  }

  protected async unpublish(): Promise<void> {
    this.publishError.set(null);
    try {
      await call(api.POST('/api/snapshots/{id}/unpublish', { params: { path: { id: this.id } } }));
      await this.load();
    } catch (e) {
      this.publishError.set(e as ApiError);
    }
  }
}
