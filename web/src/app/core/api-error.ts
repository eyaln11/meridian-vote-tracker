/**
 * Every failed call becomes one of these, so every screen shows errors the same way (README 7.7):
 * a title and detail for the banner, and field errors that belong next to the input they are about.
 */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly title: string,
    public readonly detail?: string,
    /** Validation messages by field name, e.g. { weight: ['The weight must be ...'] }. */
    public readonly fieldErrors: Record<string, string[]> = {},
    /** The id that appears in the server logs for this request. */
    public readonly correlationId?: string,
    /** Extra data the server attached, e.g. the current state of a conflict that was already decided (409). */
    public readonly extensions: Record<string, unknown> = {},
  ) {
    super(title);
  }

  /** The first message for a field, or null. Field names are matched ignoring case. */
  field(name: string): string | null {
    const key = Object.keys(this.fieldErrors).find((k) => k.toLowerCase() === name.toLowerCase());
    return key ? (this.fieldErrors[key][0] ?? null) : null;
  }

  get hasFieldErrors(): boolean {
    return Object.keys(this.fieldErrors).length > 0;
  }
}

interface ProblemBody {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
  correlationId?: string;
  [extra: string]: unknown;
}

const known = new Set(['type', 'title', 'status', 'detail', 'instance', 'errors', 'correlationId', 'traceId']);

/** Turns the server's ProblemDetails response (RFC 9457) into an ApiError. */
export function toApiError(response: Response, body: unknown): ApiError {
  const problem: ProblemBody = body && typeof body === 'object' ? (body as ProblemBody) : {};

  // The server names fields like "Weight" or "$.weight"; the screens use "weight".
  const fieldErrors: Record<string, string[]> = {};
  for (const [key, messages] of Object.entries(problem.errors ?? {})) {
    const name = key.split('.').pop() ?? key;
    fieldErrors[name.charAt(0).toLowerCase() + name.slice(1)] = messages;
  }

  const extensions: Record<string, unknown> = {};
  for (const [key, value] of Object.entries(problem)) {
    if (!known.has(key)) {
      extensions[key] = value;
    }
  }

  return new ApiError(
    response.status,
    problem.title ?? `The request failed (${response.status})`,
    problem.detail,
    fieldErrors,
    problem.correlationId ?? response.headers.get('X-Correlation-ID') ?? undefined,
    extensions,
  );
}
