import "client-only";

import { request } from "@/lib/http";

export type PublicRegistryDocument = {
  id: string;
  originalName: string;
  contentType: string;
  sizeBytes: number;
  displayOrder: number;
  downloadUrl: string;
};

export type PublicRegistryEntry = {
  id: string;
  positionNumber: string;
  title: string;
  entryDate: string;
  description: string | null;
  isPublished: boolean;
  documents: PublicRegistryDocument[];
};

export type PublicRegistry = {
  id: string;
  code: string;
  name: string;
  description: string | null;
  isPublished: boolean;
  displayOrder: number;
  entries: PublicRegistryEntry[];
};

type Paged<T> = { items: T[]; page: number; pageSize: number; total: number };
export type PublicRegistryListItem = Omit<PublicRegistry, "entries"> & { entryCount: number };

export function getPublicRegistries() {
  return request<Paged<PublicRegistryListItem>>("/api/public/registries?page=1&pageSize=100");
}

export function getPublicRegistry(code: string, filters: { search?: string; from?: string; to?: string } = {}) {
  const params = new URLSearchParams({ page: "1", pageSize: "100" });
  if (filters.search) params.set("search", filters.search);
  if (filters.from) params.set("from", filters.from);
  if (filters.to) params.set("to", filters.to);
  return request<PublicRegistry>(`/api/public/registries/${encodeURIComponent(code)}?${params.toString()}`);
}
