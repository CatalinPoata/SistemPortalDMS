"use client";

import Link from "next/link";
import { useRef, useState, type FormEvent } from "react";

import ApiErrorPanel, {
  FieldErrors,
  fieldErrorsFor,
} from "@/components/api-error-panel";
import { resetPassword } from "@/lib/auth-store";
import { ApiError, asApiError, request } from "@/lib/http";

type Mode =
  | "register"
  | "resend-confirmation"
  | "forgot-password"
  | "reset-password";

type Field = {
  name: string;
  label: string;
  type: "text" | "email" | "password";
  autoComplete: string;
  minLength?: number;
  maxLength?: number;
  hint?: string;
};

type FormConfig = {
  title: string;
  description: string;
  button: string;
  fields: readonly Field[];
};

const emailField: Field = {
  name: "email",
  label: "E-mail",
  type: "email",
  autoComplete: "email",
  maxLength: 256,
};

const passwordHint =
  "Minimum 10 caractere și cel puțin 3 categorii: " +
  "litere mici, litere mari, cifre, caractere speciale.";

const forms: Record<Mode, FormConfig> = {
  register: {
    title: "Creează un cont",
    description:
      "Cont de cetățean. După înregistrare, confirmă adresa de e-mail.",
    button: "Creează contul",
    fields: [
      {
        name: "fullName",
        label: "Nume complet",
        type: "text",
        autoComplete: "name",
        minLength: 2,
        maxLength: 200,
      },
      emailField,
      {
        name: "password",
        label: "Parolă",
        type: "password",
        autoComplete: "new-password",
        minLength: 10,
        hint: passwordHint,
      },
    ],
  },

  "resend-confirmation": {
    title: "Retrimite confirmarea",
    description:
      "Solicită un nou link pentru confirmarea adresei de e-mail.",
    button: "Solicită linkul",
    fields: [emailField],
  },

  "forgot-password": {
    title: "Ai uitat parola?",
    description:
      "Introdu adresa de e-mail pentru a solicita instrucțiunile.",
    button: "Solicită resetarea",
    fields: [emailField],
  },

  "reset-password": {
    title: "Alege o parolă nouă",
    description:
      "Tokenul este preluat automat din linkul primit prin e-mail.",
    button: "Resetează parola",
    fields: [
      {
        name: "newPassword",
        label: "Parolă nouă",
        type: "password",
        autoComplete: "new-password",
        minLength: 10,
        hint: passwordHint,
      },
      {
        name: "confirmPassword",
        label: "Confirmă parola",
        type: "password",
        autoComplete: "new-password",
      },
    ],
  },
};

function value(data: FormData, name: string) {
  return String(data.get(name) ?? "");
}

async function submitAccountForm(mode: Mode, data: FormData) {
  if (mode === "reset-password") {
    const token = new URLSearchParams(
      window.location.search,
    ).get("token");

    if (!token?.trim()) {
      throw new ApiError(400, {
        detail:
          "Linkul nu conține un token. Solicită un nou link de resetare.",
      });
    }

    return resetPassword(
      token,
      value(data, "newPassword"),
      value(data, "confirmPassword"),
    );
  }

  const email = value(data, "email").trim();

  const body =
    mode === "register"
      ? {
          email,
          fullName: value(data, "fullName").trim(),
          password: value(data, "password"),
        }
      : { email };

  return request<{ message: string }>(`/api/auth/${mode}`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
}

export default function AccountForm({ mode }: { mode: Mode }) {
  const config = forms[mode];
  const running = useRef(false);

  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<ApiError | null>(null);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (running.current || message !== null) return;

    const form = event.currentTarget;
    const data = new FormData(form);

    running.current = true;
    setBusy(true);
    setError(null);

    try {
      const result = await submitAccountForm(mode, data);

      form.reset();
      setMessage(result.message);

      if (mode === "reset-password") {
        window.history.replaceState(
          null,
          "",
          window.location.pathname,
        );
      }
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      running.current = false;
      setBusy(false);
    }
  }

  return (
    <main className="flex min-h-screen items-center justify-center bg-slate-100 p-4 text-slate-950">
      <section className="w-full max-w-lg space-y-6 rounded-2xl border border-slate-200 bg-white p-6 shadow-sm sm:p-8">
        <h1 className="text-2xl font-semibold">
          {config.title}
        </h1>

        <p>{config.description}</p>

        {error && <ApiErrorPanel error={error} showFieldErrors={false} />}

        {message !== null ? (
          <p
            role="status"
            className="rounded-lg bg-green-50 p-4 text-green-900"
          >
            {message}
          </p>
        ) : (
          <form onSubmit={handleSubmit} aria-busy={busy}>
            <fieldset disabled={busy} className="space-y-5">
              <legend className="sr-only">
                {config.title}
              </legend>

              {config.fields.map(field => {
                const fieldErrors = fieldErrorsFor(error, field.name);

                return (
                <div key={field.name}>
                  <label
                    htmlFor={field.name}
                    className="text-sm font-medium"
                  >
                    {field.label}
                  </label>

                  <input
                    id={field.name}
                    name={field.name}
                    type={field.type}
                    autoComplete={field.autoComplete}
                    minLength={field.minLength}
                    maxLength={field.maxLength}
                    required
                    aria-describedby={
                      [
                        field.hint ? `${field.name}-hint` : null,
                        fieldErrors.length > 0
                          ? `${field.name}-errors`
                          : null,
                      ]
                        .filter(Boolean)
                        .join(" ") || undefined
                    }
                    aria-invalid={fieldErrors.length > 0}
                    onChange={() => setError(null)}
                    className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-base text-slate-950 focus:outline-2 focus:outline-blue-700 aria-[invalid=true]:border-red-600"
                  />

                  {field.hint && (
                    <p
                      id={`${field.name}-hint`}
                      className="mt-1 text-sm text-slate-600"
                    >
                      {field.hint}
                    </p>
                  )}

                  <div id={`${field.name}-errors`}>
                    <FieldErrors messages={fieldErrors} />
                  </div>
                </div>
                );
              })}

              <button
                type="submit"
                disabled={busy}
                className="w-full rounded-lg bg-blue-700 px-4 py-2 font-medium text-white hover:bg-blue-800 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-700 disabled:opacity-60"
              >
                {busy ? "Se procesează…" : config.button}
              </button>
            </fieldset>
          </form>
        )}

        <Link
          href="/login"
          className="block text-blue-700 underline"
        >
          Înapoi la autentificare
        </Link>
      </section>
    </main>
  );
}
