"use client";

import Link from "next/link";
import { useState } from "react";

import ApiErrorMessage from "@/components/api-error-message";
import { useApiQuery } from "@/hooks/use-api-query";
import { useAuth } from "@/hooks/use-auth";
import { apiRequest } from "@/lib/auth-store";
import { asApiError, type ApiError } from "@/lib/http";

type OutboxStatus = "Pending" | "Failed";

type OutboxMessage = {
  id: string;
  aggregateType: string;
  aggregateId: string;
  eventType: string;
  status: OutboxStatus | "Delivered";
  attempts: number;
  nextAttemptAt: string;
  lastError: string | null;
  deliveredAt: string | null;
  createdAt: string;
};

const buttonClass =
  "rounded-lg bg-blue-700 px-4 py-2 text-white hover:bg-blue-800 " +
  "disabled:cursor-not-allowed disabled:opacity-50 " +
  "focus-visible:outline-2 focus-visible:outline-offset-2 " +
  "focus-visible:outline-blue-700";

const secondaryButtonClass =
  "rounded-lg border border-slate-300 px-4 py-2 text-slate-900 " +
  "hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50 " +
  "focus-visible:outline-2 focus-visible:outline-offset-2 " +
  "focus-visible:outline-blue-700";

const dateTimeFormatter = new Intl.DateTimeFormat("ro-RO", {
  dateStyle: "short",
  timeStyle: "medium",
});

function dateTime(value: string | null) {
  if (!value) return "—";

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime())
    ? value
    : `${dateTimeFormatter.format(parsed)} UTC`;
}

function statusLabel(status: OutboxStatus) {
  return status === "Failed" ? "Eșuate" : "În așteptare";
}

export default function OutboxPage() {
  const auth = useAuth();

  if (auth.status === "loading") {
    return <PageFrame><p role="status">Verificăm sesiunea…</p></PageFrame>;
  }

  if (auth.status === "error") {
    return <PageFrame><ApiErrorMessage error={auth.error} /></PageFrame>;
  }

  if (auth.status !== "authenticated") {
    return <PageFrame><p>Autentifică-te ca administrator pentru a monitoriza outbox-ul.</p></PageFrame>;
  }

  if (auth.user.role !== "Admin") {
    return <PageFrame><p>Acces permis numai administratorilor.</p></PageFrame>;
  }

  return <OutboxWorkspace />;
}

function PageFrame({ children }: { children: React.ReactNode }) {
  return (
    <main className="min-h-screen bg-slate-100 p-4 text-slate-950 sm:p-6">
      <div className="mx-auto max-w-7xl space-y-6">
        <header className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="text-sm font-semibold uppercase tracking-wider text-blue-700">DMS</p>
            <h1 className="text-2xl font-semibold">Monitorizare outbox</h1>
          </div>
          <Link href="/registratura" className="text-blue-700 underline">
            Înapoi la registratură
          </Link>
        </header>
        {children}
      </div>
    </main>
  );
}

function OutboxWorkspace() {
  const [status, setStatus] = useState<OutboxStatus>("Failed");
  const [revision, setRevision] = useState(0);
  const [retryingId, setRetryingId] = useState<string | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const outbox = useApiQuery<OutboxMessage[]>(
    `/api/integration/outbox?status=${status}`,
    revision,
  );

  function reload() {
    setError(null);
    setMessage(null);
    setRevision(value => value + 1);
  }

  async function retry(item: OutboxMessage) {
    if (
      retryingId ||
      !window.confirm("Mesajul va fi relansat la următoarea execuție a procesatorului outbox. Continui?")
    ) {
      return;
    }

    setRetryingId(item.id);
    setError(null);
    setMessage(null);

    try {
      await apiRequest<OutboxMessage>(
        `/api/integration/outbox/${item.id}/retry`,
        { method: "POST" },
      );
      setMessage("Mesajul a fost mutat în starea «În așteptare».");
      setRevision(value => value + 1);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setRetryingId(null);
    }
  }

  return (
    <PageFrame>
      <section className="space-y-4 rounded-xl bg-white p-4 shadow-sm sm:p-6">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <div>
            <h2 className="text-lg font-semibold">Mesaje de integrare</h2>
            <p className="mt-1 max-w-3xl text-sm text-slate-600">
              Sunt afișate cel mult 200 de mesaje, ordonate după următoarea
              încercare. Reîncercarea manuală este disponibilă numai pentru
              mesajele eșuate.
            </p>
          </div>

          <button type="button" className={secondaryButtonClass} onClick={reload}>
            Reîncarcă
          </button>
        </div>

        <div className="flex flex-wrap gap-2" role="group" aria-label="Stare mesaje outbox">
          {(["Failed", "Pending"] as const).map(value => (
            <button
              key={value}
              type="button"
              onClick={() => {
                setStatus(value);
                setError(null);
                setMessage(null);
              }}
              className={
                "rounded-lg px-4 py-2 font-medium focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-700 " +
                (status === value
                  ? "bg-blue-700 text-white"
                  : "border border-slate-300 text-slate-900 hover:bg-slate-100")
              }
              aria-pressed={status === value}
            >
              {statusLabel(value)}
            </button>
          ))}
        </div>

        {error && <ApiErrorMessage error={error} />}
        {outbox.error && <ApiErrorMessage error={outbox.error} />}
        {message && <p role="status" className="rounded-lg bg-emerald-50 p-3 text-emerald-900">{message}</p>}

        {outbox.loading ? (
          <p role="status">Încărcăm mesajele outbox…</p>
        ) : outbox.data && outbox.data.length === 0 ? (
          <p className="rounded-lg border border-dashed border-slate-300 p-6 text-slate-600">
            Nu există mesaje {statusLabel(status).toLocaleLowerCase("ro-RO")}.
          </p>
        ) : outbox.data ? (
          <div className="overflow-x-auto rounded-lg border border-slate-200">
            <table className="w-full min-w-[70rem] text-left text-sm">
              <thead className="bg-slate-100 text-slate-700">
                <tr>
                  <th scope="col" className="p-3">Eveniment</th>
                  <th scope="col" className="p-3">Agregat</th>
                  <th scope="col" className="p-3">Creat</th>
                  <th scope="col" className="p-3">Încercări</th>
                  <th scope="col" className="p-3">Următoarea încercare</th>
                  <th scope="col" className="p-3">Ultima eroare</th>
                  <th scope="col" className="p-3"><span className="sr-only">Acțiuni</span></th>
                </tr>
              </thead>
              <tbody>
                {outbox.data.map(item => (
                  <tr key={item.id} className="border-t border-slate-200 align-top">
                    <td className="p-3 font-medium">{item.eventType}</td>
                    <td className="p-3">
                      <span className="block">{item.aggregateType}</span>
                      <code className="block max-w-56 break-all text-xs text-slate-600">{item.aggregateId}</code>
                    </td>
                    <td className="p-3 whitespace-nowrap">{dateTime(item.createdAt)}</td>
                    <td className="p-3 text-center">{item.attempts}</td>
                    <td className="p-3 whitespace-nowrap">{dateTime(item.nextAttemptAt)}</td>
                    <td className="max-w-md p-3">
                      {item.lastError ? (
                        <details>
                          <summary className="cursor-pointer text-red-800 underline">Vezi eroarea</summary>
                          <pre className="mt-2 max-h-40 overflow-auto whitespace-pre-wrap break-words rounded bg-red-50 p-2 text-xs text-red-950">{item.lastError}</pre>
                        </details>
                      ) : "—"}
                    </td>
                    <td className="p-3 text-right">
                      {item.status === "Failed" && (
                        <button
                          type="button"
                          className={buttonClass}
                          disabled={retryingId !== null}
                          onClick={() => void retry(item)}
                        >
                          {retryingId === item.id ? "Relansăm…" : "Reîncearcă"}
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : null}
      </section>
    </PageFrame>
  );
}
