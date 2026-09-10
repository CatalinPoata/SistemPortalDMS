import "client-only";

import { ApiError, asApiError, request } from "@/lib/http";
import { authConfig, type PortalRole } from "@/lib/auth-config";


type Tokens = {
  accessToken: string;
  accessTokenExpiresAt: string;
};

type LoginResult =
  | Tokens
  | {
      challenge: string;
      expiresAt: string;
    };

export type CurrentUser = {
  id: string;
  email: string;
  fullName: string;
  role: PortalRole;
  emailConfirmed: boolean;
};

type AuthState =
  | { status: "loading"; user: null; error: null }
  | { status: "anonymous"; user: null; error: null }
  | { status: "authenticated"; user: CurrentUser; error: null }
  | { status: "error"; user: null; error: ApiError };

const initialState: AuthState = {
  status: "loading",
  user: null,
  error: null,
};

let state: AuthState = initialState;
let tokens: Tokens | null = null;
let initialized = false;
let restorePromise: Promise<void> | null = null;
let channel: BroadcastChannel | null = null;

const listeners = new Set<() => void>();

export function getAuthSnapshot() {
  return state;
}

export function getServerAuthSnapshot() {
  return initialState;
}

export function subscribeAuth(listener: () => void) {
  listeners.add(listener);

  return () => {
    listeners.delete(listener);
  };
}

function publish(next: AuthState) {
  state = next;
  listeners.forEach(listener => listener());
}

function clearSession() {
  tokens = null;

  publish({
    status: "anonymous",
    user: null,
    error: null,
  });
}

async function withAuthLock<T>(
  action: () => Promise<T>,
): Promise<T> {
  if (!window.isSecureContext || !navigator.locks) {
    throw new ApiError(0, {
      detail:
        "Deschide aplicația prin HTTPS într-un browser actualizat.",
    });
  }

  return navigator.locks.request(authConfig.lockName, action);
}

function bearer(accessToken: string) {
  return {
    Authorization: `Bearer ${accessToken}`,
  };
}

function isTokens(result: LoginResult): result is Tokens {
  return (
    "accessToken" in result &&
    "accessTokenExpiresAt" in result
  );
}

async function getCsrf(accessToken?: string) {
  const result = await request<{ token: string }>(
    "/api/security/csrf",
    {
      headers: accessToken ? bearer(accessToken) : undefined,
    },
  );

  return result.token;
}

async function refreshLocked(): Promise<Tokens> {
  tokens = null;

  const csrf = await getCsrf();

  const result = await request<Tokens>("/api/auth/refresh", {
    method: "POST",
    headers: {
      "X-CSRF-TOKEN": csrf,
    },
  });

  tokens = result;
  return result;
}

async function refreshOrClearSessionLocked(): Promise<Tokens> {
  try {
    return await refreshLocked();
  } catch (error) {
    const failure = asApiError(error);

    if (failure.status === 401) {
      clearSession();
      channel?.postMessage("session-changed");
    }

    throw failure;
  }
}

async function ensureTokensLocked(): Promise<Tokens> {
  if (
    tokens &&
    Date.parse(tokens.accessTokenExpiresAt) > Date.now() + 30_000
  ) {
    return tokens;
  }

  return refreshOrClearSessionLocked();
}

async function readUserLocked(): Promise<CurrentUser> {
  const current = await ensureTokensLocked();

  const user = await request<CurrentUser>(authConfig.profilePath, {
    headers: bearer(current.accessToken),
  });

  if (!authConfig.allowedRoles.includes(user.role)) {
    throw new ApiError(401, {
      detail: authConfig.forbiddenMessage,
    });
  }

  return user;
}

async function revokeCurrentSessionLocked(current: Tokens) {
  const csrf = await getCsrf(current.accessToken);

  await request<void>("/api/auth/revoke", {
    method: "POST",
    headers: {
      ...bearer(current.accessToken),
      "X-CSRF-TOKEN": csrf,
    },
  });
}

async function restoreLocked() {
  try {
    const user = await readUserLocked();

    publish({
      status: "authenticated",
      user,
      error: null,
    });
  } catch (error) {
    const failure = asApiError(error);

    if (failure.status === 401) {
      clearSession();
      return;
    }

    if (failure.status === 403) {
      const current = tokens;

      if (current) {
        try {
          await revokeCurrentSessionLocked(current);
        } catch {
        }
      }

      clearSession();
      channel?.postMessage("session-changed");
      return;
    }

    publish({
      status: "error",
      user: null,
      error: failure,
    });
  }
}

export function restoreSession(): Promise<void> {
  if (!restorePromise) {
    restorePromise = withAuthLock(restoreLocked)
      .catch(error => {
        publish({
          status: "error",
          user: null,
          error: asApiError(error),
        });
      })
      .finally(() => {
        restorePromise = null;
      });
  }

  return restorePromise;
}

async function synchronizeSession() {
  try {
    await withAuthLock(async () => {
      tokens = null;
      await restoreLocked();
    });
  } catch (error) {
    publish({
      status: "error",
      user: null,
      error: asApiError(error),
    });
  }
}

export function initializeAuth() {
  if (initialized) return;

  initialized = true;

  if (typeof BroadcastChannel !== "undefined") {
    channel = new BroadcastChannel(authConfig.channelName);

    channel.onmessage = event => {
      if (event.data === "session-changed") {
        void synchronizeSession();
      }
    };
  }

  void restoreSession();
}

export async function login(email: string, password: string) {
  await withAuthLock(async () => {
    const result = await request<LoginResult>("/api/auth/login", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        email: email.trim(),
        password,
      }),
    });

    if (!isTokens(result)) {
      clearSession();

      throw new ApiError(401, {
        detail: authConfig.forbiddenMessage,
      });
    }

    tokens = result;

    channel?.postMessage("session-changed");

    try {
      const user = await readUserLocked();

      publish({
        status: "authenticated",
        user,
        error: null,
      });
    } catch (error) {
      const current = tokens;

      if (current) {
        try {
          await revokeCurrentSessionLocked(current);
        } catch {
        }
      }

      clearSession();
      channel?.postMessage("session-changed");

      throw error;
    }
  });
}

export async function logout() {
  await withAuthLock(async () => {
    try {
      const current = await ensureTokensLocked();
      await revokeCurrentSessionLocked(current);
    } catch (error) {
      const failure = asApiError(error);
      if (failure.status !== 401) throw failure;
    }

    clearSession();
    channel?.postMessage("session-changed");
  });
}

export async function authenticatedRequest<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  return withAuthLock(async () => {
    try {
      let current = await ensureTokensLocked();

      const send = () => {
        const headers = new Headers(init.headers);
        headers.set("Authorization", `Bearer ${current.accessToken}`);

        return request<T>(path, {
          ...init,
          headers,
        });
      };

      try {
        return await send();
      } catch (error) {
        const failure = asApiError(error);
        if (failure.status !== 401) throw failure;

        current = await refreshOrClearSessionLocked();
        return send();
      }
    } catch (error) {
      const failure = asApiError(error);
      if (failure.status === 401) {
        clearSession();
        channel?.postMessage("session-changed");
      }
      throw failure;
    }
  });
}

export async function authenticatedFetch(
  path: string,
  init: RequestInit = {},
): Promise<Response> {
  if (!path.startsWith("/api/")) {
    throw new Error("Sunt permise numai rute locale /api/.");
  }

  return withAuthLock(async () => {
    try {
      let current = await ensureTokensLocked();

      const send = async () => {
        const headers = new Headers(init.headers);
        headers.set("Authorization", `Bearer ${current.accessToken}`);
        headers.set("Accept", "application/pdf, application/problem+json");

        let response: Response;
        try {
          response = await fetch(path, {
            ...init,
            headers,
            credentials: "same-origin",
            cache: "no-store",
            redirect: "error",
          });
        } catch (error) {
          throw asApiError(error);
        }

        if (response.ok) return response;

        const problem = await response.json().catch(() => ({
          detail: "Serverul nu a putut procesa cererea.",
        }));
        throw new ApiError(response.status, {
          ...problem,
          traceId:
            problem.traceId ?? response.headers.get("X-Trace-Id") ?? undefined,
        });
      };

      try {
        return await send();
      } catch (error) {
        const failure = asApiError(error);
        if (failure.status !== 401) throw failure;

        current = await refreshOrClearSessionLocked();
        return send();
      }
    } catch (error) {
      const failure = asApiError(error);
      if (failure.status === 401) {
        clearSession();
        channel?.postMessage("session-changed");
      }
      throw failure;
    }
  });
}

export async function resetPassword(
  token: string,
  newPassword: string,
  confirmPassword: string,
): Promise<{ message: string }> {
  return withAuthLock(async () => {
    const result = await request<{ message: string }>(
      "/api/auth/reset-password",
      {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          token,
          newPassword,
          confirmPassword,
        }),
      },
    );

    clearSession();

    if (typeof BroadcastChannel !== "undefined") {
      const sender =
        channel ?? new BroadcastChannel(authConfig.channelName);

      sender.postMessage("session-changed");

      if (sender !== channel) {
        sender.close();
      }
    }

    return result;
  });
}
