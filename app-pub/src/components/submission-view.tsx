"use client";

import Link from "next/link";
import { useEffect, useState, type FormEvent } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { useAuth } from "@/hooks/use-auth";
import { asApiError, type ApiError } from "@/lib/http";
import type { ServiceFormField, ServiceFormSchema } from "@/lib/public-services";
import {
  createSubmissionFileDownloadUrl,
  downloadRegistrationReceipt,
  getMySubmissions,
  getSubmission,
  uploadClarifications,
  withdrawSubmission,
  type SubmissionDetails,
  type SubmissionListItem,
  type SubmissionStatus,
} from "@/lib/submissions";

export const statusLabels: Record<SubmissionStatus, string> = {
  Submitted: "Depusă",
  Registered: "Înregistrată",
  InReview: "În analiză",
  InfoRequested: "Completări solicitate",
  Completed: "Finalizată",
  Rejected: "Respinsă",
  Cancelled: "Retrasă",
};

const withdrawableStatuses: SubmissionStatus[] = [
  "Submitted",
  "Registered",
  "InfoRequested",
];

function formatDate(value: string) {
  return new Intl.DateTimeFormat("ro-RO", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value));
}

function formatFileSize(sizeBytes: number) {
  if (sizeBytes < 1024 * 1024) {
    return `${Math.max(1, Math.round(sizeBytes / 1024))} KB`;
  }

  return `${(sizeBytes / 1024 / 1024).toFixed(1)} MB`;
}

function fieldIsVisible(
  field: ServiceFormField,
  values: Record<string, unknown>,
) {
  if (!field.visibleWhen) return true;

  return Object.is(
    values[field.visibleWhen.field],
    field.visibleWhen.equals,
  );
}

function displayValue(field: ServiceFormField, value: unknown) {
  if (value === undefined || value === null || value === "") return "—";

  if (field.type === "boolean") return value === true ? "Da" : "Nu";

  if (field.type === "select" || field.type === "radio") {
    return field.options?.find(option => option.value === value)?.label ?? String(value);
  }

  if (field.type === "checkboxes" && Array.isArray(value)) {
    return value
      .map(item =>
        field.options?.find(option => option.value === item)?.label ?? String(item),
      )
      .join(", ");
  }

  if (field.type === "file" && typeof value === "object") {
    return "Fișier atașat";
  }

  return String(value);
}

export function ReadOnlySubmissionValues({
  schema,
  values,
}: {
  schema: ServiceFormSchema;
  values: Record<string, unknown>;
}) {
  return (
    <div className="space-y-5">
      {schema.sections.map(section => (
        <section
          key={section.key}
          className="rounded-2xl border border-slate-200 bg-white p-5"
        >
          <h3 className="text-lg font-semibold">{section.title}</h3>
          <dl className="mt-4 divide-y divide-slate-100">
            {section.fields.map(field => {
              if (
                !fieldIsVisible(field, values) ||
                field.type === "heading" ||
                field.type === "paragraph"
              ) {
                return null;
              }

              return (
                <div
                  key={field.key}
                  className="grid gap-1 py-3 sm:grid-cols-[minmax(0,1fr)_minmax(0,2fr)] sm:gap-5"
                >
                  <dt className="text-sm font-medium text-slate-600">
                    {field.label}
                  </dt>
                  <dd className="whitespace-pre-wrap break-words text-slate-950">
                    {displayValue(field, values[field.key])}
                  </dd>
                </div>
              );
            })}
          </dl>
        </section>
      ))}
    </div>
  );
}

export function MySubmissions() {
  const auth = useAuth();
  const [items, setItems] = useState<SubmissionListItem[]>([]);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);

  useEffect(() => {
    if (auth.status !== "authenticated" || auth.user.role !== "Citizen") {
      return;
    }

    let active = true;
    setLoading(true);
    void getMySubmissions(page, pageSize)
      .then(result => {
        if (!active) return;
        setItems(result.items);
        setTotal(result.total);
        setPageSize(result.pageSize);
      })
      .catch(failure => {
        if (active) setError(asApiError(failure));
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [auth.status, auth.user, page, pageSize]);

  if (auth.status === "loading") {
    return <p role="status">Se verifică sesiunea…</p>;
  }

  if (auth.status === "anonymous") {
    return (
      <p className="rounded-xl border border-blue-200 bg-blue-50 p-4">
        <Link
          href="/login?next=/cereri"
          className="font-medium text-blue-800 underline"
        >
          Autentifică-te
        </Link>{" "}
        pentru a consulta cererile tale.
      </p>
    );
  }

  if (auth.status === "error") {
    return <ApiErrorPanel error={auth.error} />;
  }

  if (auth.user.role !== "Citizen") {
    return <p>Numai conturile de cetățean au cereri personale.</p>;
  }

  if (loading) return <p role="status">Se încarcă cererile…</p>;
  if (error) return <ApiErrorPanel error={error} />;

  if (items.length === 0) {
    return (
      <div className="rounded-2xl border border-slate-200 bg-white p-6">
        <p>Nu ai depus încă nicio cerere.</p>
        <Link href="/#servicii" className="mt-3 inline-block text-blue-700 underline">
          Vezi serviciile disponibile
        </Link>
      </div>
    );
  }

  const lastPage = Math.max(1, Math.ceil(total / pageSize));

  return (
    <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white">
      <ul className="divide-y divide-slate-200">
        {items.map(item => (
          <li key={item.id}>
            <Link
              href={`/cereri/${item.id}`}
              className="grid gap-3 p-5 hover:bg-slate-50 sm:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_auto] sm:items-center"
            >
              <div>
                <h2 className="font-semibold text-slate-950">{item.serviceTitle}</h2>
                <p className="mt-1 text-sm text-slate-600">
                  Depusă la {formatDate(item.submittedAt)}
                </p>
              </div>
              <div className="text-sm">
                <span className="font-medium">{statusLabels[item.status]}</span>
                {item.registryDisplayNumber && (
                  <p className="text-slate-600">Nr. {item.registryDisplayNumber}</p>
                )}
              </div>
              <span className="text-blue-700">Detalii →</span>
            </Link>
          </li>
        ))}
      </ul>
      <nav aria-label="Paginarea cererilor" className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 p-4">
        <p className="text-sm text-slate-600">Pagina {page} din {lastPage} · {total} cereri</p>
        <div className="flex gap-2">
          <button type="button" onClick={() => setPage(current => Math.max(1, current - 1))} disabled={page === 1} className="rounded-lg border border-slate-300 px-3 py-2 text-sm disabled:cursor-not-allowed disabled:opacity-50">← Anterioară</button>
          <button type="button" onClick={() => setPage(current => Math.min(lastPage, current + 1))} disabled={page >= lastPage} className="rounded-lg border border-slate-300 px-3 py-2 text-sm disabled:cursor-not-allowed disabled:opacity-50">Următoarea →</button>
        </div>
      </nav>
    </div>
  );
}

export function SubmissionDetail({ id }: { id: string }) {
  const auth = useAuth();
  const [submission, setSubmission] = useState<SubmissionDetails | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [downloadingFileId, setDownloadingFileId] = useState<string | null>(null);
  const [downloadingReceipt, setDownloadingReceipt] = useState(false);
  const [reason, setReason] = useState("");
  const [clarificationFiles, setClarificationFiles] = useState<File[]>([]);
  const [uploadingClarifications, setUploadingClarifications] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  useEffect(() => {
    if (auth.status !== "authenticated" || auth.user.role !== "Citizen") {
      return;
    }

    let active = true;
    void getSubmission(id)
      .then(result => {
        if (active) setSubmission(result);
      })
      .catch(failure => {
        if (active) setError(asApiError(failure));
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [auth.status, auth.user, id]);

  async function handleWithdraw(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!submission || busy) return;

    setBusy(true);
    setError(null);

    try {
      const updated = await withdrawSubmission(submission.id, reason.trim());
      setSubmission(updated);
      setReason("");
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(false);
    }
  }

  async function handleFileDownload(fileId: string) {
    if (!submission || downloadingFileId) return;

    setDownloadingFileId(fileId);
    setError(null);

    try {
      const result = await createSubmissionFileDownloadUrl(
        submission.id,
        fileId,
      );
      window.location.assign(result.url);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setDownloadingFileId(null);
    }
  }

  async function handleReceiptDownload() {
    if (!submission || downloadingReceipt) return;

    setDownloadingReceipt(true);
    setError(null);

    try {
      const result = await downloadRegistrationReceipt(submission.id);
      const url = URL.createObjectURL(result.blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = result.fileName;
      document.body.appendChild(anchor);
      anchor.click();
      anchor.remove();
      URL.revokeObjectURL(url);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setDownloadingReceipt(false);
    }
  }

  async function handleClarifications(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!submission || uploadingClarifications || clarificationFiles.length === 0) {
      return;
    }

    setUploadingClarifications(true);
    setError(null);

    try {
      const updated = await uploadClarifications(
        submission.id,
        clarificationFiles,
      );
      setSubmission(updated);
      setClarificationFiles([]);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setUploadingClarifications(false);
    }
  }

  if (auth.status === "loading" || loading) {
    return <p role="status">Se încarcă cererea…</p>;
  }

  if (auth.status === "anonymous") {
    return (
      <p>
        <Link
          href={`/login?next=${encodeURIComponent(`/cereri/${id}`)}`}
          className="text-blue-700 underline"
        >
          Autentifică-te
        </Link>{" "}
        pentru a consulta cererea.
      </p>
    );
  }

  if (auth.status === "error") return <ApiErrorPanel error={auth.error} />;
  if (error && !submission) return <ApiErrorPanel error={error} />;
  if (!submission) return null;

  return (
    <article className="space-y-8">
      <header className="space-y-3">
        <Link href="/cereri" className="text-sm text-blue-700 underline">
          ← Cererile mele
        </Link>
        <h1 className="text-3xl font-bold">{submission.serviceTitle}</h1>
        <div className="flex flex-wrap gap-x-6 gap-y-2 text-sm text-slate-600">
          <span>Stare: {statusLabels[submission.status]}</span>
          <span>Depusă la: {formatDate(submission.submittedAt)}</span>
          {submission.registryDisplayNumber && (
            <span>Număr: {submission.registryDisplayNumber}</span>
          )}
        </div>
        {submission.registryDisplayNumber && (
          <button
            type="button"
            disabled={downloadingReceipt}
            onClick={() => void handleReceiptDownload()}
            className="rounded-lg border border-blue-700 px-3 py-2 text-sm font-medium text-blue-700 hover:bg-blue-50 disabled:opacity-50"
          >
            {downloadingReceipt ? "Se pregătește dovada…" : "Descarcă dovada PDF"}
          </button>
        )}
      </header>

      {error && <ApiErrorPanel error={error} />}

      {submission.statusDetails && (
        <p className="rounded-xl border border-amber-200 bg-amber-50 p-4">
          {submission.statusDetails}
        </p>
      )}

      {submission.status === "InfoRequested" && (
        <section className="rounded-2xl border border-amber-200 bg-amber-50 p-5">
          <h2 className="text-lg font-semibold text-amber-950">
            Trimite completările solicitate
          </h2>
          <p className="mt-1 text-sm text-amber-900">
            Încarcă documentele cerute. Fiecare fișier poate avea cel mult 10 MB;
            cererea revine în analiză după confirmarea înregistrării de către DMS.
          </p>
          <form onSubmit={handleClarifications} className="mt-4 space-y-3">
            <label htmlFor="clarification-files" className="block text-sm font-medium">
              Documente de clarificare
            </label>
            <input
              id="clarification-files"
              type="file"
              multiple
              required
              disabled={uploadingClarifications}
              accept="application/pdf,image/png,image/jpeg,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.oasis.opendocument.text"
              onChange={event => {
                setClarificationFiles(Array.from(event.currentTarget.files ?? []));
              }}
              className="block w-full rounded-lg border border-amber-300 bg-white px-3 py-2 text-sm"
            />
            {clarificationFiles.length > 0 && (
              <p className="text-sm text-amber-900">
                Selectate: {clarificationFiles.map(file => file.name).join(", ")}
              </p>
            )}
            <button
              type="submit"
              disabled={uploadingClarifications || clarificationFiles.length === 0}
              className="rounded-lg bg-amber-700 px-4 py-2 font-medium text-white hover:bg-amber-800 disabled:opacity-50"
            >
              {uploadingClarifications
                ? "Se încarcă…"
                : "Trimite completările"}
            </button>
          </form>
        </section>
      )}

      <section aria-labelledby="submitted-values" className="space-y-4">
        <h2 id="submitted-values" className="text-2xl font-semibold">
          Datele cererii
        </h2>
        <ReadOnlySubmissionValues
          schema={submission.formSnapshot}
          values={submission.values}
        />
      </section>

      <section aria-labelledby="timeline-title" className="space-y-4">
        <h2 id="timeline-title" className="text-2xl font-semibold">
          Cronologie
        </h2>
        <ol className="space-y-3 border-l-2 border-blue-200 pl-5">
          {submission.events.map(event => (
            <li key={event.id} className="relative">
              <span className="absolute -left-[1.65rem] top-1.5 size-3 rounded-full bg-blue-700" />
              <p className="font-medium">{event.message}</p>
              <time className="text-sm text-slate-600">
                {formatDate(event.occurredAt)}
              </time>
            </li>
          ))}
        </ol>
      </section>

      {submission.files.length > 0 && (
        <section aria-labelledby="files-title" className="space-y-4">
          <h2 id="files-title" className="text-2xl font-semibold">
            Fișiere
          </h2>
          <ul className="divide-y divide-slate-200 rounded-2xl border border-slate-200 bg-white">
            {submission.files.map(file => (
              <li key={file.id} className="flex flex-wrap items-center justify-between gap-3 p-4">
                <div>
                  <p className="font-medium text-slate-950">{file.originalName}</p>
                  <p className="text-sm text-slate-600">
                    {file.fieldKey ? `Câmp: ${file.fieldKey}` : "Atașament general"}
                  </p>
                </div>
                <div className="flex items-center gap-4">
                  <span className="text-sm text-slate-600">
                    {formatFileSize(file.sizeBytes)}
                  </span>
                  {file.kind !== "Response" && (
                    <button
                      type="button"
                      disabled={downloadingFileId !== null}
                      onClick={() => void handleFileDownload(file.id)}
                      className="rounded-lg border border-blue-700 px-3 py-1.5 text-sm font-medium text-blue-700 hover:bg-blue-50 disabled:opacity-50"
                    >
                      {downloadingFileId === file.id
                        ? "Se pregătește…"
                        : "Descarcă"}
                    </button>
                  )}
                </div>
              </li>
            ))}
          </ul>
        </section>
      )}

      {withdrawableStatuses.includes(submission.status) && (
        <section className="rounded-2xl border border-red-200 bg-red-50 p-5">
          <h2 className="text-lg font-semibold text-red-950">Retrage cererea</h2>
          <p className="mt-1 text-sm text-red-900">
            Retragerea este definitivă și necesită un motiv.
          </p>
          <form onSubmit={handleWithdraw} className="mt-4 space-y-3">
            <label htmlFor="withdraw-reason" className="block text-sm font-medium">
              Motiv
            </label>
            <textarea
              id="withdraw-reason"
              required
              minLength={1}
              maxLength={1000}
              disabled={busy}
              value={reason}
              onChange={event => setReason(event.target.value)}
              className="min-h-24 w-full rounded-lg border border-red-300 bg-white px-3 py-2"
            />
            <button
              type="submit"
              disabled={busy || !reason.trim()}
              className="rounded-lg bg-red-700 px-4 py-2 font-medium text-white hover:bg-red-800 disabled:opacity-50"
            >
              {busy ? "Se retrage…" : "Confirmă retragerea"}
            </button>
          </form>
        </section>
      )}
    </article>
  );
}
