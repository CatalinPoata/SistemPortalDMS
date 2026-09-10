"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { apiRequest } from "@/lib/auth-store";
import { ApiError, asApiError } from "@/lib/http";

type Submission = {
  id: string;
  externalId: string;
  serviceCode: string;
  serviceTitle: string;
  status: string;
  applicantName: string;
  applicantEmail: string;
  submittedAt: string;
  registeredAt: string | null;
  registryDisplayNumber: string | null;
};

type Paged<T> = { items: T[]; page: number; pageSize: number; total: number };

const statuses = ["Submitted", "Registered", "InReview", "InfoRequested", "Completed", "Rejected", "Cancelled"];
const inputClass = "mt-1 w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-slate-950";
const buttonClass = "rounded-md bg-blue-700 px-3 py-2 text-sm font-medium text-white hover:bg-blue-800 disabled:opacity-60";

function ErrorMessage({ error }: { error: ApiError | null }) {
  if (!error) return null;
  return <ApiErrorPanel error={error} />;
}

export default function SubmissionsPage() {
  const [items, setItems] = useState<Submission[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState("");
  const [serviceCode, setServiceCode] = useState("");
  const [applicant, setApplicant] = useState("");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [revision, setRevision] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);

  useEffect(() => {
    let current = true;
    const query = new URLSearchParams({ page: String(page), pageSize: "25" });
    if (status) query.set("status", status);
    if (serviceCode.trim()) query.set("serviceCode", serviceCode.trim());
    if (applicant.trim()) query.set("applicant", applicant.trim());
    if (dateFrom) query.set("dateFrom", dateFrom);
    if (dateTo) query.set("dateTo", dateTo);
    void apiRequest<Paged<Submission>>(`/api/admin/submissions?${query}`)
      .then(result => { if (current) { setItems(result.items); setTotal(result.total); setError(null); } })
      .catch(failure => { if (current) setError(asApiError(failure)); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [page, status, serviceCode, applicant, dateFrom, dateTo, revision]);

  function applyFilters() {
    setPage(1);
    setRevision(value => value + 1);
  }
  const pageCount = Math.max(1, Math.ceil(total / 25));

  return <main className="min-h-screen bg-slate-100 p-4 text-slate-950 sm:p-6"><div className="mx-auto max-w-7xl space-y-6">
    <header className="flex flex-wrap items-center justify-between gap-3"><div><p className="text-sm font-semibold uppercase tracking-wider text-blue-700">Portal de servicii</p><h1 className="text-2xl font-semibold">Cereri depuse</h1></div><Link href="/" className="text-blue-700 underline">Înapoi la administrare</Link></header>
    <ErrorMessage error={error} />
    <section className="grid gap-3 rounded-xl bg-white p-4 shadow-sm sm:grid-cols-2 lg:grid-cols-4">
      <label>Serviciu (cod)<input className={inputClass} value={serviceCode} onChange={event => setServiceCode(event.target.value)} /></label>
      <label>Solicitant<input className={inputClass} value={applicant} onChange={event => setApplicant(event.target.value)} /></label>
      <label>Stare<select className={inputClass} value={status} onChange={event => setStatus(event.target.value)}><option value="">Toate</option>{statuses.map(item => <option key={item} value={item}>{item}</option>)}</select></label>
      <label>De la<input className={inputClass} type="date" value={dateFrom} onChange={event => setDateFrom(event.target.value)} /></label>
      <label>Până la<input className={inputClass} type="date" value={dateTo} onChange={event => setDateTo(event.target.value)} /></label>
      <div className="flex items-end"><button className={buttonClass} type="button" onClick={applyFilters}>Aplică filtrele</button></div>
    </section>
    <section className="overflow-x-auto rounded-xl bg-white shadow-sm"><table className="min-w-full text-left text-sm"><thead className="bg-slate-50"><tr>{["Depus la", "Solicitant", "Serviciu", "Stare", "Registratură", "Acțiuni"].map(label => <th key={label} className="p-3">{label}</th>)}</tr></thead><tbody>
      {items.map(item => <tr key={item.id} className="border-t border-slate-200"><td className="p-3">{new Date(item.submittedAt).toLocaleString("ro-RO")}</td><td className="p-3">{item.applicantName}<span className="block text-slate-600">{item.applicantEmail}</span></td><td className="p-3">{item.serviceTitle}<span className="block text-slate-600">{item.serviceCode}</span></td><td className="p-3">{item.status}</td><td className="p-3">{item.registryDisplayNumber ?? "—"}</td><td className="p-3"><Link href={`/cereri/${item.id}`} className="font-medium text-blue-700 underline">Detalii</Link></td></tr>)}
      {!loading && items.length === 0 && <tr><td className="p-4 text-slate-600" colSpan={6}>Nu există cereri pentru filtrele alese.</td></tr>}
    </tbody></table></section>
    <footer className="flex items-center justify-between"><p className="text-sm text-slate-600">{loading ? "Încărcăm…" : `${total} cereri`}</p><div className="flex gap-2"><button className={buttonClass} disabled={page <= 1 || loading} onClick={() => setPage(value => value - 1)}>Anterior</button><span className="self-center text-sm">Pagina {page} / {pageCount}</span><button className={buttonClass} disabled={page >= pageCount || loading} onClick={() => setPage(value => value + 1)}>Următor</button></div></footer>
  </div></main>;
}
