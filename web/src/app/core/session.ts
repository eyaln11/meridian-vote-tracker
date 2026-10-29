import { Injectable, computed, signal } from '@angular/core';
import { api, call, setApiUser, type Schemas } from '../api/client';

type User = Schemas['UserDto'];

const STORAGE_KEY = 'meridian.user';

/**
 * Who is acting. The README allows a simplified sign-in: predefined users and a switcher at the top of the screen.
 * The role is still enforced by the server, so hiding a button here is only a convenience.
 */
@Injectable({ providedIn: 'root' })
export class Session {
  readonly users = signal<User[]>([]);
  readonly user = signal<User | null>(null);
  readonly isAdmin = computed(() => this.user()?.role === 'Admin');

  async init(): Promise<void> {
    this.users.set(await call(api.GET('/api/users')));

    const remembered = this.read();
    const start = this.users().find((u) => u.username === remembered) ?? this.users()[0] ?? null;
    this.select(start?.username ?? null);
  }

  select(username: string | null): void {
    const user = this.users().find((u) => u.username === username) ?? null;
    setApiUser(user?.username ?? null);
    this.user.set(user);
    this.write(user?.username ?? null);
  }

  // Browser storage is per tab, so two tabs can act as two users (Dana and Omer). It can also be blocked, so it is only a convenience and every access is guarded.
  private read(): string | null {
    try {
      return sessionStorage.getItem(STORAGE_KEY);
    } catch {
      return null;
    }
  }

  private write(username: string | null): void {
    try {
      if (username) {
        sessionStorage.setItem(STORAGE_KEY, username);
      }
    } catch {
      // ignore
    }
  }
}
