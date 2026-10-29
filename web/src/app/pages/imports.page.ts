import { DatePipe } from '@angular/common';
import { Component, DestroyRef, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { api, call, num, type Schemas } from '../api/client';
import { ApiError } from '../core/api-error';
import { Session } from '../core/session';
import { ProblemComponent } from '../shared/problem.component';

type Import = Schemas['ImportDto'];
type RowClass = Schemas['RowClass'];

@Component({
  selector: 'app-imports',
  imports: [FormsModule, DatePipe, ProblemComponent],
  template: `
    <h2>Imports</h2>

    <section class="card">
      <h3>Upload a CSV file</h3>
      <div class="row">
        <div class="field" [class.invalid]="uploadError()?.field('file')">
          <label for="file">File (.csv)</label>
          <input id="file" type="file" accept=".csv" (change)="chooseFile($event)" />
          @if (uploadError()?.field('file'); as message) {
            <span class="field-error">{{ message }}</span>
          }
        </div>
        <button (click)="upload()" [disabled]="uploading()">{{ uploading() ? 'Uploading…' : 'Upload' }}</button>
      </div>
      <p class="muted">The file is processed in the background. You can leave this page; progress is kept.</p>
      @if (uploadError() && !uploadError()!.hasFieldErrors) {
        <app-problem [error]="uploadError()" />
      }
    </section>

    <app-problem [error]="loadError()" />

    <section class="card">
      <h3>History</h3>
      @if (imports().length === 0) {
        <p class="empty">No file has been imported yet.</p>
      } @else {
        <table>
          <thead>
            <tr>
              <th>File</th><th>Uploaded</th><th>By</th><th>Status</th><th>Progress</th>
              <th class="num">New</th><th class="num">Conflicts opened</th>
            </tr>
          </thead>
          <tbody>
            @for (i of imports(); track i.id) {
              <tr class="clickable" (click)="open(i)">
                <td>{{ i.fileName }}</td>
                <td>{{ i.uploadedAt | date: 'yyyy-MM-dd HH:mm' }}</td>
                <td>{{ i.uploadedBy }}</td>
                <td><span class="pill" [class]="'pill ' + i.status">{{ i.status }}</span></td>
                <td>
                  @if (i.status === 'Processing' || i.status === 'Queued') {
                    <progress [value]="percent(i)" max="100"></progress>
                    <div class="muted">{{ i.stage ?? 'Waiting' }}: {{ rows(i.rowsParsed) }} of {{ rows(i.totalRows) }} read, {{ rows(i.rowsApplied) }} saved</div>
                  } @else if (i.status === 'Failed') {
                    <span class="field-error">{{ i.error }}</span>
                  } @else {
                    {{ rows(i.totalRows) }} rows
                  }
                </td>
                <td class="num">{{ rows(i.counts.new) }}</td>
                <td class="num">{{ i.counts.conflictsOpened }}</td>
              </tr>
            }
          </tbody>
        </table>
      }
    </section>

    @if (selected(); as s) {
      <section class="card">
        <h3>Report: {{ s.fileName }}</h3>
        <p class="muted">Uploaded by {{ s.uploadedBy }} on {{ s.uploadedAt | date: 'yyyy-MM-dd HH:mm' }}. {{ s.counts.votesCreated }} votes created.</p>
        <div class="chips">
          <span class="chip">New <b>{{ rows(s.counts.new) }}</b></span>
          <span class="chip">Unchanged <b>{{ rows(s.counts.unchanged) }}</b></span>
          <span class="chip">Duplicate <b>{{ rows(s.counts.duplicate) }}</b></span>
          <span class="chip">Invalid <b>{{ rows(s.counts.invalid) }}</b></span>
          <span class="chip">Contradiction <b>{{ rows(s.counts.contradiction) }}</b></span>
          <span class="chip">Conflict <b>{{ rows(s.counts.conflict) }}</b></span>
        </div>

        <h3>Rows that need an explanation</h3>
        <div class="row">
          <div class="field">
            <label for="class">Show</label>
            <select id="class" [ngModel]="rowClass()" (ngModelChange)="changeClass($event)">
              <option value="">All</option>
              <option value="Invalid">Invalid</option>
              <option value="Contradiction">Contradiction</option>
              <option value="Duplicate">Duplicate</option>
              <option value="Conflict">Conflict</option>
            </select>
          </div>
        </div>
        <app-problem [error]="reportError()" />
        @if (rowPage(); as p) {
          @if (p.items.length === 0) {
            <p class="empty">Nothing to show.</p>
          } @else {
            <table>
              <thead><tr><th class="num">Line</th><th>Class</th><th>Vote</th><th>Country</th><th>Reason</th><th>Original value</th></tr></thead>
              <tbody>
                @for (r of p.items; track r.lineNumber) {
                  <tr>
                    <td class="num">{{ r.lineNumber }}</td>
                    <td><span class="pill" [class]="'pill ' + r.class">{{ r.class }}</span></td>
                    <td>{{ r.voteId }}</td>
                    <td>{{ r.countryCode }}</td>
                    <td>{{ r.reason }}</td>
                    <td>{{ r.originalValue }}</td>
                  </tr>
                }
              </tbody>
            </table>
            <div class="pager">
              <button class="secondary" (click)="goRows(-1)" [disabled]="p.page <= 1">Previous</button>
              <span>Page {{ p.page }} of {{ pages(p.total, p.pageSize) }} ({{ p.total }} rows)</span>
              <button class="secondary" (click)="goRows(1)" [disabled]="p.page >= pages(p.total, p.pageSize)">Next</button>
            </div>
          }
        }

        <h3>What happened in each vote</h3>
        @if (votePage(); as v) {
          <table>
            <thead><tr><th>Vote</th><th>Created</th><th class="num">New</th><th class="num">Unchanged</th><th class="num">Duplicate</th><th class="num">Contradiction</th><th class="num">Conflict</th></tr></thead>
            <tbody>
              @for (x of v.items; track x.voteId) {
                <tr>
                  <td>{{ x.voteId }}</td>
                  <td>{{ x.voteCreated ? 'yes' : '' }}</td>
                  <td class="num">{{ x.new }}</td><td class="num">{{ x.unchanged }}</td><td class="num">{{ x.duplicate }}</td>
                  <td class="num">{{ x.contradiction }}</td><td class="num">{{ x.conflict }}</td>
                </tr>
              }
            </tbody>
          </table>
          <div class="pager">
            <button class="secondary" (click)="goVotes(-1)" [disabled]="v.page <= 1">Previous</button>
            <span>Page {{ v.page }} of {{ pages(v.total, v.pageSize) }} ({{ v.total }} votes)</span>
            <button class="secondary" (click)="goVotes(1)" [disabled]="v.page >= pages(v.total, v.pageSize)">Next</button>
          </div>
        }
      </section>
    }
  `,
})
export class ImportsPage {
  private readonly session = inject(Session);

  protected readonly imports = signal<Import[]>([]);
  protected readonly selected = signal<Import | null>(null);
  protected readonly rowPage = signal<Schemas['PagedResultOfImportRowDto'] | null>(null);
  protected readonly votePage = signal<Schemas['PagedResultOfImportVoteDto'] | null>(null);
  protected readonly rowClass = signal<RowClass | ''>('');
  protected readonly loadError = signal<ApiError | null>(null);
  protected readonly uploadError = signal<ApiError | null>(null);
  protected readonly reportError = signal<ApiError | null>(null);
  protected readonly uploading = signal(false);

  private file: File | null = null;
  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    // Reload when the acting user changes; stop the refresh timer when the page is left.
    effect(() => {
      this.session.user();
      void this.refresh();
    });
    inject(DestroyRef).onDestroy(() => clearTimeout(this.timer));
  }

  protected rows = num;

  protected percent(i: Import): number {
    const total = num(i.totalRows);
    if (total === 0) {
      return 0;
    }
    // Reading is the first half of the work and saving the second half.
    return Math.min(100, ((num(i.rowsParsed) + num(i.rowsApplied)) / (2 * total)) * 100);
  }

  protected pages(total: number, size: number): number {
    return Math.max(1, Math.ceil(total / size));
  }

  protected chooseFile(event: Event): void {
    this.file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.uploadError.set(null);
  }

  protected async upload(): Promise<void> {
    this.uploadError.set(null);
    if (!this.file) {
      this.uploadError.set(new ApiError(400, 'Validation failed', undefined, { file: ['Choose a CSV file first.'] }));
      return;
    }
    const file = this.file;
    this.uploading.set(true);
    try {
      await call(
        api.POST('/api/imports', {
          body: { file: file as unknown as string },
          // The upload is a multipart form; the browser adds the boundary itself.
          bodySerializer: () => {
            const form = new FormData();
            form.append('file', file);
            return form;
          },
        }),
      );
      await this.refresh();
    } catch (e) {
      this.uploadError.set(e as ApiError);
    } finally {
      this.uploading.set(false);
    }
  }

  protected async open(i: Import): Promise<void> {
    this.selected.set(i);
    this.rowClass.set('');
    await Promise.all([this.loadRows(1), this.loadVotes(1)]);
  }

  protected async changeClass(value: RowClass | ''): Promise<void> {
    this.rowClass.set(value);
    await this.loadRows(1);
  }

  protected goRows(step: number): Promise<void> {
    return this.loadRows((this.rowPage()?.page ?? 1) + step);
  }

  protected goVotes(step: number): Promise<void> {
    return this.loadVotes((this.votePage()?.page ?? 1) + step);
  }

  private async loadRows(page: number): Promise<void> {
    const id = this.selected()?.id;
    if (!id) {
      return;
    }
    try {
      const cls = this.rowClass();
      this.rowPage.set(
        await call(api.GET('/api/imports/{id}/rows', { params: { path: { id }, query: { class: cls || undefined, page, pageSize: 25 } } })),
      );
      this.reportError.set(null);
    } catch (e) {
      this.reportError.set(e as ApiError);
    }
  }

  private async loadVotes(page: number): Promise<void> {
    const id = this.selected()?.id;
    if (!id) {
      return;
    }
    try {
      this.votePage.set(await call(api.GET('/api/imports/{id}/votes', { params: { path: { id }, query: { page, pageSize: 25 } } })));
    } catch (e) {
      this.reportError.set(e as ApiError);
    }
  }

  /** Loads the history, and keeps refreshing every second while a file is still being processed (README ING-02). */
  private async refresh(): Promise<void> {
    clearTimeout(this.timer);
    try {
      const list = await call(api.GET('/api/imports'));
      this.imports.set(list);
      this.loadError.set(null);

      const current = this.selected();
      if (current) {
        const updated = list.find((i) => i.id === current.id);
        if (updated) {
          this.selected.set(updated);
        }
      }
      if (list.some((i) => i.status === 'Queued' || i.status === 'Processing')) {
        this.timer = setTimeout(() => void this.refresh(), 1000);
      }
    } catch (e) {
      this.loadError.set(e as ApiError);
    }
  }
}
