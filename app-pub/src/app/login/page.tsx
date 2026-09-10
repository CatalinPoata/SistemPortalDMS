"use client";

import {
  Suspense,
  useEffect,
  useRef,
  useState,
  type FormEvent,
} from "react";
import { useRouter, useSearchParams } from "next/navigation";

import ApiErrorPanel, {
  FieldErrors,
  fieldErrorsFor,
} from "@/components/api-error-panel";
import { useAuth } from "@/hooks/use-auth";
import {
  login,
  logout,
  restoreSession,
} from "@/lib/auth-store";
import { ApiError, asApiError } from "@/lib/http";
import { authConfig, roleLabels } from "@/lib/auth-config";
import Link from "next/link";

const inputClass =
  "mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 " +
  "text-base text-slate-950 focus:border-blue-600 focus:outline-none " +
  "focus:ring-2 focus:ring-blue-200";

const buttonClass =
  "rounded-lg bg-blue-700 px-4 py-2 text-base font-medium text-white " +
  "hover:bg-blue-800 focus-visible:outline-2 focus-visible:outline-offset-2 " +
  "focus-visible:outline-blue-700 disabled:cursor-wait disabled:opacity-60";

function getSafeNextPath(value: string | null) {
  if (value?.startsWith("/") && !value.startsWith("//")) {
    return value;
  }

  return "/";
}

function ErrorMessage({ error }: { error: ApiError | null }) {
  if (!error) return null;
  return <ApiErrorPanel error={error} showFieldErrors={false} />;
}

function LoginContent() {
  const auth = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const nextPath = getSafeNextPath(searchParams.get("next"));

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  const formError = error ?? auth.error;
  const emailErrors = fieldErrorsFor(formError, "email");
  const passwordErrors = fieldErrorsFor(formError, "password");

  const running = useRef(false);

  useEffect(() => {
    if (auth.status === "authenticated") {
      router.replace(nextPath);
    }
  }, [auth.status, nextPath, router]);

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
      router.replace(nextPath);
    });
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-100 p-4 text-slate-950">
      <section
        aria-labelledby="page-title"
        className="w-full max-w-lg space-y-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8"
      >
        <header className="space-y-2">
          <p className="text-sm font-semibold uppercase tracking-wider text-blue-700">
            {authConfig.sectionLabel}
          </p>

          <h1 id="page-title" className="text-2xl font-semibold">
            {authConfig.title}
          </h1>
        </header>

        <ErrorMessage error={formError} />

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
            <p>{authConfig.loginDescription}</p>

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
                aria-invalid={emailErrors.length > 0}
                aria-describedby={
                  emailErrors.length > 0 ? "email-errors" : undefined
                }
                onChange={event => {
                  setEmail(event.target.value);
                  setError(null);
                }}
                className={`${inputClass} aria-[invalid=true]:border-red-600`}
              />

              <div id="email-errors">
                <FieldErrors messages={emailErrors} />
              </div>
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
                aria-invalid={passwordErrors.length > 0}
                aria-describedby={
                  passwordErrors.length > 0 ? "password-errors" : undefined
                }
                onChange={event => {
                  setPassword(event.target.value);
                  setError(null);
                }}
                className={`${inputClass} aria-[invalid=true]:border-red-600`}
              />

              <div id="password-errors">
                <FieldErrors messages={passwordErrors} />
              </div>
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

        {auth.status === "authenticated" && (
          <div className="space-y-5">
            <div>
              <h2 className="text-xl font-semibold">
                {auth.user.fullName}
              </h2>

              <p className="break-all text-slate-600">
                {auth.user.email}
              </p>

              <p className="mt-2 text-sm">
                Rol: {roleLabels[auth.user.role]}
              </p>
            </div>

            <div className="flex flex-wrap gap-3">
              <button
                disabled={busy}
                className={buttonClass}
                onClick={() => void run(restoreSession)}
              >
                Verifică sesiunea
              </button>

              <button
                disabled={busy}
                className={buttonClass}
                onClick={() => void run(logout)}
              >
                Deconectare
              </button>
            </div>
          </div>
        )}
        {auth.status === "anonymous" && (
            <nav
                aria-label="Administrarea contului"
                className="flex flex-col gap-3 text-sm"
            >
                <Link href="/register" className="text-blue-700 underline">
                Nu ai cont? Înregistrează-te
                </Link>

                <Link
                href="/resend-confirmation"
                className="text-blue-700 underline"
                >
                Retrimite e-mailul de confirmare
                </Link>

                <Link
                href="/forgot-password"
                className="text-blue-700 underline"
                >
                Ai uitat parola?
                </Link>
            </nav>
        )}
      </section>
    </main>
  );
}

export default function LoginPage() {
  return (
    <Suspense
      fallback={
        <main className="flex min-h-screen items-center justify-center bg-slate-100 p-4 text-slate-950">
          <p role="status">Se încarcă pagina de autentificare…</p>
        </main>
      }
    >
      <LoginContent />
    </Suspense>
  );
}
