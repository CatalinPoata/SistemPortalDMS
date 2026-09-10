"use client";

import Link from "next/link";
import {
  useEffect,
  useState,
  type FormEvent,
} from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { useAuth } from "@/hooks/use-auth";
import {
  createArticle,
  deleteArticle,
  emptyArticle,
  getArticle,
  getArticles,
  publishArticle,
  toWriteModel,
  unpublishArticle,
  updateArticle,
  type ArticleDetails,
  type ArticleListItem,
  type ArticleWrite,
} from "@/lib/articles";
import { asApiError, type ApiError } from "@/lib/http";
import { RichTextEditor } from "@/components/rich-text-editor";

const inputClass =
  "mt-1 w-full rounded-md border border-slate-300 bg-white px-3 py-2 " +
  "text-slate-950 focus:border-blue-600 focus:outline-none focus:ring-2 " +
  "focus:ring-blue-200 disabled:bg-slate-100";

const buttonClass =
  "rounded-md bg-blue-700 px-3 py-2 text-sm font-medium text-white " +
  "hover:bg-blue-800 disabled:cursor-wait disabled:opacity-60";

const secondaryButtonClass =
  "rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium " +
  "text-slate-800 hover:bg-slate-50 disabled:cursor-wait disabled:opacity-60";

function ErrorMessage({ error }: { error: ApiError | null }) {
  if (!error) return null;
  return <ApiErrorPanel error={error} />;
}

function ArticleEditor({
  slug,
  onSaved,
  onDeleted,
}: {
  slug: string | null;
  onSaved: (article: ArticleDetails) => void;
  onDeleted: () => void;
}) {
  const [model, setModel] = useState<ArticleWrite>(emptyArticle);
  const [loaded, setLoaded] = useState<ArticleDetails | null>(null);
  const [busy, setBusy] = useState(slug !== null);
  const [error, setError] = useState<ApiError | null>(null);

  const isNew = slug === null;

  useEffect(() => {
    let active = true;

    if (slug === null) {
      return () => {
        active = false;
      };
    }

    void getArticle(slug)
      .then(article => {
        if (!active) return;
        setLoaded(article);
        setModel(toWriteModel(article));
      })
      .catch(failure => {
        if (active) setError(asApiError(failure));
      })
      .finally(() => {
        if (active) setBusy(false);
      });

    return () => {
      active = false;
    };
  }, [slug]);

  function update<K extends keyof ArticleWrite>(key: K, value: ArticleWrite[K]) {
    setModel(current => ({ ...current, [key]: value }));
  }

  async function save() {
    setBusy(true);
    setError(null);

    try {
      const article = isNew
        ? await createArticle(model)
        : await updateArticle(slug, model);
      setLoaded(article);
      setModel(toWriteModel(article));
      onSaved(article);
      return article;
    } catch (failure) {
      setError(asApiError(failure));
      return null;
    } finally {
      setBusy(false);
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await save();
  }

  async function togglePublication() {
    const article = loaded ?? (await save());
    if (!article) return;

    setBusy(true);
    setError(null);

    try {
      const result = article.isPublished
        ? await unpublishArticle(article.slug)
        : await publishArticle(article.slug);
      setLoaded(result);
      setModel(toWriteModel(result));
      onSaved(result);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(false);
    }
  }

  async function remove() {
    if (!loaded || !window.confirm("Ștergi definitiv acest articol?")) return;

    setBusy(true);
    setError(null);

    try {
      await deleteArticle(loaded.slug);
      onDeleted();
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="rounded-xl border border-slate-200 bg-white p-5 shadow-sm sm:p-6">
      <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-xl font-semibold">
            {isNew ? "Articol nou" : "Editează articolul"}
          </h2>
          {loaded && (
            <p className="text-sm text-slate-600">
              {loaded.isPublished ? "Publicat" : "Ciornă"}
              {loaded.publishedAt && ` · ${new Date(loaded.publishedAt).toLocaleString("ro-RO")}`}
            </p>
          )}
        </div>

        {loaded && (
          <a
            href={`https://portal.localhost/articole/${encodeURIComponent(loaded.slug)}`}
            target="_blank"
            rel="noreferrer"
            className={secondaryButtonClass}
          >
            Vezi în portal
          </a>
        )}
      </div>

      <ErrorMessage error={error} />

      <form onSubmit={handleSubmit} className="mt-5 space-y-5" aria-busy={busy}>
        <fieldset disabled={busy} className="space-y-5">
          <div>
            <label htmlFor="article-title" className="text-sm font-medium">
              Titlu
            </label>
            <input
              id="article-title"
              required
              maxLength={250}
              value={model.title}
              onChange={event => update("title", event.target.value)}
              className={inputClass}
            />
          </div>

          <div>
            <label htmlFor="article-slug" className="text-sm font-medium">
              Slug
            </label>
            <input
              id="article-slug"
              maxLength={160}
              value={model.slug}
              onChange={event => update("slug", event.target.value)}
              placeholder="Se generează din titlu dacă îl lași gol"
              className={inputClass}
            />
            <p className="mt-1 text-sm text-slate-600">
              Doar litere mici, cifre și cratimă.
            </p>
          </div>

          <div>
            <label htmlFor="article-summary" className="text-sm font-medium">
              Rezumat
            </label>
            <textarea
              id="article-summary"
              maxLength={500}
              rows={3}
              value={model.summary}
              onChange={event => update("summary", event.target.value)}
              className={inputClass}
            />
          </div>

          <div>
            <label className="text-sm font-medium">Conținut</label>
            <RichTextEditor
              value={model.body}
              disabled={busy}
              onChange={value => update("body", value)}
            />
            <p className="mt-1 text-sm text-slate-600">
              HTML-ul este sanitizat de server la salvare.
            </p>
          </div>

          <div>
            <label htmlFor="article-published-at" className="text-sm font-medium">
              Data publicării (UTC, opțională)
            </label>
            <input
              id="article-published-at"
              type="datetime-local"
              value={model.publishedAt}
              onChange={event => update("publishedAt", event.target.value)}
              className={inputClass}
            />
            <p className="mt-1 text-sm text-slate-600">
              O dată viitoare programează apariția după publicare.
            </p>
          </div>
        </fieldset>

        <div className="flex flex-wrap gap-3">
          <button type="submit" disabled={busy} className={buttonClass}>
            {busy ? "Se salvează…" : "Salvează"}
          </button>
          <button
            type="button"
            disabled={busy}
            className={secondaryButtonClass}
            onClick={() => void togglePublication()}
          >
            {loaded?.isPublished ? "Depublică" : "Publică"}
          </button>
          {loaded && (
            <button
              type="button"
              disabled={busy}
              onClick={() => void remove()}
              className="rounded-md bg-red-700 px-3 py-2 text-sm font-medium text-white hover:bg-red-800 disabled:opacity-60"
            >
              Șterge
            </button>
          )}
        </div>
      </form>
    </section>
  );
}

export function ArticlesPage() {
  const auth = useAuth();
  const [articles, setArticles] = useState<ArticleListItem[]>([]);
  const [selectedSlug, setSelectedSlug] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  const [error, setError] = useState<ApiError | null>(null);

  useEffect(() => {
    if (auth.status !== "authenticated") return;

    let active = true;
    void getArticles()
      .then(result => {
        if (active) setArticles(result.items);
      })
      .catch(failure => {
        if (active) setError(asApiError(failure));
      });

    return () => {
      active = false;
    };
  }, [auth.status, revision]);

  if (auth.status === "loading") {
    return <main className="p-6">Se verifică sesiunea…</main>;
  }

  if (auth.status !== "authenticated") {
    return (
      <main className="p-6">
        <Link href="/" className="text-blue-700 underline">
          Autentifică-te ca administrator pentru a gestiona articolele.
        </Link>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-slate-50 p-4 text-slate-950 sm:p-6">
      <div className="mx-auto max-w-7xl space-y-6">
        <header className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <p className="text-sm font-semibold uppercase tracking-wider text-blue-700">
              Backoffice Portal
            </p>
            <h1 className="mt-1 text-3xl font-bold">Articole</h1>
          </div>
          <nav className="flex gap-3 text-sm font-medium">
            <Link href="/servicii" className="text-blue-700 underline">
              Servicii
            </Link>
            <Link href="/" className="text-blue-700 underline">
              Cont
            </Link>
          </nav>
        </header>

        {error && <ErrorMessage error={error} />}

        <div className="grid gap-6 lg:grid-cols-[19rem_minmax(0,1fr)]">
          <aside className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <button
              type="button"
              className={`${buttonClass} mb-4 w-full`}
              onClick={() => setSelectedSlug(null)}
            >
              Articol nou
            </button>
            {articles.length === 0 ? (
              <p className="text-sm text-slate-600">Nu există articole.</p>
            ) : (
              <nav aria-label="Articole configurate" className="space-y-2">
                {articles.map(article => (
                  <button
                    type="button"
                    key={article.slug}
                    onClick={() => setSelectedSlug(article.slug)}
                    className={
                      "w-full rounded-md border p-3 text-left " +
                      (selectedSlug === article.slug
                        ? "border-blue-700 bg-blue-50"
                        : "border-slate-200 hover:bg-slate-50")
                    }
                  >
                    <span className="block font-medium">{article.title}</span>
                    <span className="mt-1 block text-xs text-slate-500">
                      {article.slug}
                    </span>
                  </button>
                ))}
              </nav>
            )}
          </aside>

          <ArticleEditor
            key={selectedSlug ?? "new"}
            slug={selectedSlug}
            onSaved={article => {
              setSelectedSlug(article.slug);
              setRevision(current => current + 1);
            }}
            onDeleted={() => {
              setSelectedSlug(null);
              setRevision(current => current + 1);
            }}
          />
        </div>
      </div>
    </main>
  );
}
