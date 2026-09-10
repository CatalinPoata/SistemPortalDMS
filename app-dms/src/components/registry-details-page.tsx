"use client";

import Link from "next/link";
import { useState } from "react";
import { useAuth } from "@/hooks/use-auth";
import { useApiQuery } from "@/hooks/use-api-query";
import { restoreSession } from "@/lib/auth-store";
import type { RegistryEntryDetails } from "@/lib/registry-details";
import ApiErrorMessage from "@/components/api-error-message";
import RegistryDetailsSections from "@/components/registry-details-sections";

const buttonClass =
  "rounded-lg bg-blue-700 px-4 py-2 text-white hover:bg-blue-800 " +
  "disabled:opacity-50 focus-visible:outline-2 " +
  "focus-visible:outline-offset-2 focus-visible:outline-blue-700";

export default function RegistryDetailsPage({ entryId }: {
  entryId: string;
}) {
  const auth = useAuth();

  return (
    <main className="min-h-screen bg-slate-100 p-4 text-slate-950 sm:p-6">
      <div className="mx-auto max-w-5xl space-y-6">
        <header className="space-y-3">
          <nav
            aria-label="Navigare"
            className="flex flex-wrap gap-4"
          >
            <Link
              href="/registratura"
              className="text-blue-700 underline"
            >
              Înapoi la registratură
            </Link>

            <Link href="/" className="text-blue-700 underline">
              Cont și deconectare
            </Link>
          </nav>

          <h1 className="text-2xl font-semibold">
            Detaliu poziție
          </h1>
        </header>

        {auth.status === "loading" && (
          <p role="status">Verificăm sesiunea…</p>
        )}

        {auth.status === "anonymous" && (
          <p role="status">
            Autentifică-te din pagina de cont pentru a consulta poziția.
          </p>
        )}

        {auth.status === "error" && (
          <div className="space-y-3">
            <ApiErrorMessage error={auth.error} />

            <button
              className={buttonClass}
              onClick={() => void restoreSession()}
            >
              Reîncearcă verificarea sesiunii
            </button>
          </div>
        )}

        {auth.status === "authenticated" && (
          <DetailsContent
            key={`${auth.user.id}:${entryId}`}
            entryId={entryId}
          />
        )}
      </div>
    </main>
  );
}

function DetailsContent({ entryId }: { entryId: string }) {
  const [revision, setRevision] = useState(0);
  const [notice, setNotice] = useState<string | null>(null);

  const { data, error, loading } = useApiQuery<RegistryEntryDetails>(
    `/api/registry-entries/${encodeURIComponent(entryId)}`,
    revision,
  );

  return (
    <div className="space-y-6" aria-busy={loading}>
      <button
        className={buttonClass}
        disabled={loading}
        onClick={() => setRevision(value => value + 1)}
      >
        {error ? "Reîncearcă" : "Reîncarcă detaliile"}
      </button>

      {loading && (
        <p role="status">Încărcăm detaliile…</p>
      )}

      {error && (
        <div className="space-y-3">
          {error.status === 404 && (
            <p className="font-semibold">
              Poziția solicitată nu există.
            </p>
          )}

          <ApiErrorMessage error={error} />
        </div>
      )}

      {notice && (
  <p
    role="status"
    className="rounded-lg bg-green-50 p-4 text-green-900"
  >
    {notice}
  </p>
)}

    {data && (
    <RegistryDetailsSections
        entry={data}
        onEntryChanged={message => {
        setNotice(message);
        setRevision(value => value + 1);
        }}
    />
    )}
    </div>
  );
}
