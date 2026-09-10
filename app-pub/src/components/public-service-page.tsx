"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import DynamicServiceForm from "@/components/dynamic-service-form";
import { asApiError, type ApiError } from "@/lib/http";
import {
  getPublicService,
  type PublicServiceDetails,
} from "@/lib/public-services";

export default function PublicServicePage({ code }: { code: string }) {
  const [service, setService] = useState<PublicServiceDetails | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;

    void getPublicService(code)
      .then(result => {
        if (active) setService(result);
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
  }, [code]);

  if (loading) {
    return <p role="status">Se încarcă serviciul…</p>;
  }

  if (error) {
    return (
      <div className="space-y-5">
        <ApiErrorPanel error={error} />
        <Link href="/" className="text-blue-700 underline">
          Înapoi la servicii
        </Link>
      </div>
    );
  }

  if (!service) return null;

  return (
    <article className="space-y-8">
      <header className="space-y-3">
        <Link href="/" className="text-sm font-medium text-blue-700 underline">
          ← Toate serviciile
        </Link>
        <h1 className="text-3xl font-bold tracking-tight sm:text-4xl">
          {service.title}
        </h1>
        {service.shortDescription && (
          <p className="max-w-3xl text-lg text-slate-600">
            {service.shortDescription}
          </p>
        )}
      </header>

      {service.description && (
        <section
          aria-label="Descrierea serviciului"
          className="rich-content max-w-none rounded-2xl border border-slate-200 bg-white p-6"
          dangerouslySetInnerHTML={{ __html: service.description }}
        />
      )}

      <section aria-labelledby="form-title" className="space-y-5">
        <div>
          <p className="text-sm font-semibold uppercase tracking-wider text-blue-700">
            Formular online
          </p>
          <h2 id="form-title" className="mt-1 text-2xl font-bold">
            Completează cererea
          </h2>
        </div>

        <DynamicServiceForm service={service} />
      </section>
    </article>
  );
}
