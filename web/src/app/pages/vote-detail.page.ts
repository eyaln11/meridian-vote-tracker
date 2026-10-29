import { DatePipe } from '@angular/common';
import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { api, call, type Schemas } from '../api/client';
import { ApiError } from '../core/api-error';
import { Session } from '../core/session';
import { ProblemComponent } from '../shared/problem.component';

@Component({
  selector: 'app-vote-detail',
  imports: [FormsModule, DatePipe, RouterLink, ProblemComponent],
  template: `
    <p><a routerLink="/votes">&larr; All votes</a></p>
    <app-problem [error]="error()" />

    @if (vote(); as v) {
      <h2>{{ v.voteId }}: {{ v.title }}</h2>
      <p class="muted">Session {{ v.session }}, voted on {{ v.voteDate | date: 'yyyy-MM-dd' }}. {{ v.openConflicts }} conflicts waiting for a decision.</p>

      <section class="card">
        <h3>Institute position</h3>
        @if (v.position; as p) {
          <p>{{ p.stance }}, weight {{ p.weight }}. Last set by {{ p.updatedBy }} on {{ p.updatedAt | date: 'yyyy-MM-dd HH:mm' }}.</p>
        } @else {
          <p class="muted">No position yet. This vote is left out of every ranking until Lena sets one.</p>
        }

        @if (session.isAdmin()) {
          <div class="row">
            <div class="field" [class.invalid]="saveError()?.field('stance')">
              <label for="stance">Stance</label>
              <select id="stance" [(ngModel)]="stance">
                <option value="">Choose…</option>
                <option value="Yes">Yes</option>
                <option value="No">No</option>
              </select>
              @if (saveError()?.field('stance'); as message) {
                <span class="field-error">{{ message }}</span>
              }
            </div>
            <div class="field" [class.invalid]="saveError()?.field('weight')">
              <label for="weight">Weight (1 to 5)</label>
              <input id="weight" type="number" [(ngModel)]="weight" />
              @if (saveError()?.field('weight'); as message) {
                <span class="field-error">{{ message }}</span>
              }
            </div>
            <button (click)="save()" [disabled]="saving()">Save position</button>
          </div>
          @if (saveError() && !saveError()!.hasFieldErrors) {
            <app-problem [error]="saveError()" />
          }
          @if (saved()) {
            <div class="notice">Position saved.</div>
          }
        } @else {
          <p class="muted">Only the admin can change the position.</p>
        }
      </section>

      <section class="card">
        <h3>Country votes ({{ v.countryVotes.length }})</h3>
        <table>
          <thead><tr><th>Country</th><th>Ballot</th><th>Source</th><th>Approved by</th></tr></thead>
          <tbody>
            @for (c of v.countryVotes; track c.countryCode) {
              <tr>
                <td>{{ c.countryCode }} {{ c.countryName }}</td>
                <td>{{ c.ballot }}</td>
                <td>{{ c.sourceFile }}, line {{ c.sourceLine }}</td>
                <td>{{ c.approvedBy ?? 'imported' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </section>

      <section class="card">
        <h3>Timeline</h3>
        <table>
          <thead><tr><th>When</th><th>What</th><th>Who</th><th>Country</th><th>Before</th><th>After</th><th>Source / note</th></tr></thead>
          <tbody>
            @for (e of timeline(); track e.id) {
              <tr>
                <td>{{ e.occurredAt | date: 'yyyy-MM-dd HH:mm' }}</td>
                <td>{{ e.type }}</td>
                <td>{{ e.actor }}</td>
                <td>{{ e.countryCode }}</td>
                <td>{{ e.oldValue }}</td>
                <td>{{ e.newValue }}</td>
                <td>{{ e.file }} {{ e.note }}</td>
              </tr>
            }
          </tbody>
        </table>
      </section>
    }
  `,
})
export class VoteDetailPage {
  protected readonly session = inject(Session);
  private readonly id = Number(inject(ActivatedRoute).snapshot.paramMap.get('id'));

  protected readonly vote = signal<Schemas['VoteDetailDto'] | null>(null);
  protected readonly timeline = signal<Schemas['TimelineEventDto'][]>([]);
  protected readonly error = signal<ApiError | null>(null);
  protected readonly saveError = signal<ApiError | null>(null);
  protected readonly saving = signal(false);
  protected readonly saved = signal(false);

  protected stance: 'Yes' | 'No' | '' = '';
  protected weight: number | null = null;

  constructor() {
    effect(() => {
      this.session.user();
      void this.load();
    });
  }

  private async load(): Promise<void> {
    try {
      const [vote, timeline] = await Promise.all([
        call(api.GET('/api/votes/{id}', { params: { path: { id: this.id } } })),
        call(api.GET('/api/votes/{id}/timeline', { params: { path: { id: this.id } } })),
      ]);
      this.vote.set(vote);
      this.timeline.set(timeline);
      this.stance = (vote.position?.stance as 'Yes' | 'No' | undefined) ?? '';
      this.weight = vote.position?.weight ?? null;
      this.error.set(null);
    } catch (e) {
      this.error.set(e as ApiError);
    }
  }

  protected async save(): Promise<void> {
    this.saving.set(true);
    this.saved.set(false);
    this.saveError.set(null);
    try {
      await call(
        api.PUT('/api/votes/{id}/position', {
          params: { path: { id: this.id } },
          // The server validates the values and answers with field errors, which are shown next to the inputs.
          body: { stance: this.stance === '' ? null : this.stance, weight: this.weight ?? undefined },
        }),
      );
      this.saved.set(true);
      await this.load();
    } catch (e) {
      this.saveError.set(e as ApiError);
    } finally {
      this.saving.set(false);
    }
  }
}
