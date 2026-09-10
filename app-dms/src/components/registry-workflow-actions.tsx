"use client";

import {
  useState,
  type FormEvent,
} from "react";
import { apiRequest } from "@/lib/auth-store";
import { ApiError, asApiError } from "@/lib/http";
import type { RegistryEntryDetails } from "@/lib/registry-details";
import ApiErrorMessage from "@/components/api-error-message";

const buttonClass =
  "rounded-lg bg-blue-700 px-4 py-2 text-white hover:bg-blue-800 " +
  "disabled:cursor-not-allowed disabled:opacity-50 " +
  "focus-visible:outline-2 focus-visible:outline-offset-2 " +
  "focus-visible:outline-blue-700";

const dangerButtonClass =
  "rounded-lg bg-red-700 px-4 py-2 text-white hover:bg-red-800 " +
  "disabled:cursor-not-allowed disabled:opacity-50 " +
  "focus-visible:outline-2 focus-visible:outline-offset-2 " +
  "focus-visible:outline-red-700";

const inputClass =
  "mt-1 w-full rounded-lg border border-slate-300 bg-white p-2 " +
  "focus:outline-2 focus:outline-blue-700";

type NoteAction = "request-info" | "reject" | "cancel";

const noteActions: Record<NoteAction, {
  label: string;
  endpoint: string;
  successMessage: string;
}> = {
  "request-info": {
    label: "Solicită clarificări",
    endpoint: "request-info",
    successMessage: "Au fost solicitate clarificări.",
  },
  reject: {
    label: "Respinge poziția",
    endpoint: "reject",
    successMessage: "Poziția a fost respinsă.",
  },
  cancel: {
    label: "Anulează poziția",
    endpoint: "cancel",
    successMessage: "Poziția a fost anulată.",
  },
};

export default function RegistryWorkflowActions({
  entry,
  onEntryChanged,
}: {
  entry: RegistryEntryDetails;
  onEntryChanged: (message: string) => void;
}) {
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [noteAction, setNoteAction] = useState<NoteAction | null>(
    null,
  );

  const hasResponseDocument = entry.documents.some(
    document => document.direction === "Out",
  );

  const isFinal = [
    "Completed",
    "Rejected",
    "Cancelled",
  ].includes(entry.status);

  async function run(
    endpoint: string,
    successMessage: string,
    note?: string,
  ) {
    setError(null);
    setPending(true);

    try {
      await apiRequest(
        `/api/registry-entries/${encodeURIComponent(entry.id)}/${endpoint}`,
        note === undefined
          ? {
              method: "POST",
            }
          : {
              method: "POST",
              headers: {
                "Content-Type": "application/json",
              },
              body: JSON.stringify({ note }),
            },
      );

      onEntryChanged(successMessage);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setPending(false);
    }
  }

  async function submitNote(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (!noteAction) {
      return;
    }

    const note = String(
      new FormData(event.currentTarget).get("note") ?? "",
    ).trim();

    if (!note) {
      setError(new ApiError(422, {
        title: "Motiv obligatoriu",
        detail: "Completează motivul acestei acțiuni.",
      }));
      return;
    }

    const action = noteActions[noteAction];

    await run(
      action.endpoint,
      action.successMessage,
      note,
    );
  }

  if (isFinal) {
    return (
      <section className="rounded-xl bg-white p-5 shadow-sm">
        <h2 className="text-xl font-semibold">Acțiuni workflow</h2>
        <p className="mt-3">
          Poziția se află într-o stare finală și nu mai are tranziții disponibile.
        </p>
      </section>
    );
  }

  const canTake =
    entry.status === "Registered" ||
    entry.status === "InfoRequested";

  const canCancel =
    entry.status === "Registered" ||
    entry.status === "InfoRequested";

  return (
    <section className="space-y-4 rounded-xl bg-white p-5 shadow-sm">
      <h2 className="text-xl font-semibold">Acțiuni workflow</h2>

      {error && <ApiErrorMessage error={error} />}

      {canTake && (
        <button
          type="button"
          className={buttonClass}
          disabled={pending}
          onClick={() =>
            void run(
              "take",
              entry.status === "Registered"
                ? "Poziția a fost preluată spre soluționare."
                : "Poziția a revenit în lucru.",
            )
          }
        >
          {entry.status === "Registered"
            ? "Preia spre soluționare"
            : "Reia soluționarea"}
        </button>
      )}

      {entry.status === "InReview" && (
        <div className="flex flex-wrap gap-3">
          <button
            type="button"
            className={buttonClass}
            disabled={pending}
            onClick={() => {
              setError(null);
              setNoteAction("request-info");
            }}
          >
            Solicită clarificări
          </button>

          <button
            type="button"
            className={dangerButtonClass}
            disabled={pending}
            onClick={() => {
              setError(null);
              setNoteAction("reject");
            }}
          >
            Respinge
          </button>

          <button
            type="button"
            className={buttonClass}
            disabled={pending || !hasResponseDocument}
            title={
              hasResponseDocument
                ? undefined
                : "Finalizarea necesită un document de ieșire."
            }
            onClick={() =>
              void run(
                "complete",
                "Poziția a fost finalizată.",
              )
            }
          >
            Finalizează
          </button>

          {!hasResponseDocument && (
            <p className="w-full text-sm text-slate-600">
              Încarcă mai întâi un document cu sensul „Ieșire”.
            </p>
          )}
        </div>
      )}

      {canCancel && (
        <button
          type="button"
          className={dangerButtonClass}
          disabled={pending}
          onClick={() => {
            setError(null);
            setNoteAction("cancel");
          }}
        >
          Anulează poziția
        </button>
      )}

      {noteAction && (
        <form
          onSubmit={submitNote}
          className="space-y-3 rounded-lg border border-slate-300 p-4"
        >
          <label>
            Motiv
            <textarea
              required
              name="note"
              maxLength={1000}
              rows={4}
              className={inputClass}
            />
          </label>

          <div className="flex flex-wrap gap-3">
            <button
              type="submit"
              className={
                noteAction === "reject" ||
                noteAction === "cancel"
                  ? dangerButtonClass
                  : buttonClass
              }
              disabled={pending}
            >
              {pending
                ? "Procesăm…"
                : noteActions[noteAction].label}
            </button>

            <button
              type="button"
              className={buttonClass}
              disabled={pending}
              onClick={() => {
                setNoteAction(null);
                setError(null);
              }}
            >
              Renunță
            </button>
          </div>
        </form>
      )}
    </section>
  );
}
