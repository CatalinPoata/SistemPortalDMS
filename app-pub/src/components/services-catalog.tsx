"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { asApiError, type ApiError } from "@/lib/http";
import {
  getPublicServices,
  type PublicServiceListItem,
} from "@/lib/public-services";

export default function ServicesCatalog() {
  const [services, setServices] = useState<PublicServiceListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);

  useEffect(() => {
    let active = true;

    void getPublicServices()
      .then(result => {
        if (active) setServices(result.items);
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

  if (loading) {
    return <p role="status">Se încarcă serviciile publice…</p>;
  }

  if (error) {
    return <ApiErrorPanel error={error} />;
  }

  if (services.length === 0) {
    return (
      <p className="rounded-xl border border-slate-200 bg-white p-6 text-slate-600">
        Nu există încă servicii publicate.
      </p>
    );
  }

  return (
    <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
      {services.map(service => (
        <article
          key={service.id}
          className="flex min-h-56 flex-col rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"
        >
          <h3 className="text-xl font-semibold text-slate-950">
            {service.title}
          </h3>

          <p className="mt-3 flex-1 text-sm leading-6 text-slate-600">
            {service.shortDescription ??
              "Consultă detaliile și completează formularul online."}
          </p>

          {service.requiresAttachment && (
            <p className="mt-4 text-xs font-medium text-amber-800">
              Necesită documente atașate
            </p>
          )}

          <Link
            href={`/servicii/${encodeURIComponent(service.code)}`}
            className="mt-5 inline-flex w-fit rounded-lg bg-blue-700 px-4 py-2 text-sm font-medium text-white hover:bg-blue-800"
          >
            Deschide serviciul
          </Link>
        </article>
      ))}
    </div>
  );
}
