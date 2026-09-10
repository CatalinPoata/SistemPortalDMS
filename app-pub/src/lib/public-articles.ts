import "client-only";

import { request } from "@/lib/http";

export type PublicArticleListItem = {
  id: string;
  slug: string;
  title: string;
  summary: string | null;
  publishedAt: string | null;
};

export type PublicArticle = PublicArticleListItem & {
  body: string;
};

type PagedArticles = {
  items: PublicArticleListItem[];
  page: number;
  pageSize: number;
  total: number;
};

export function getPublicArticles() {
  return request<PagedArticles>(
    "/api/public/articles?page=1&pageSize=6",
  );
}

export function getPublicArticle(slug: string) {
  return request<PublicArticle>(
    `/api/public/articles/${encodeURIComponent(slug)}`,
  );
}
