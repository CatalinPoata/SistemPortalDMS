import "client-only";

import {
  ApiError,
  asApiError,
  request,
  requestResponse,
} from "@/lib/http";
import { authConfig, type PortalRole } from "@/lib/auth-config";

type Tokens = {
  accessToken: string;
  accessTokenExpiresAt: string;
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
  | {
      status: "totp";
      user: null;
      error: null;
      challenge: string;
      expiresAt: string;
    }
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
    throw new ApiError(403, {
      detail: authConfig.forbiddenMessage,
    });
  }

  return user;
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
    const response = await requestResponse("/api/auth/login", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
      },
      body: JSON.stringify({
        email: email.trim(),
        password,
      }),
    });

    if (response.status === 202) {
      const challenge = (await response.json()) as {
        challenge: string;
        expiresAt: string;
      };

      tokens = null;
      publish({
        status: "totp",
        user: null,
        error: null,
        challenge: challenge.challenge,
        expiresAt: challenge.expiresAt,
      });
      return;
    }

    tokens = (await response.json()) as Tokens;

    channel?.postMessage("session-changed");

    try {
      const user = await readUserLocked();

      publish({
        status: "authenticated",
        user,
        error: null,
      });
    } catch (error) {
      publish({
        status: "error",
        user: null,
        error: asApiError(error),
      });

      throw error;
    }
  });
}

export async function logout() {
  await withAuthLock(async () => {
    try {
      const current = await ensureTokensLocked();

      const csrf = await getCsrf(current.accessToken);

      await request<void>("/api/auth/revoke", {
        method: "POST",
        headers: {
          ...bearer(current.accessToken),
          "X-CSRF-TOKEN": csrf,
        },
      });
    } catch (error) {
      const failure = asApiError(error);
      if (failure.status !== 401) throw failure;
    }

    clearSession();
    channel?.postMessage("session-changed");
  });
}

export async function verifyTotpLogin(challenge: string, code: string) {
  await withAuthLock(async () => {
    tokens = await request<Tokens>("/api/auth/totp/verify-login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ challenge, code }),
    });

    channel?.postMessage("session-changed");
    const user = await readUserLocked();
    publish({ status: "authenticated", user, error: null });
  });
}

export function cancelTotpLogin() {
  clearSession();
}

export async function apiRequest<T>(
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

function readDownloadName(response: Response, fallbackName: string) {
  const disposition = response.headers.get("Content-Disposition");
  const match = disposition?.match(
    /filename\*?=(?:UTF-8'')?\"?([^\";]+)/i,
  );

  if (!match?.[1]) return fallbackName;

  try {
    return decodeURIComponent(match[1]);
  } catch {
    return match[1];
  }
}

export async function apiDownload(
  path: string,
  init: RequestInit = {},
  {
    accept = "application/pdf, application/problem+json",
    fallbackName = "raport.pdf",
  }: {
    accept?: string;
    fallbackName?: string;
  } = {},
): Promise<{ blob: Blob; fileName: string }> {
  return withAuthLock(async () => {
    try {
      let current = await ensureTokensLocked();

      const send = () => {
        const headers = new Headers(init.headers);
        headers.set("Authorization", `Bearer ${current.accessToken}`);
        headers.set("Accept", accept);

        return requestResponse(path, {
          ...init,
          headers,
          signal: init.signal ?? AbortSignal.timeout(60_000),
        });
      };

      let response: Response;
      try {
        response = await send();
      } catch (error) {
        const failure = asApiError(error);
        if (failure.status !== 401) throw failure;

        current = await refreshOrClearSessionLocked();
        response = await send();
      }

      return {
        blob: await response.blob(),
        fileName: readDownloadName(response, fallbackName),
      };
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
