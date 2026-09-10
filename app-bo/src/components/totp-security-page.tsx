"use client";

import Link from "next/link";
import { useState, type FormEvent } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { useAuth } from "@/hooks/use-auth";
import { apiRequest } from "@/lib/auth-store";
import { ApiError, asApiError } from "@/lib/http";

type Enrollment = { secret: string; otpAuthUri: string };
type Enabled = { recoveryCodes: string[] };

const inputClass =
  "mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 " +
  "text-slate-950 focus:border-blue-600 focus:outline-none focus:ring-2 focus:ring-blue-200";

const primaryButton =
  "rounded-lg bg-blue-700 px-4 py-2 font-medium text-white hover:bg-blue-800 " +
  "disabled:cursor-wait disabled:opacity-60";

function ErrorMessage({ error }: { error: ApiError | null }) {
  if (!error) return null;
  return <ApiErrorPanel error={error} />;
}

export default function TotpSecurityPage() {
  const auth = useAuth();
  const [enrollment, setEnrollment] = useState<Enrollment | null>(null);
  const [recoveryCodes, setRecoveryCodes] = useState<string[] | null>(null);
  const [enableCode, setEnableCode] = useState("");
  const [disablePassword, setDisablePassword] = useState("");
  const [disableCode, setDisableCode] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  async function run(action: () => Promise<void>) {
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      await action();
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(false);
    }
  }

  async function beginSetup() {
    await run(async () => {
      const setup = await apiRequest<Enrollment>("/api/auth/totp/setup", {
        method: "POST",
      });
      setEnrollment(setup);
      setRecoveryCodes(null);
      setMessage("Adaugă secretul în aplicația de autentificare, apoi confirmă codul curent.");
    });
  }

  async function enable(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await run(async () => {
      const result = await apiRequest<Enabled>("/api/auth/totp/enable", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ code: enableCode }),
      });
      setEnrollment(null);
      setEnableCode("");
      setRecoveryCodes(result.recoveryCodes);
      setMessage("Autentificarea în doi pași este activă. Salvează recovery codes acum; nu vor mai fi afișate.");
    });
  }

  async function disable(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await run(async () => {
      await apiRequest<void>("/api/auth/totp/disable", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          currentPassword: disablePassword,
          code: disableCode,
        }),
      });
      setDisablePassword("");
      setDisableCode("");
      setRecoveryCodes(null);
      setMessage("Autentificarea în doi pași a fost dezactivată.");
    });
  }

  if (auth.status !== "authenticated") {
    return <p className="p-6">Autentifică-te ca administrator pentru a continua.</p>;
  }

  return (
    <main className="min-h-screen bg-slate-50 px-4 py-8 text-slate-950 sm:px-6">
      <div className="mx-auto max-w-3xl space-y-6">
        <Link href="/" className="text-sm font-medium text-blue-700 hover:underline">
          ← Înapoi la administrare
        </Link>

        <header>
          <p className="text-sm font-semibold uppercase tracking-wider text-blue-700">Securitate cont</p>
          <h1 className="mt-2 text-3xl font-bold">Autentificare în doi pași</h1>
          <p className="mt-3 text-slate-700">
            Protejează contul de administrator cu o aplicație compatibilă TOTP,
            precum Microsoft Authenticator, Google Authenticator sau 1Password.
          </p>
        </header>

        <ErrorMessage error={error} />
        {message && <p role="status" className="rounded-lg bg-green-50 p-4 text-green-900">{message}</p>}

        {!enrollment && !recoveryCodes && (
          <section className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-xl font-semibold">Activează TOTP</h2>
            <p className="mt-2 text-slate-700">
              La următoarea autentificare, parola nu va mai fi suficientă fără codul din aplicația ta.
            </p>
            <button type="button" disabled={busy} onClick={() => void beginSetup()} className={primaryButton + " mt-5"}>
              {busy ? "Se pregătește…" : "Configurează autentificarea în doi pași"}
            </button>
          </section>
        )}

        {enrollment && (
          <section className="space-y-5 rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
            <h2 className="text-xl font-semibold">1. Adaugă contul în aplicația de autentificare</h2>
            <p className="text-slate-700">
              Deschide linkul de mai jos pe dispozitivul care are aplicația TOTP sau introdu manual secretul.
            </p>
            <a href={enrollment.otpAuthUri} className="inline-block break-all text-blue-700 underline">
              Deschide aplicația de autentificare
            </a>
            <div>
              <label className="text-sm font-medium" htmlFor="totp-secret">Secret manual (afișat o singură dată)</label>
              <input id="totp-secret" readOnly value={enrollment.secret} className={inputClass + " font-mono"} />
            </div>

            <form onSubmit={enable} className="space-y-4 border-t border-slate-200 pt-5">
              <h2 className="text-xl font-semibold">2. Confirmă primul cod</h2>
              <div>
                <label className="text-sm font-medium" htmlFor="enable-totp-code">Cod cu 6 cifre</label>
                <input id="enable-totp-code" autoComplete="one-time-code" required disabled={busy} value={enableCode} onChange={event => setEnableCode(event.target.value)} className={inputClass} />
              </div>
              <button type="submit" disabled={busy} className={primaryButton}>{busy ? "Se verifică…" : "Activează TOTP"}</button>
            </form>
          </section>
        )}

        {recoveryCodes && (
          <section className="rounded-xl border-2 border-amber-300 bg-amber-50 p-6">
            <h2 className="text-xl font-semibold text-amber-950">Recovery codes — salvează-le acum</h2>
            <p className="mt-2 text-amber-950">Fiecare cod funcționează o singură dată. Nu va mai putea fi recuperat din aplicație.</p>
            <ul className="mt-4 grid grid-cols-2 gap-2 rounded-lg bg-white p-4 font-mono text-sm text-slate-950 sm:grid-cols-3">
              {recoveryCodes.map(code => <li key={code}>{code}</li>)}
            </ul>
            <button type="button" className="mt-5 rounded-lg border border-amber-700 px-4 py-2 font-medium text-amber-950 hover:bg-amber-100" onClick={() => setRecoveryCodes(null)}>
              Am salvat codurile
            </button>
          </section>
        )}

        <section className="rounded-xl border border-red-200 bg-white p-6 shadow-sm">
          <h2 className="text-xl font-semibold text-red-900">Dezactivează TOTP</h2>
          <p className="mt-2 text-slate-700">Este necesară parola curentă și un cod TOTP sau recovery code nefolosit.</p>
          <form onSubmit={disable} className="mt-5 grid gap-4 sm:grid-cols-2">
            <div>
              <label htmlFor="disable-password" className="text-sm font-medium">Parola curentă</label>
              <input id="disable-password" type="password" autoComplete="current-password" required disabled={busy} value={disablePassword} onChange={event => setDisablePassword(event.target.value)} className={inputClass} />
            </div>
            <div>
              <label htmlFor="disable-totp-code" className="text-sm font-medium">Cod TOTP sau recovery code</label>
              <input id="disable-totp-code" autoComplete="one-time-code" required disabled={busy} value={disableCode} onChange={event => setDisableCode(event.target.value)} className={inputClass} />
            </div>
            <button type="submit" disabled={busy} className="w-fit rounded-lg bg-red-700 px-4 py-2 font-medium text-white hover:bg-red-800 disabled:opacity-60">
              {busy ? "Se procesează…" : "Dezactivează"}
            </button>
          </form>
        </section>
      </div>
    </main>
  );
}
