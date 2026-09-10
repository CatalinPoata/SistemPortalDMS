"use client";

import Link from "next/link";
import { useEffect, useState, type FormEvent } from "react";

import ApiErrorMessage from "@/components/api-error-message";
import { useAuth } from "@/hooks/use-auth";
import { apiRequest } from "@/lib/auth-store";
import { asApiError, type ApiError } from "@/lib/http";
import type { DocumentKind, RegistryType } from "@/lib/registry-types";

type RegistryDraft = Omit<RegistryType, "id">;
type DocumentKindDraft = Omit<DocumentKind, "id">;

const newRegistry = (): RegistryDraft => ({
  code: "", name: "", direction: "In", startNumber: 1,
  defaultDeadlineDays: 30, isClosed: false,
});
const newDocumentKind = (): DocumentKindDraft => ({
  code: "", name: "", isActive: true,
});
const inputClass = "mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-slate-950 focus:outline-2 focus:outline-blue-700";
const buttonClass = "rounded-lg bg-blue-700 px-4 py-2 text-sm font-medium text-white hover:bg-blue-800 disabled:opacity-50";

export default function RegistryAdministrationPage() {
  const auth = useAuth();
  const isAdmin = auth.user?.role === "Admin";
  const [registries, setRegistries] = useState<RegistryType[]>([]);
  const [documentKinds, setDocumentKinds] = useState<DocumentKind[]>([]);
  const [selectedRegistry, setSelectedRegistry] = useState<RegistryType | null>(null);
  const [selectedDocumentKind, setSelectedDocumentKind] = useState<DocumentKind | null>(null);
  const [registry, setRegistry] = useState<RegistryDraft>(newRegistry);
  const [documentKind, setDocumentKind] = useState<DocumentKindDraft>(newDocumentKind);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  async function load() {
    setLoading(true);
    try {
      const [nextRegistries, nextDocumentKinds] = await Promise.all([
        apiRequest<RegistryType[]>("/api/registry-types"),
        apiRequest<DocumentKind[]>(`/api/document-kinds${isAdmin ? "?includeInactive=true" : ""}`),
      ]);
      setRegistries(nextRegistries);
      setDocumentKinds(nextDocumentKinds);
      setError(null);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (auth.status === "authenticated") void load();
  }, [auth.status, isAdmin]);

  function chooseRegistry(item: RegistryType | null) {
    setSelectedRegistry(item);
    setRegistry(item ? { ...item } : newRegistry());
    setError(null);
    setMessage(null);
  }

  function chooseDocumentKind(item: DocumentKind | null) {
    setSelectedDocumentKind(item);
    setDocumentKind(item ? { ...item } : newDocumentKind());
    setError(null);
    setMessage(null);
  }

  async function saveRegistry(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true); setError(null); setMessage(null);
    try {
      const payload = selectedRegistry ? registry : {
        code: registry.code, name: registry.name, direction: registry.direction,
        startNumber: registry.startNumber,
        defaultDeadlineDays: registry.defaultDeadlineDays,
      };
      const saved = await apiRequest<RegistryType>(
        selectedRegistry ? `/api/registry-types/${selectedRegistry.id}` : "/api/registry-types",
        { method: selectedRegistry ? "PUT" : "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) },
      );
      chooseRegistry(saved);
      setMessage(selectedRegistry ? "Registrul a fost actualizat." : "Registrul a fost creat.");
      await load();
    } catch (failure) { setError(asApiError(failure)); }
    finally { setBusy(false); }
  }

  async function closeRegistry() {
    if (!selectedRegistry) return;
    setBusy(true); setError(null);
    try {
      const saved = await apiRequest<RegistryType>(`/api/registry-types/${selectedRegistry.id}/close`, { method: "POST" });
      chooseRegistry(saved);
      setMessage("Registrul a fost închis. Pozițiile noi sunt refuzate.");
      await load();
    } catch (failure) { setError(asApiError(failure)); }
    finally { setBusy(false); }
  }

  async function deleteRegistry() {
    if (!selectedRegistry || !window.confirm("Ștergi registrul? Registrele cu poziții nu pot fi șterse.")) return;
    setBusy(true); setError(null);
    try {
      await apiRequest<void>(`/api/registry-types/${selectedRegistry.id}`, { method: "DELETE" });
      chooseRegistry(null); setMessage("Registrul a fost șters."); await load();
    } catch (failure) { setError(asApiError(failure)); }
    finally { setBusy(false); }
  }

  async function saveDocumentKind(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true); setError(null); setMessage(null);
    try {
      const payload = selectedDocumentKind ? documentKind : { code: documentKind.code, name: documentKind.name };
      const saved = await apiRequest<DocumentKind>(
        selectedDocumentKind ? `/api/document-kinds/${selectedDocumentKind.id}` : "/api/document-kinds",
        { method: selectedDocumentKind ? "PUT" : "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload) },
      );
      chooseDocumentKind(saved);
      setMessage(selectedDocumentKind ? "Tipul de document a fost actualizat." : "Tipul de document a fost creat.");
      await load();
    } catch (failure) { setError(asApiError(failure)); }
    finally { setBusy(false); }
  }

  async function deleteDocumentKind() {
    if (!selectedDocumentKind || !window.confirm("Elimini tipul? Dacă este utilizat, va fi doar dezactivat.")) return;
    setBusy(true); setError(null);
    try {
      await apiRequest<void>(`/api/document-kinds/${selectedDocumentKind.id}`, { method: "DELETE" });
      chooseDocumentKind(null); setMessage("Tipul de document a fost eliminat sau dezactivat."); await load();
    } catch (failure) { setError(asApiError(failure)); }
    finally { setBusy(false); }
  }

  if (auth.status === "loading") return <p className="p-6">Se verifică sesiunea…</p>;
  if (auth.status === "anonymous") return <p className="p-6">Autentifică-te pentru catalogul DMS.</p>;
  if (auth.status === "error") return <main className="p-6"><ApiErrorMessage error={auth.error} /></main>;

  return (
    <main className="min-h-screen bg-slate-100 p-4 text-slate-950 sm:p-6">
      <div className="mx-auto max-w-7xl space-y-6">
        <header className="flex flex-wrap items-center justify-between gap-3">
          <div><p className="text-sm font-semibold uppercase tracking-wider text-blue-700">Configurare DMS</p><h1 className="text-2xl font-semibold">Registre și documente</h1></div>
          <Link href="/registratura" className="text-blue-700 underline">Înapoi la registratură</Link>
        </header>
        {error && <ApiErrorMessage error={error} />}
        {message && <p role="status" className="rounded-lg bg-emerald-50 p-3 text-emerald-900">{message}</p>}
        {loading ? <p role="status">Se încarcă catalogul…</p> : <div className="grid gap-6 xl:grid-cols-2">
          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <CatalogHeader title="Registre" description="Direcția stabilește ce poziții acceptă registrul." canEdit={isAdmin} onNew={() => chooseRegistry(null)} label="Registru nou" />
            <ul className="mt-4 space-y-2">{registries.map(item => <li key={item.id}><button type="button" onClick={() => isAdmin && chooseRegistry(item)} className="w-full rounded-lg border border-slate-200 p-3 text-left hover:bg-slate-50"><b>{item.code} — {item.name}</b><span className="mt-1 block text-sm text-slate-600">{directionLabel(item.direction)} · Nr. inițial {item.startNumber} · Termen {item.defaultDeadlineDays} zile · {item.isClosed ? "Închis" : "Deschis"}</span></button></li>)}{registries.length === 0 && <li className="text-slate-600">Nu există registre.</li>}</ul>
            {isAdmin && <RegistryForm selected={selectedRegistry} draft={registry} busy={busy} onChange={setRegistry} onSave={saveRegistry} onClose={closeRegistry} onDelete={deleteRegistry} />}
          </section>
          <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
            <CatalogHeader title="Tipuri de document" description="Tipurile inactive se păstrează pentru istoricul documentelor." canEdit={isAdmin} onNew={() => chooseDocumentKind(null)} label="Tip nou" />
            <ul className="mt-4 space-y-2">{documentKinds.map(item => <li key={item.id}><button type="button" onClick={() => isAdmin && chooseDocumentKind(item)} className="w-full rounded-lg border border-slate-200 p-3 text-left hover:bg-slate-50"><b>{item.code} — {item.name}</b><span className="mt-1 block text-sm text-slate-600">{item.isActive ? "Activ" : "Inactiv"}</span></button></li>)}{documentKinds.length === 0 && <li className="text-slate-600">Nu există tipuri de document.</li>}</ul>
            {isAdmin && <DocumentKindForm selected={selectedDocumentKind} draft={documentKind} busy={busy} onChange={setDocumentKind} onSave={saveDocumentKind} onDelete={deleteDocumentKind} />}
          </section>
        </div>}
      </div>
    </main>
  );
}

function CatalogHeader({ title, description, canEdit, onNew, label }: { title: string; description: string; canEdit: boolean; onNew: () => void; label: string }) {
  return <div className="flex flex-wrap items-start justify-between gap-3"><div><h2 className="text-xl font-semibold">{title}</h2><p className="mt-1 text-sm text-slate-600">{description}</p></div>{canEdit && <button type="button" onClick={onNew} className={buttonClass}>{label}</button>}</div>;
}

function RegistryForm({ selected, draft, busy, onChange, onSave, onClose, onDelete }: { selected: RegistryType | null; draft: RegistryDraft; busy: boolean; onChange: (value: RegistryDraft) => void; onSave: (event: FormEvent<HTMLFormElement>) => void; onClose: () => void; onDelete: () => void }) {
  return <form onSubmit={onSave} className="mt-6 space-y-4 border-t border-slate-200 pt-5"><h3 className="text-lg font-semibold">{selected ? "Editează registru" : "Registru nou"}</h3><label className="block text-sm font-medium">Cod<input required maxLength={30} value={draft.code} onChange={event => onChange({ ...draft, code: event.target.value.toUpperCase() })} className={inputClass} /></label><label className="block text-sm font-medium">Denumire<input required maxLength={200} value={draft.name} onChange={event => onChange({ ...draft, name: event.target.value })} className={inputClass} /></label><div className="grid gap-3 sm:grid-cols-3"><label className="text-sm font-medium">Direcție<select value={draft.direction} onChange={event => onChange({ ...draft, direction: event.target.value as RegistryType["direction"] })} className={inputClass}><option value="In">Intrări</option><option value="Out">Ieșiri</option><option value="Both">Ambele</option></select></label><label className="text-sm font-medium">Număr inițial<input required min={1} type="number" value={draft.startNumber} onChange={event => onChange({ ...draft, startNumber: Number(event.target.value) })} className={inputClass} /></label><label className="text-sm font-medium">Termen (zile)<input required min={0} type="number" value={draft.defaultDeadlineDays} onChange={event => onChange({ ...draft, defaultDeadlineDays: Number(event.target.value) })} className={inputClass} /></label></div>{selected && <label className="flex gap-2 text-sm"><input type="checkbox" checked={draft.isClosed} onChange={event => onChange({ ...draft, isClosed: event.target.checked })} />Închis</label>}<div className="flex flex-wrap gap-3"><button disabled={busy} className={buttonClass}>{busy ? "Se salvează…" : "Salvează"}</button>{selected && !selected.isClosed && <button type="button" disabled={busy} onClick={onClose} className="rounded-lg border border-amber-700 px-4 py-2 text-sm font-medium text-amber-800 hover:bg-amber-50">Închide</button>}{selected && <button type="button" disabled={busy} onClick={onDelete} className="rounded-lg border border-red-700 px-4 py-2 text-sm font-medium text-red-700 hover:bg-red-50">Șterge</button>}</div></form>;
}

function DocumentKindForm({ selected, draft, busy, onChange, onSave, onDelete }: { selected: DocumentKind | null; draft: DocumentKindDraft; busy: boolean; onChange: (value: DocumentKindDraft) => void; onSave: (event: FormEvent<HTMLFormElement>) => void; onDelete: () => void }) {
  return <form onSubmit={onSave} className="mt-6 space-y-4 border-t border-slate-200 pt-5"><h3 className="text-lg font-semibold">{selected ? "Editează tip de document" : "Tip de document nou"}</h3><label className="block text-sm font-medium">Cod<input required maxLength={30} value={draft.code} onChange={event => onChange({ ...draft, code: event.target.value.toUpperCase() })} className={inputClass} /></label><label className="block text-sm font-medium">Denumire<input required maxLength={150} value={draft.name} onChange={event => onChange({ ...draft, name: event.target.value })} className={inputClass} /></label>{selected && <label className="flex gap-2 text-sm"><input type="checkbox" checked={draft.isActive} onChange={event => onChange({ ...draft, isActive: event.target.checked })} />Activ</label>}<div className="flex flex-wrap gap-3"><button disabled={busy} className={buttonClass}>{busy ? "Se salvează…" : "Salvează"}</button>{selected && <button type="button" disabled={busy} onClick={onDelete} className="rounded-lg border border-red-700 px-4 py-2 text-sm font-medium text-red-700 hover:bg-red-50">Elimină / dezactivează</button>}</div></form>;
}

function directionLabel(direction: RegistryType["direction"]) {
  return direction === "In" ? "Intrări" : direction === "Out" ? "Ieșiri" : "Intrări și ieșiri";
}
