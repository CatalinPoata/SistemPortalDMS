"use client";

import { useEffect, useState, type FormEvent } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { asApiError, type ApiError } from "@/lib/http";
import { getPublicRegistries, getPublicRegistry, type PublicRegistry, type PublicRegistryListItem } from "@/lib/public-registries";

function formatSize(bytes: number) {
  return `${Math.max(1, Math.round(bytes / 1024))} KB`;
}

export default function PublicRegistriesPage() {
  const [registries, setRegistries] = useState<PublicRegistryListItem[]>([]);
  const [selected, setSelected] = useState<PublicRegistry | null>(null);
  const [search, setSearch] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  useEffect(() => {
    let active = true;
    void getPublicRegistries().then(result => { if (active) setRegistries(result.items); }).catch(failure => { if (active) setError(asApiError(failure)); }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, []);

  async function open(code: string, filters = { search, from, to }) {
    setBusy(true); setError(null);
    try { setSelected(await getPublicRegistry(code, filters)); } catch (failure) { setError(asApiError(failure)); } finally { setBusy(false); }
  }

  function filter(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (selected) void open(selected.code);
  }

  if (loading) return <p className="text-slate-600">Se încarcă registrele…</p>;
  return <div className="space-y-6">
    {error && <ApiErrorPanel error={error} />}
    {!selected ? <>{registries.length === 0 ? <p className="text-slate-600">Nu există registre publicate.</p> : <div className="grid gap-4 md:grid-cols-2">{registries.map(registry => <button key={registry.code} type="button" onClick={() => void open(registry.code, { search: "", from: "", to: "" })} className="rounded-2xl border border-slate-200 bg-white p-5 text-left shadow-sm hover:border-blue-500"><h2 className="text-xl font-semibold">{registry.name}</h2><p className="mt-1 text-sm text-slate-500">{registry.entryCount} poziții · {registry.code}</p>{registry.description && <p className="mt-3 text-slate-600">{registry.description}</p>}</button>)}</div>}</> : <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm"><button type="button" onClick={() => setSelected(null)} className="text-sm text-blue-700 underline">← Înapoi la registre</button><h2 className="mt-4 text-2xl font-bold">{selected.name}</h2>{selected.description && <p className="mt-2 text-slate-600">{selected.description}</p>}<form onSubmit={filter} className="mt-6 grid gap-3 rounded-xl bg-slate-50 p-4 md:grid-cols-4"><label className="md:col-span-2">Caută după titlu<input value={search} onChange={event => setSearch(event.target.value)} className="mt-1 w-full rounded-lg border border-slate-300 p-2" /></label><label>De la<input type="date" value={from} onChange={event => setFrom(event.target.value)} className="mt-1 w-full rounded-lg border border-slate-300 p-2" /></label><label>Până la<input type="date" value={to} onChange={event => setTo(event.target.value)} className="mt-1 w-full rounded-lg border border-slate-300 p-2" /></label><button type="submit" disabled={busy} className="rounded-lg bg-blue-700 px-4 py-2 text-white md:col-span-4 md:justify-self-start">Filtrează</button></form><div className="mt-6 space-y-4">{selected.entries.length === 0 ? <p className="text-slate-600">Nu există poziții pentru filtrul ales.</p> : selected.entries.map(entry => <article key={entry.id} className="rounded-xl border border-slate-200 p-4"><div className="flex flex-wrap items-start justify-between gap-3"><div><p className="text-sm font-semibold text-blue-700">{entry.positionNumber} · {entry.entryDate}</p><h3 className="mt-1 text-lg font-semibold">{entry.title}</h3>{entry.description && <p className="mt-2 text-slate-600">{entry.description}</p>}</div></div>{entry.documents.length > 0 && <ul className="mt-4 space-y-2">{entry.documents.map(document => <li key={document.id}><a href={document.downloadUrl} className="text-blue-700 underline">{document.originalName}</a><span className="ml-2 text-sm text-slate-500">({formatSize(document.sizeBytes)})</span></li>)}</ul>}</article>)}</div></section>}
  </div>;
}
