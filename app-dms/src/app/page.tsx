"use client";

import { useRef, useState, type FormEvent } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { useAuth } from "@/hooks/use-auth";
import {
  cancelTotpLogin,
  login,
  logout,
  restoreSession,
  verifyTotpLogin,
} from "@/lib/auth-store";
import { ApiError, asApiError } from "@/lib/http";
import Link from "next/link";

const inputClass =
  "mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 " +
  "text-base text-slate-950 focus:border-blue-600 focus:outline-none " +
  "focus:ring-2 focus:ring-blue-200";

const buttonClass =
  "rounded-lg bg-blue-700 px-4 py-2 text-base font-medium text-white " +
  "hover:bg-blue-800 focus-visible:outline-2 focus-visible:outline-offset-2 " +
  "focus-visible:outline-blue-700 disabled:cursor-wait disabled:opacity-60";

function ErrorMessage({ error }: { error: ApiError | null }) {
  if (!error) return null;
  return <ApiErrorPanel error={error} />;
}

export default function Home() {
  const auth = useAuth();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [totpCode, setTotpCode] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  const running = useRef(false);

  async function run(action: () => Promise<void>) {
    if (running.current) return;

    running.current = true;
    setBusy(true);
    setError(null);

    try {
      await action();
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      running.current = false;
      setBusy(false);
    }
  }

  async function handleLogin(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    await run(async () => {
      await login(email, password);
      setPassword("");
    });
  }

  async function handleTotpVerification(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (auth.status !== "totp") return;

    await run(async () => {
      await verifyTotpLogin(auth.challenge, totpCode);
      setTotpCode("");
    });
  }

  return (
    <main className={auth.status === "authenticated"
      ? "min-h-full bg-slate-100 p-4 text-slate-950 sm:p-6"
      : "flex min-h-full items-center justify-center bg-slate-100 p-4 text-slate-950"}>
      <section
        aria-labelledby="page-title"
        className={auth.status === "authenticated"
          ? "mx-auto max-w-7xl space-y-6"
          : "w-full max-w-lg space-y-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8"}
      >
        <header className="space-y-2">
          <p className="text-sm font-semibold uppercase tracking-wider text-blue-700">
            {auth.status === "authenticated" ? "Spațiu operațional" : "Registratură"}
          </p>

          <h1 id="page-title" className="text-2xl font-semibold">
            {auth.status === "authenticated" ? "Panou registratură" : "DMS"}
          </h1>
        </header>

        <ErrorMessage error={error ?? auth.error} />

        {auth.status === "loading" && (
          <p role="status">Verificăm sesiunea…</p>
        )}

        {auth.status === "error" && (
          <div className="flex flex-wrap gap-3">
            <button
              disabled={busy}
              className={buttonClass}
              onClick={() => void run(restoreSession)}
            >
              Reîncearcă
            </button>

            <button
              disabled={busy}
              className={buttonClass}
              onClick={() => void run(logout)}
            >
              Închide sesiunea
            </button>
          </div>
        )}

        {auth.status === "anonymous" && (
          <form
            onSubmit={handleLogin}
            className="space-y-5"
            aria-busy={busy}
          >
            <p>Autentificare pentru funcționari și administratori.</p>

            <div>
              <label
                htmlFor="email"
                className="text-sm font-medium"
              >
                E-mail
              </label>

              <input
                id="email"
                name="email"
                type="email"
                autoComplete="username"
                required
                disabled={busy}
                value={email}
                onChange={event => setEmail(event.target.value)}
                className={inputClass}
              />
            </div>

            <div>
              <label
                htmlFor="password"
                className="text-sm font-medium"
              >
                Parolă
              </label>

              <input
                id="password"
                name="password"
                type="password"
                autoComplete="current-password"
                required
                disabled={busy}
                value={password}
                onChange={event => setPassword(event.target.value)}
                className={inputClass}
              />
            </div>

            <button
              type="submit"
              disabled={busy}
              className={buttonClass + " w-full"}
            >
              {busy ? "Se procesează…" : "Autentificare"}
            </button>
          </form>
        )}

        {auth.status === "totp" && (
          <form
            onSubmit={handleTotpVerification}
            className="space-y-5"
            aria-busy={busy}
          >
            <div className="space-y-2">
              <h2 className="text-xl font-semibold">
                Verificare în doi pași
              </h2>
              <p>
                Introdu codul din aplicația de autentificare sau unul dintre
                codurile de recuperare.
              </p>
              <p className="text-sm text-slate-600">
                Cererea expiră la {new Date(auth.expiresAt).toLocaleTimeString("ro-RO")}.
              </p>
            </div>

            <div>
              <label htmlFor="totp-code" className="text-sm font-medium">
                Cod TOTP sau recovery code
              </label>
              <input
                id="totp-code"
                name="totpCode"
                autoComplete="one-time-code"
                required
                disabled={busy}
                value={totpCode}
                onChange={event => setTotpCode(event.target.value)}
                className={inputClass}
              />
            </div>

            <div className="flex flex-wrap gap-3">
              <button type="submit" disabled={busy} className={buttonClass}>
                {busy ? "Se verifică…" : "Confirmă"}
              </button>
              <button
                type="button"
                disabled={busy}
                className="rounded-lg border border-slate-300 px-4 py-2 text-slate-900 hover:bg-slate-100"
                onClick={() => {
                  setTotpCode("");
                  cancelTotpLogin();
                }}
              >
                Înapoi
              </button>
            </div>
          </form>
        )}

        {auth.status === "authenticated" && (
          <div className="space-y-6">
            <section className="rounded-2xl bg-gradient-to-r from-blue-700 to-indigo-700 p-6 text-white shadow-lg sm:p-8">
              <p className="text-sm font-medium text-blue-100">Sesiune operațională activă</p>
              <div className="mt-2 flex flex-wrap items-end justify-between gap-4">
                <div>
                  <h2 className="text-2xl font-semibold">Bun venit, {auth.user.fullName}</h2>
                  <p className="mt-1 text-blue-100">{auth.user.email} · {auth.user.role === "Admin" ? "Administrator" : "Funcționar"}</p>
                </div>
                <Link href="/registratura/noua" className="rounded-lg bg-white px-4 py-2 font-medium text-blue-800 shadow-sm transition hover:bg-blue-50">
                  Înregistrează poziție nouă
                </Link>
              </div>
            </section>

            <section>
              <div className="mb-3 flex items-center justify-between gap-4">
                <h2 className="text-lg font-semibold">Activitate curentă</h2>
                <p className="text-sm text-slate-600">Acces rapid la operațiunile de registratură.</p>
              </div>
              <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
                <Link href="/registratura" className="rounded-xl border border-blue-100 bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-blue-300 hover:shadow-md"><span className="text-2xl">▤</span><h3 className="mt-3 font-semibold">Registratură</h3><p className="mt-1 text-sm text-slate-600">Caută, filtrează și gestionează pozițiile.</p></Link>
                <Link href="/registratura/noua" className="rounded-xl border border-blue-100 bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-blue-300 hover:shadow-md"><span className="text-2xl">＋</span><h3 className="mt-3 font-semibold">Poziție nouă</h3><p className="mt-1 text-sm text-slate-600">Înregistrează rapid o intrare sau ieșire.</p></Link>
                <Link href="/registre" className="rounded-xl border border-blue-100 bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-blue-300 hover:shadow-md"><span className="text-2xl">▦</span><h3 className="mt-3 font-semibold">Registre & documente</h3><p className="mt-1 text-sm text-slate-600">Consultă registrele și tipurile de document.</p></Link>
                <Link href="/rapoarte" className="rounded-xl border border-indigo-100 bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-indigo-300 hover:shadow-md"><span className="text-2xl">▥</span><h3 className="mt-3 font-semibold">Rapoarte</h3><p className="mt-1 text-sm text-slate-600">Preview, PDF și CSV pentru activitate.</p></Link>
                <Link href="/securitate" className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-blue-300 hover:shadow-md"><span className="text-2xl">◈</span><h3 className="mt-3 font-semibold">Securitate / TOTP</h3><p className="mt-1 text-sm text-slate-600">Activează autentificarea în doi pași și gestionează codurile de recuperare.</p></Link>
                {auth.user.role === "Admin" && <Link href="/compartimente" className="rounded-xl border border-amber-100 bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:border-amber-300 hover:shadow-md"><span className="text-2xl">⌘</span><h3 className="mt-3 font-semibold">Compartimente</h3><p className="mt-1 text-sm text-slate-600">Administrează structura organizațională.</p></Link>}
              </div>
            </section>

            <section className="flex flex-wrap items-center gap-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
              <Link href="/registratura" className="rounded-lg bg-slate-800 px-4 py-2 text-sm font-medium text-white hover:bg-slate-950">Deschide registratura</Link>
              <Link href="/registre" className="rounded-lg bg-slate-800 px-4 py-2 text-sm font-medium text-white hover:bg-slate-950">Registre & documente</Link>
              <Link href="/rapoarte" className="rounded-lg bg-slate-800 px-4 py-2 text-sm font-medium text-white hover:bg-slate-950">Deschide rapoarte</Link>
              <Link href="/securitate" className="rounded-lg bg-slate-800 px-4 py-2 text-sm font-medium text-white hover:bg-slate-950">Securitate / TOTP</Link>
              <span className="mx-auto" />
              <button disabled={busy} className="rounded-lg border border-blue-300 px-4 py-2 text-sm font-medium text-blue-800 hover:bg-blue-50" onClick={() => void run(restoreSession)}>Reînnoiește sesiunea</button>
              <button disabled={busy} className="rounded-lg bg-red-700 px-4 py-2 text-sm font-medium text-white hover:bg-red-800" onClick={() => void run(logout)}>Deconectare</button>
            </section>
          </div>
        )}
      </section>
    </main>
  );
}
