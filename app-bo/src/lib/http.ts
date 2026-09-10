import "client-only";

export type ApiProblem = {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
};

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly problem: ApiProblem,
  ) {
    super(problem.detail ?? problem.title ?? "Cererea a eșuat.");
    this.name = "ApiError";
  }
}

export function asApiError(error: unknown): ApiError {
  return error instanceof ApiError
    ? error
    : new ApiError(0, {
        detail:
          "Nu putem contacta serverul. Verifică conexiunea și încearcă din nou.",
      });
}

export async function request<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  if (!path.startsWith("/api/")) {
    throw new Error("Sunt permise numai rute locale /api/.");
  }

  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json, application/problem+json");

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

  if (!response.ok) {
    const problem: ApiProblem = await response.json().catch(() => ({
      detail: "Serverul nu a putut procesa cererea.",
    }));

    throw new ApiError(response.status, {
      ...problem,
      traceId:
        problem.traceId ??
        response.headers.get("X-Trace-Id") ??
        undefined,
    });
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

export async function requestResponse(
  path: string,
  init: RequestInit = {},
): Promise<Response> {
  if (!path.startsWith("/api/")) {
    throw new Error("Sunt permise numai rute locale /api/.");
  }

  const headers = new Headers(init.headers);
  if (!headers.has("Accept")) {
    headers.set("Accept", "application/json, application/problem+json");
  }

  try {
    const response = await fetch(path, {
      ...init,
      headers,
      credentials: "same-origin",
      cache: "no-store",
      redirect: "error",
    });

    if (response.ok) {
      return response;
    }

    const problem: ApiProblem = await response.json().catch(() => ({
      detail: "Serverul nu a putut procesa cererea.",
    }));

    throw new ApiError(response.status, {
      ...problem,
      traceId:
        problem.traceId ??
        response.headers.get("X-Trace-Id") ??
        undefined,
    });
  } catch (error) {
    throw asApiError(error);
  }
}
