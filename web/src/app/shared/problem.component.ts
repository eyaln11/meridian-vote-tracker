import { Component, input } from '@angular/core';
import { ApiError } from '../core/api-error';

/** The one banner every screen uses for a failed request: what happened, why, and the reference to find it in the logs. */
@Component({
  selector: 'app-problem',
  template: `
    @if (error(); as e) {
      <div class="banner" [class.warning]="e.status === 409" role="alert">
        <strong>{{ e.title }}</strong>
        @if (e.detail) {
          <div>{{ e.detail }}</div>
        }
        @if (e.correlationId) {
          <small>Reference: {{ e.correlationId }}</small>
        }
      </div>
    }
  `,
})
export class ProblemComponent {
  readonly error = input<ApiError | null>(null);
}
