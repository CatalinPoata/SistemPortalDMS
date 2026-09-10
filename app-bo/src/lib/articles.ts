import "client-only";

import { apiRequest } from "@/lib/auth-store";

export type ArticleListItem = {
  id: string;
  slug: string;
  title: string;
  summary: string | null;
  publishedAt: string | null;
};

export type ArticleDetails = ArticleListItem & {
  body: string;
  isPublished: boolean;
  createdAt: string;
  updatedAt: string | null;
};

export type PagedArticles = {
  items: ArticleListItem[];
  page: number;
  pageSize: number;
  total: number;
};

export type ArticleWrite = {
  slug: string;
  title: string;
  summary: string;
  body: string;
  publishedAt: string;
};

export function emptyArticle(): ArticleWrite {
  return {
    slug: "",
    title: "",
    summary: "",
    body: "<p></p>",
    publishedAt: "",
  };
}

export function toWriteModel(article: ArticleDetails): ArticleWrite {
  return {
    slug: article.slug,
    title: article.title,
    summary: article.summary ?? "",
    body: article.body,
    publishedAt: article.publishedAt?.slice(0, 16) ?? "",
  };
}

export function getArticles() {
  return apiRequest<PagedArticles>(
    "/api/articles?page=1&pageSize=100",
  );
}

export function getArticle(slug: string) {
  return apiRequest<ArticleDetails>(
    `/api/articles/${encodeURIComponent(slug)}`,
  );
}

export function createArticle(article: ArticleWrite) {
  return apiRequest<ArticleDetails>("/api/articles", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(toPayload(article)),
  });
}

export function updateArticle(currentSlug: string, article: ArticleWrite) {
  return apiRequest<ArticleDetails>(
    `/api/articles/${encodeURIComponent(currentSlug)}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(toPayload(article)),
    },
  );
}

export function publishArticle(slug: string) {
  return apiRequest<ArticleDetails>(
    `/api/articles/${encodeURIComponent(slug)}/publish`,
    { method: "POST" },
  );
}

export function unpublishArticle(slug: string) {
  return apiRequest<ArticleDetails>(
    `/api/articles/${encodeURIComponent(slug)}/unpublish`,
    { method: "POST" },
  );
}

export function deleteArticle(slug: string) {
  return apiRequest<void>(`/api/articles/${encodeURIComponent(slug)}`, {
    method: "DELETE",
  });
}

function toPayload(article: ArticleWrite) {
  return {
    slug: article.slug.trim() || null,
    title: article.title.trim(),
    summary: article.summary.trim() || null,
    body: article.body,
    publishedAt: article.publishedAt
      ? new Date(article.publishedAt).toISOString()
      : null,
  };
}
