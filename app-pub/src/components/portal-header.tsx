"use client";

import Link from "next/link";
import { useState } from "react";

import { useAuth } from "@/hooks/use-auth";
import { logout } from "@/lib/auth-store";
import { asApiError } from "@/lib/http";

export default function PortalHeader() {
  const auth = useAuth();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function handleLogout() {
    setBusy(true);
    setError(null);

    try {
      await logout();
    } catch (failure) {
      setError(asApiError(failure).message);
    } finally {
      setBusy(false);
    }
  }

  return (
    <header className="sticky top-0 z-40 border-b border-blue-950 bg-slate-950 text-white shadow-lg">
      <div className="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-x-6 gap-y-3 px-4 py-3 sm:px-6">
        <Link href="/" className="flex items-center gap-3 font-semibold tracking-tight">
          <span className="grid size-9 place-items-center rounded-lg bg-blue-600 text-sm shadow-sm">
            PS
          </span>
          <span>Portal de servicii</span>
        </Link>

        <nav
          aria-label="Navigare principală"
          className="flex flex-1 flex-wrap items-center gap-x-1 gap-y-1 text-sm font-medium"
        >
          <Link href="/#servicii" className="rounded-md px-3 py-2 text-slate-200 transition hover:bg-slate-800 hover:text-white">
            Servicii
          </Link>

          <Link href="/#articole" className="rounded-md px-3 py-2 text-slate-200 transition hover:bg-slate-800 hover:text-white">
            Articole
          </Link>

          <Link href="/programari" className="rounded-md px-3 py-2 text-slate-200 transition hover:bg-slate-800 hover:text-white">
            Programări
          </Link>

          <Link href="/chestionare" className="rounded-md px-3 py-2 text-slate-200 transition hover:bg-slate-800 hover:text-white">
            Chestionare
          </Link>

          <Link href="/registre" className="rounded-md px-3 py-2 text-slate-200 transition hover:bg-slate-800 hover:text-white">
            Registre publice
          </Link>

          {auth.status === "authenticated" && auth.user.role === "Citizen" && (
            <>
              <Link href="/cereri" className="rounded-md px-3 py-2 text-slate-200 transition hover:bg-slate-800 hover:text-white">
                Cererile mele
              </Link>
              <Link href="/notificari" className="rounded-md px-3 py-2 text-slate-200 transition hover:bg-slate-800 hover:text-white">
                Notificări
              </Link>
            </>
          )}

          {auth.status === "authenticated" ? (
            <button
              type="button"
              disabled={busy}
              onClick={() => void handleLogout()}
              className="rounded-lg border border-slate-500 px-3 py-2 text-white transition hover:border-slate-300 hover:bg-slate-800 disabled:opacity-60"
            >
              {busy ? "Se închide…" : "Deconectare"}
            </button>
          ) : (
            <Link
              href="/login"
              className="rounded-lg bg-blue-600 px-4 py-2 text-white shadow-sm transition hover:bg-blue-500"
            >
              Autentificare
            </Link>
          )}
        </nav>

        {error && (
          <p role="alert" className="w-full rounded-md bg-red-950/60 px-3 py-2 text-sm text-red-100">
            {error}
          </p>
        )}
      </div>
    </header>
  );
}
