"use client";

import { useEffect, useState } from "react";
import { apiGet } from "@/lib/auth-store";
import { ApiError, asApiError } from "@/lib/http";

export function useApiQuery<T>(
  path: string,
  revision = 0,
) {
  const key = `${revision}:${path}`;

  const [result, setResult] = useState<{
    key: string;
    data: T | null;
    error: ApiError | null;
  } | null>(null);

  useEffect(() => {
    const controller = new AbortController();

    apiGet<T>(path, controller.signal).then(
      data => {
        if (!controller.signal.aborted) {
          setResult({
            key,
            data,
            error: null,
          });
        }
      },
      error => {
        if (!controller.signal.aborted) {
          setResult({
            key,
            data: null,
            error: asApiError(error),
          });
        }
      },
    );

    return () => controller.abort();
  }, [path, key]);

  const current =
    result?.key === key ? result : null;

  return {
    data: current?.data ?? null,
    error: current?.error ?? null,
    loading: current === null,
  };
}
