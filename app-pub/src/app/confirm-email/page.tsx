"use client";

import Link from "next/link";
import { useRef, useState } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { ApiError, asApiError, request } from "@/lib/http";

export default function ConfirmEmailPage() {
  const running = useRef(false);

  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<ApiError | null>(null);

  async function confirm() {
    if (running.current || message) return;

    running.current = true;
    setBusy(true);
    setError(null);

    try {
      const token = new URLSearchParams(
        window.location.search,
      ).get("token");

      if (!token) {
        throw new ApiError(400, {
          detail: "Linkul de confirmare nu conține un token.",
        });
      }

      const result = await request<{ message: string }>(
        "/api/auth/confirm-email",
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({ token }),
        },
      );

      setMessage(result.message);

      window.history.replaceState(
        null,
        "",
        window.location.pathname,
      );
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      running.current = false;
      setBusy(false);
    }
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-100 p-4 text-slate-950">
      <section className="w-full max-w-lg space-y-5 rounded-xl bg-white p-6">
        <h1 className="text-2xl font-semibold">
          Confirmare e-mail
        </h1>

        {message && <p role="status">{message}</p>}

        {error && <ApiErrorPanel error={error} />}

        {!message && (
          <button
            type="button"
            disabled={busy}
            onClick={() => void confirm()}
            className="rounded-lg bg-blue-700 px-4 py-2 text-white disabled:opacity-60"
          >
            {busy ? "Se confirmă…" : "Confirmă adresa"}
          </button>
        )}

        <Link
          href="/login"
          className="block text-blue-700 underline"
        >
          Mergi la autentificare
        </Link>
      </section>
    </main>
  );
}
