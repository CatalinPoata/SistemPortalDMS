import "client-only";

import { apiRequest } from "@/lib/auth-store";

export type RegistryDocument = { id: string; originalName: string; contentType: string; sizeBytes: number; displayOrder: number; downloadUrl: string };
export type RegistryEntry = { id: string; positionNumber: string; title: string; entryDate: string; description: string | null; isPublished: boolean; documents: RegistryDocument[] };
export type Registry = { id: string; code: string; name: string; description: string | null; isPublished: boolean; displayOrder: number; entries: RegistryEntry[] };
export type RegistryListItem = Omit<Registry, "entries"> & { entryCount: number };
export type RegistryWrite = { code: string; name: string; description: string; displayOrder: number; isPublished: boolean };
export type EntryWrite = { positionNumber: string; title: string; entryDate: string; description: string; isPublished: boolean };
type Paged<T> = { items: T[]; page: number; pageSize: number; total: number };

export function emptyRegistry(): RegistryWrite { return { code: "", name: "", description: "", displayOrder: 0, isPublished: false }; }
export function emptyEntry(): EntryWrite { return { positionNumber: "", title: "", entryDate: new Date().toISOString().slice(0, 10), description: "", isPublished: false }; }
export function getRegistries() { return apiRequest<Paged<RegistryListItem>>("/api/public-registries?page=1&pageSize=100"); }
export function getRegistry(code: string) { return apiRequest<Registry>(`/api/public-registries/${encodeURIComponent(code)}`); }
export function createRegistry(value: RegistryWrite) { return apiRequest<Registry>("/api/public-registries", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(value) }); }
export function updateRegistry(code: string, value: RegistryWrite) { return apiRequest<Registry>(`/api/public-registries/${encodeURIComponent(code)}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(value) }); }
export function deleteRegistry(code: string) { return apiRequest<void>(`/api/public-registries/${encodeURIComponent(code)}`, { method: "DELETE" }); }
export function publishRegistry(code: string) { return apiRequest<Registry>(`/api/public-registries/${encodeURIComponent(code)}/publish`, { method: "POST" }); }
export function unpublishRegistry(code: string) { return apiRequest<Registry>(`/api/public-registries/${encodeURIComponent(code)}/unpublish`, { method: "POST" }); }
export function createEntry(code: string, value: EntryWrite) { return apiRequest<RegistryEntry>(`/api/public-registries/${encodeURIComponent(code)}/entries`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(value) }); }
export function updateEntry(code: string, id: string, value: EntryWrite) { return apiRequest<RegistryEntry>(`/api/public-registries/${encodeURIComponent(code)}/entries/${id}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(value) }); }
export function deleteEntry(code: string, id: string) { return apiRequest<void>(`/api/public-registries/${encodeURIComponent(code)}/entries/${id}`, { method: "DELETE" }); }
export function publishEntry(code: string, id: string, publish: boolean) { return apiRequest<RegistryEntry>(`/api/public-registries/${encodeURIComponent(code)}/entries/${id}/${publish ? "publish" : "unpublish"}`, { method: "POST" }); }
export function uploadDocument(code: string, entryId: string, file: File, displayOrder = 0) { const body = new FormData(); body.append("file", file); body.append("displayOrder", String(displayOrder)); return apiRequest<RegistryEntry>(`/api/public-registries/${encodeURIComponent(code)}/entries/${entryId}/documents`, { method: "POST", body }); }
export function deleteDocument(code: string, entryId: string, documentId: string) { return apiRequest<void>(`/api/public-registries/${encodeURIComponent(code)}/entries/${entryId}/documents/${documentId}`, { method: "DELETE" }); }
