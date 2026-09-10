"use client";

import {
  useState,
  type FormEvent,
  type ReactNode,
} from "react";
import { useApiQuery } from "@/hooks/use-api-query";
import { apiRequest } from "@/lib/auth-store";
import { ApiError, asApiError } from "@/lib/http";
import type { RegistryDocumentDetails } from "@/lib/registry-details";
import ApiErrorMessage from "@/components/api-error-message";

const maxFileSize = 10 * 1024 * 1024;

const inputClass =
  "mt-1 w-full rounded-lg border border-slate-300 bg-white p-2 " +
  "focus:outline-2 focus:outline-blue-700";

const buttonClass =
  "rounded-lg bg-blue-700 px-4 py-2 text-white hover:bg-blue-800 " +
  "disabled:cursor-not-allowed disabled:opacity-50 " +
  "focus-visible:outline-2 focus-visible:outline-offset-2 " +
  "focus-visible:outline-blue-700";

type DocumentKind = {
  id: string;
  code: string;
  name: string;
};

type SignedDownloadUrl = {
  documentId: string;
  url: string;
  expiresAt: string;
};

function Panel({ children }: {
  children: ReactNode;
}) {
  return (
    <section className="space-y-4 rounded-xl bg-white p-5 shadow-sm">
      {children}
    </section>
  );
}

export function DocumentUploadPanel({
  entryId,
  onDocumentUploaded,
}: {
  entryId: string;
  onDocumentUploaded: () => void;
}) {
  const [revision, setRevision] = useState(0);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  const documentKinds = useApiQuery<DocumentKind[]>(
    "/api/document-kinds",
    revision,
  );

  async function upload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const form = event.currentTarget;
    const formData = new FormData(form);
    const file = formData.get("File");

    setError(null);

    try {
      if (!(file instanceof File) || file.size === 0) {
        throw new ApiError(422, {
          title: "Fișier invalid",
          detail: "Alege un fișier care nu este gol.",
        });
      }

      if (file.size > maxFileSize) {
        throw new ApiError(413, {
          title: "Fișier prea mare",
          detail: "Dimensiunea maximă permisă pentru un fișier este de 10 MB.",
        });
      }

      if (!formData.get("DocumentKindId")) {
        throw new ApiError(422, {
          title: "Tip de document obligatoriu",
          detail: "Alege tipul documentului.",
        });
      }

      for (const name of ["DocumentDate", "Issuer", "Note"]) {
        if (!String(formData.get(name) ?? "").trim()) {
          formData.delete(name);
        }
      }

      setPending(true);

      await apiRequest(
        "/api/registry-entries/" +
          encodeURIComponent(entryId) +
          "/documents",
        {
          method: "POST",
          body: formData,
        },
        {
          timeoutMs: 120_000,
        },
      );

      form.reset();
      onDocumentUploaded();
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setPending(false);
    }
  }

  if (documentKinds.error) {
    return (
      <Panel>
        <h2 className="text-xl font-semibold">Adaugă document</h2>

        <ApiErrorMessage error={documentKinds.error} />

        <button
          type="button"
          className={buttonClass}
          onClick={() => setRevision(value => value + 1)}
        >
          Reîncearcă încărcarea tipurilor
        </button>
      </Panel>
    );
  }

  if (!documentKinds.data) {
    return (
      <Panel>
        <h2 className="text-xl font-semibold">Adaugă document</h2>
        <p role="status">Încărcăm tipurile de documente…</p>
      </Panel>
    );
  }

  if (documentKinds.data.length === 0) {
    return (
      <Panel>
        <h2 className="text-xl font-semibold">Adaugă document</h2>
        <p>Nu există tipuri de document active.</p>
      </Panel>
    );
  }

  return (
    <Panel>
      <h2 className="text-xl font-semibold">Adaugă document</h2>

      <p className="text-sm text-slate-600">
        Sunt acceptate PDF, PNG, JPEG, DOCX și ODT, cu dimensiunea maximă de 10 MB.
        Tipul real este verificat de server după conținut.
      </p>

      {error && <ApiErrorMessage error={error} />}

      <form
        onSubmit={upload}
        className="grid gap-4 sm:grid-cols-2"
      >
        <fieldset disabled={pending} className="contents">
          <label className="sm:col-span-2">
            Fișier
            <input
              required
              name="File"
              type="file"
              accept={[
                "application/pdf",
                "image/png",
                "image/jpeg",
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                "application/vnd.oasis.opendocument.text",
              ].join(",")}
              className={inputClass}
            />
          </label>

          <label>
            Tip document
            <select
              required
              name="DocumentKindId"
              defaultValue=""
              className={inputClass}
            >
              <option value="" disabled>
                Alege tipul
              </option>

              {documentKinds.data.map(kind => (
                <option key={kind.id} value={kind.id}>
                  {kind.code} — {kind.name}
                </option>
              ))}
            </select>
          </label>

          <label>
            Sens
            <select
              name="Direction"
              defaultValue="In"
              className={inputClass}
            >
              <option value="In">Intrare</option>
              <option value="Out">Ieșire</option>
            </select>
          </label>

          <label>
            Data documentului
            <input
              name="DocumentDate"
              type="date"
              className={inputClass}
            />
          </label>

          <label>
            Emitent
            <input
              name="Issuer"
              maxLength={200}
              className={inputClass}
            />
          </label>

          <label className="sm:col-span-2">
            Observații
            <textarea
              name="Note"
              maxLength={500}
              rows={3}
              className={inputClass}
            />
          </label>

          <div className="sm:col-span-2">
            <button type="submit" className={buttonClass}>
              {pending ? "Încărcăm documentul…" : "Încarcă documentul"}
            </button>
          </div>
        </fieldset>
      </form>
    </Panel>
  );
}

export function DocumentDownloadButton({
  entryId,
  document,
}: {
  entryId: string;
  document: RegistryDocumentDetails;
}) {
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  async function download() {
    setError(null);
    setPending(true);

    try {
      const result = await apiRequest<SignedDownloadUrl>(
        "/api/registry-entries/" +
          encodeURIComponent(entryId) +
          "/documents/" +
          encodeURIComponent(document.id) +
          "/download-url",
        {
          method: "POST",
        },
      );

      const target = new URL(result.url, window.location.origin);

      if (
        target.origin !== window.location.origin ||
        !target.pathname.startsWith("/api/documents/")
      ) {
        throw new ApiError(0, {
          detail: "Serverul a returnat un URL de descărcare invalid.",
        });
      }

      window.location.assign(target.toString());
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setPending(false);
    }
  }

  return (
    <div className="space-y-3">
      <button
        type="button"
        className={buttonClass}
        disabled={pending}
        onClick={() => void download()}
      >
        {pending ? "Pregătim descărcarea…" : "Descarcă"}
      </button>

      {error && <ApiErrorMessage error={error} />}
    </div>
  );
}
