"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { asApiError, type ApiError } from "@/lib/http";
import {
  getPublicArticle,
  type PublicArticle,
} from "@/lib/public-articles";

function formatDate(value: string | null) {
  if (!value) return null;

  return new Intl.DateTimeFormat("ro-RO", {
    dateStyle: "long",
  }).format(new Date(value));
}

export default function PublicArticlePage({ slug }: { slug: string }) {
  const [article, setArticle] = useState<PublicArticle | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let active = true;

    void getPublicArticle(slug)
      .then(result => {
        if (active) setArticle(result);
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
  }, [slug]);

  if (loading) {
    return <p role="status">Se încarcă articolul…</p>;
  }

  if (error) {
    return (
      <div className="space-y-5">
        <ApiErrorPanel error={error} />
        <Link href="/#articole" className="text-blue-700 underline">
          Înapoi la articole
        </Link>
      </div>
    );
  }

  if (!article) return null;

  return (
    <article className="space-y-8">
      <header className="space-y-3">
        <Link href="/#articole" className="text-sm font-medium text-blue-700 underline">
          ← Toate articolele
        </Link>
        {formatDate(article.publishedAt) && (
          <time className="block text-sm text-slate-500">
            {formatDate(article.publishedAt)}
          </time>
        )}
        <h1 className="text-3xl font-bold tracking-tight sm:text-4xl">
          {article.title}
        </h1>
        {article.summary && (
          <p className="max-w-3xl text-lg text-slate-600">
            {article.summary}
          </p>
        )}
      </header>

      <section
        aria-label="Conținut articol"
        className="rich-content max-w-none rounded-2xl border border-slate-200 bg-white p-6"
        dangerouslySetInnerHTML={{ __html: article.body }}
      />
    </article>
  );
}
