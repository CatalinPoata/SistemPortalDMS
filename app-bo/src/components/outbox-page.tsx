"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { apiRequest } from "@/lib/auth-store";
import { ApiError, asApiError } from "@/lib/http";

type OutboxMessage = {
  id: string;
  aggregateType: string;
  aggregateId: string;
  eventType: string;
  status: "Pending" | "Failed" | "Delivered";
  attempts: number;
  nextAttemptAt: string;
  lastError: string | null;
  deliveredAt: string | null;
  createdAt: string;
};

const buttonClass = "rounded-md bg-blue-700 px-3 py-2 text-sm font-medium text-white hover:bg-blue-800 disabled:opacity-60";

function ErrorMessage({ error }: { error: ApiError | null }) {
  if (!error) return null;
  return <ApiErrorPanel error={error} />;
}

export default function OutboxPage() {
  const [status, setStatus] = useState<"Pending" | "Failed">("Failed");
  const [messages, setMessages] = useState<OutboxMessage[]>([]);
  const [loading, setLoading] = useState(true);
  const [retrying, setRetrying] = useState<string | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [revision, setRevision] = useState(0);

  useEffect(() => {
    let current = true;
    void apiRequest<OutboxMessage[]>(`/api/integration/outbox?status=${status}`)
      .then(result => { if (current) { setMessages(result); setError(null); } })
      .catch(failure => { if (current) setError(asApiError(failure)); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [status, revision]);

  async function retry(message: OutboxMessage) {
    setRetrying(message.id);
    setError(null);
    try {
      await apiRequest<OutboxMessage>(`/api/integration/outbox/${message.id}/retry`, { method: "POST" });
      setStatus("Pending");
      setRevision(value => value + 1);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setRetrying(null);
    }
  }

  return <main className="min-h-screen bg-slate-100 p-4 text-slate-950 sm:p-6"><div className="mx-auto max-w-7xl space-y-6">
    <header className="flex flex-wrap items-center justify-between gap-3"><div><p className="text-sm font-semibold uppercase tracking-wider text-blue-700">Portal de servicii</p><h1 className="text-2xl font-semibold">Monitorizare outbox</h1></div><Link href="/" className="text-blue-700 underline">Înapoi la administrare</Link></header>
    <p className="max-w-3xl text-slate-700">Mesajele reprezintă operații Portal → DMS. Un mesaj eșuat poate fi relansat manual; procesarea normală rămâne responsabilitatea workerului de retry.</p>
    <ErrorMessage error={error} />
    <div className="flex flex-wrap gap-2"><button className={status === "Failed" ? buttonClass : "rounded-md border border-slate-300 px-3 py-2 text-sm"} onClick={() => setStatus("Failed")}>Eșuate</button><button className={status === "Pending" ? buttonClass : "rounded-md border border-slate-300 px-3 py-2 text-sm"} onClick={() => setStatus("Pending")}>În așteptare</button><button className="rounded-md border border-slate-300 px-3 py-2 text-sm" onClick={() => setRevision(value => value + 1)}>Reîncarcă</button></div>
    <section className="overflow-x-auto rounded-xl bg-white shadow-sm"><table className="min-w-full text-left text-sm"><thead className="bg-slate-50"><tr>{["Eveniment", "Agregat", "Încercări", "Următoarea încercare", "Ultima eroare", "Acțiune"].map(label => <th key={label} className="p-3">{label}</th>)}</tr></thead><tbody>
      {messages.map(message => <tr key={message.id} className="border-t border-slate-200"><td className="p-3">{message.eventType}</td><td className="p-3"><span className="block">{message.aggregateType}</span><span className="block break-all text-xs text-slate-600">{message.aggregateId}</span></td><td className="p-3">{message.attempts}</td><td className="p-3">{new Date(message.nextAttemptAt).toLocaleString("ro-RO")}</td><td className="max-w-sm break-words p-3 text-red-800">{message.lastError ?? "—"}</td><td className="p-3">{message.status === "Failed" && <button className={buttonClass} disabled={retrying !== null} onClick={() => void retry(message)}>{retrying === message.id ? "Relansăm…" : "Reîncearcă"}</button>}</td></tr>)}
      {!loading && messages.length === 0 && <tr><td colSpan={6} className="p-4 text-slate-600">Nu există mesaje {status === "Failed" ? "eșuate" : "în așteptare"}.</td></tr>}
    </tbody></table></section>
    {loading && <p role="status">Încărcăm mesajele…</p>}
  </div></main>;
}
