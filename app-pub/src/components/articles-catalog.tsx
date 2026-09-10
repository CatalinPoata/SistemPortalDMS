"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { asApiError, type ApiError } from "@/lib/http";
import {
  getPublicArticles,
  type PublicArticleListItem,
} from "@/lib/public-articles";

function formatDate(value: string | null) {
  if (!value) return null;

  return new Intl.DateTimeFormat("ro-RO", {
    dateStyle: "long",
  }).format(new Date(value));
}

export default function ArticlesCatalog() {
  const [articles, setArticles] = useState<PublicArticleListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);

  useEffect(() => {
    let active = true;

    void getPublicArticles()
      .then(result => {
        if (active) setArticles(result.items);
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
    return <p role="status">Se încarcă articolele…</p>;
  }

  if (error) {
    return <ApiErrorPanel error={error} />;
  }

  if (articles.length === 0) {
    return (
      <p className="rounded-xl border border-slate-200 bg-white p-6 text-slate-600">
        Nu există încă articole publicate.
      </p>
    );
  }

  return (
    <div className="grid gap-5 md:grid-cols-2 lg:grid-cols-3">
      {articles.map(article => (
        <article
          key={article.id}
          className="flex min-h-52 flex-col rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"
        >
          {formatDate(article.publishedAt) && (
            <time className="text-sm text-slate-500">
              {formatDate(article.publishedAt)}
            </time>
          )}
          <h3 className="mt-2 text-xl font-semibold text-slate-950">
            {article.title}
          </h3>
          <p className="mt-3 flex-1 text-sm leading-6 text-slate-600">
            {article.summary ?? "Citește articolul pentru detalii."}
          </p>
          <Link
            href={`/articole/${encodeURIComponent(article.slug)}`}
            className="mt-5 inline-flex w-fit rounded-lg bg-blue-700 px-4 py-2 text-sm font-medium text-white shadow-sm transition hover:bg-blue-800"
          >
            Citește articolul
          </Link>
        </article>
      ))}
    </div>
  );
}
