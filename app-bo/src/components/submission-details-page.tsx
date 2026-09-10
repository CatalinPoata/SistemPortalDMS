"use client";

import Link from "next/link";
import { useEffect, useMemo, useState } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { apiRequest } from "@/lib/auth-store";
import { ApiError, asApiError } from "@/lib/http";

type FormField = {
  key: string;
  label: string;
  type: string;
};

type SubmissionDetails = {
  id: string;
  externalId: string;
  serviceCode: string;
  serviceTitle: string;
  schemaVersion: number;
  applicantName: string;
  applicantEmail: string;
  status: string;
  statusDetails: string | null;
  submittedAt: string;
  registryNumber: number | null;
  registryYear: number | null;
  registryDisplayNumber: string | null;
  registeredAt: string | null;
  dmsEntryId: string | null;
  formSnapshot: { sections?: Array<{ fields?: FormField[] }> };
  values: Record<string, unknown>;
  files: Array<{
    id: string;
    fieldKey: string | null;
    kind: string;
    originalName: string;
    contentType: string;
    sizeBytes: number;
  }>;
  events: Array<{
    id: string;
    occurredAt: string;
    type: string;
    message: string;
  }>;
};

function formatDate(value: string | null) {
  return value ? new Date(value).toLocaleString("ro-RO") : "—";
}

function formatBytes(bytes: number) {
  return new Intl.NumberFormat("ro-RO", {
    maximumFractionDigits: 1,
    style: "unit",
    unit: "megabyte",
    unitDisplay: "narrow",
  }).format(bytes / 1024 / 1024);
}

function displayValue(value: unknown) {
  if (value === null || value === undefined || value === "") return "—";
  if (typeof value === "boolean") return value ? "Da" : "Nu";
  if (Array.isArray(value)) return value.join(", ") || "—";
  if (typeof value === "object") return JSON.stringify(value);
  return String(value);
}

export default function SubmissionDetailsPage({
  submissionId,
}: {
  submissionId: string;
}) {
  const [details, setDetails] = useState<SubmissionDetails | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let current = true;

    void apiRequest<SubmissionDetails>(
      `/api/admin/submissions/${encodeURIComponent(submissionId)}`,
    )
      .then(result => {
        if (!current) return;
        setDetails(result);
        setError(null);
      })
      .catch(failure => {
        if (current) setError(asApiError(failure));
      })
      .finally(() => {
        if (current) setLoading(false);
      });

    return () => {
      current = false;
    };
  }, [submissionId]);

  const fieldLabels = useMemo(() => {
    const labels = new Map<string, string>();

    for (const section of details?.formSnapshot.sections ?? []) {
      for (const field of section.fields ?? []) {
        labels.set(field.key, field.label);
      }
    }

    return labels;
  }, [details]);

  return (
    <main className="min-h-screen bg-slate-100 p-4 text-slate-950 sm:p-6">
      <div className="mx-auto max-w-5xl space-y-6">
        <header className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="text-sm font-semibold uppercase tracking-wider text-blue-700">
              Portal de servicii
            </p>
            <h1 className="text-2xl font-semibold">Detaliu cerere</h1>
          </div>
          <Link href="/cereri" className="text-blue-700 underline">
            Înapoi la cereri
          </Link>
        </header>

        {error && <ApiErrorPanel error={error} />}
        {loading && <p role="status">Încărcăm cererea…</p>}

        {details && (
          <>
            <section className="rounded-xl bg-white p-5 shadow-sm">
              <h2 className="text-lg font-semibold">Identificare și stare</h2>
              <dl className="mt-4 grid gap-4 sm:grid-cols-2">
                <div><dt className="text-sm text-slate-600">Serviciu</dt><dd>{details.serviceTitle} <span className="text-slate-600">({details.serviceCode})</span></dd></div>
                <div><dt className="text-sm text-slate-600">Stare</dt><dd>{details.status}</dd></div>
                <div><dt className="text-sm text-slate-600">Depus la</dt><dd>{formatDate(details.submittedAt)}</dd></div>
                <div><dt className="text-sm text-slate-600">Înregistrat la</dt><dd>{formatDate(details.registeredAt)}</dd></div>
                <div><dt className="text-sm text-slate-600">Număr registratură</dt><dd>{details.registryDisplayNumber ?? "—"}</dd></div>
                <div><dt className="text-sm text-slate-600">Versiune formular</dt><dd>{details.schemaVersion}</dd></div>
              </dl>
              {details.statusDetails && <p className="mt-4 rounded-lg bg-amber-50 p-3 text-amber-950">{details.statusDetails}</p>}
            </section>

            <section className="rounded-xl bg-white p-5 shadow-sm">
              <h2 className="text-lg font-semibold">Solicitant</h2>
              <dl className="mt-4 grid gap-4 sm:grid-cols-2">
                <div><dt className="text-sm text-slate-600">Nume</dt><dd>{details.applicantName}</dd></div>
                <div><dt className="text-sm text-slate-600">E-mail</dt><dd>{details.applicantEmail}</dd></div>
              </dl>
            </section>

            <section className="rounded-xl bg-white p-5 shadow-sm">
              <h2 className="text-lg font-semibold">Date depuse</h2>
              {Object.keys(details.values).length === 0 ? <p className="mt-3 text-slate-600">Nu au fost depuse valori de formular.</p> : <dl className="mt-4 grid gap-4 sm:grid-cols-2">{Object.entries(details.values).map(([key, value]) => <div key={key}><dt className="text-sm text-slate-600">{fieldLabels.get(key) ?? key}</dt><dd className="break-words">{displayValue(value)}</dd></div>)}</dl>}
            </section>

            <section className="rounded-xl bg-white p-5 shadow-sm">
              <h2 className="text-lg font-semibold">Fișiere atașate</h2>
              <p className="mt-1 text-sm text-slate-600">Backoffice-ul afișează metadatele; accesul la documentele de lucru rămâne în DMS.</p>
              {details.files.length === 0 ? <p className="mt-3 text-slate-600">Nu există fișiere.</p> : <ul className="mt-4 divide-y divide-slate-200">{details.files.map(file => <li key={file.id} className="py-3"><p className="font-medium">{file.originalName}</p><p className="text-sm text-slate-600">{file.kind} · {file.contentType} · {formatBytes(file.sizeBytes)}</p></li>)}</ul>}
            </section>

            <section className="rounded-xl bg-white p-5 shadow-sm">
              <h2 className="text-lg font-semibold">Cronologie</h2>
              {details.events.length === 0 ? <p className="mt-3 text-slate-600">Nu există evenimente.</p> : <ol className="mt-4 space-y-4 border-l border-slate-300 pl-5">{details.events.map(event => <li key={event.id}><p className="font-medium">{event.type}</p><p>{event.message}</p><time className="text-sm text-slate-600">{formatDate(event.occurredAt)}</time></li>)}</ol>}
            </section>

            <section className="rounded-xl bg-white p-5 text-sm shadow-sm">
              <h2 className="text-lg font-semibold">Trasabilitate integrare</h2>
              <dl className="mt-4 space-y-3"><div><dt className="text-slate-600">ID extern</dt><dd className="break-all font-mono">{details.externalId}</dd></div><div><dt className="text-slate-600">ID poziție DMS</dt><dd className="break-all font-mono">{details.dmsEntryId ?? "Încă neînregistrată în DMS"}</dd></div></dl>
            </section>
          </>
        )}
      </div>
    </main>
  );
}
