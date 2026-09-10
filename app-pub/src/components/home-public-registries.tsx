"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { asApiError, type ApiError } from "@/lib/http";
import { getPublicRegistries, type PublicRegistryListItem } from "@/lib/public-registries";

export default function HomePublicRegistries() {
  const [registries, setRegistries] = useState<PublicRegistryListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);

  useEffect(() => {
    let active = true;

    void getPublicRegistries()
      .then(result => {
        if (active) setRegistries(result.items.slice(0, 3));
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
  }, []);

  if (loading) return <p role="status">Se încarcă registrele publice…</p>;
  if (error) return <ApiErrorPanel error={error} />;

  if (registries.length === 0) {
    return <p className="rounded-xl border border-slate-200 bg-white p-6 text-slate-600">Nu există registre publicate.</p>;
  }

  return (
    <div className="grid gap-5 md:grid-cols-3">
      {registries.map(registry => (
        <article key={registry.code} className="flex min-h-48 flex-col rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <p className="text-sm font-medium text-blue-700">{registry.entryCount} poziții publicate</p>
          <h3 className="mt-2 text-xl font-semibold text-slate-950">{registry.name}</h3>
          {registry.description ? (
            <div
              className="rich-content mt-3 flex-1 text-sm leading-6 text-slate-600"
              dangerouslySetInnerHTML={{ __html: registry.description }}
            />
          ) : (
            <p className="mt-3 flex-1 text-sm leading-6 text-slate-600">
              Consultă pozițiile din registrul {registry.code}.
            </p>
          )}
          <Link href="/registre" className="mt-5 inline-flex w-fit rounded-lg border border-blue-700 px-4 py-2 text-sm font-medium text-blue-800 hover:bg-blue-50">
            Consultă registrul
          </Link>
        </article>
      ))}
    </div>
  );
}
