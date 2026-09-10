"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { asApiError, type ApiError } from "@/lib/http";
import { getPublicSurveys } from "@/lib/surveys";

type SurveyPreview = {
  code: string;
  title: string;
  description: string | null;
};

export default function HomeSurveys() {
  const [surveys, setSurveys] = useState<SurveyPreview[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);

  useEffect(() => {
    let active = true;

    void getPublicSurveys()
      .then(result => {
        if (active) setSurveys(result.items.slice(0, 3));
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

  if (loading) return <p role="status">Se încarcă chestionarele active…</p>;
  if (error) return <ApiErrorPanel error={error} />;

  if (surveys.length === 0) {
    return <p className="rounded-xl border border-slate-200 bg-white p-6 text-slate-600">Nu există chestionare active.</p>;
  }

  return (
    <div className="grid gap-5 md:grid-cols-3">
      {surveys.map(survey => (
        <article key={survey.code} className="flex min-h-48 flex-col rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <h3 className="text-xl font-semibold text-slate-950">{survey.title}</h3>
          {survey.description ? (
            <div
              className="rich-content mt-3 flex-1 text-sm leading-6 text-slate-600"
              dangerouslySetInnerHTML={{ __html: survey.description }}
            />
          ) : (
            <p className="mt-3 flex-1 text-sm leading-6 text-slate-600">
              Spune-ne părerea ta completând chestionarul.
            </p>
          )}
          <Link href="/chestionare" className="mt-5 inline-flex w-fit rounded-lg bg-blue-700 px-4 py-2 text-sm font-medium text-white hover:bg-blue-800">
            Completează chestionarul
          </Link>
        </article>
      ))}
    </div>
  );
}
