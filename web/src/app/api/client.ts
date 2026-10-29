import createClient, { type Middleware } from 'openapi-fetch';
import { ApiError, toApiError } from '../core/api-error';
import type { components, paths } from './schema';

/** The shapes of the server's requests and responses, generated from its OpenAPI contract (see `npm run api:generate`). */
export type Schemas = components['schemas'];

let currentUser: string | null = null;

/** Tells the client which predefined user the next requests act as. */
export function setApiUser(username: string | null): void {
  currentUser = username;
}

/**
 * Runs on every request: names the user (the simplified sign-in) and sends a correlation id,
 * so one click in the UI can be followed through the API logs (README section 9).
 */
const headers: Middleware = {
  onRequest({ request }) {
    if (currentUser) {
      request.headers.set('X-User', currentUser);
    }
    request.headers.set('X-Correlation-ID', crypto.randomUUID().replaceAll('-', ''));
    return request;
  },
};

/** The typed API client: every path, parameter and response below is checked by the compiler against the contract. */
export const api = createClient<paths>({ baseUrl: '' });
api.use(headers);

/** Awaits a client call and returns its data, or throws an ApiError that every screen can show. */
export async function call<D>(
  request: Promise<{ data?: D; error?: unknown; response: Response }>,
): Promise<D> {
  let result;
  try {
    result = await request;
  } catch {
    throw new ApiError(0, 'Cannot reach the server', 'Check that the API is running, then try again.');
  }
  if (result.error !== undefined || !result.response.ok) {
    throw toApiError(result.response, result.error);
  }
  return result.data as D;
}

/** The API returns 64-bit numbers as number or string; the screens only need plain numbers. */
export const num = (value: number | string | null | undefined): number => Number(value ?? 0);
