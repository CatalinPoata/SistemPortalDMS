"use client";

import Link from "next/link";
import { useEffect, useState, type FormEvent } from "react";

import ApiErrorMessage from "@/components/api-error-message";
import { useAuth } from "@/hooks/use-auth";
import { apiRequest } from "@/lib/auth-store";
import { asApiError, type ApiError } from "@/lib/http";

type Department = {
  id: string;
  code: string;
  name: string;
  managerUserId: string | null;
  managerEmail: string | null;
  isActive: boolean;
};

type DepartmentDraft = {
  code: string;
  name: string;
  isActive: boolean;
  managerUserId: string | null;
};

const emptyDraft = (): DepartmentDraft => ({
  code: "",
  name: "",
  isActive: true,
  managerUserId: null,
});

export default function DepartmentsPage() {
  const auth = useAuth();
  const user = auth.user;
  const [items, setItems] = useState<Department[]>([]);
  const [selected, setSelected] = useState<Department | null>(null);
  const [draft, setDraft] = useState<DepartmentDraft>(emptyDraft);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  async function load() {
    setLoading(true);
    try {
      const result = await apiRequest<Department[]>(
        "/api/departments?includeInactive=true",
      );
      setItems(result);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (auth.status === "authenticated" && user?.role === "Admin") {
      void load();
    }
  }, [auth.status, user]);

  function startNew() {
    setSelected(null);
    setDraft(emptyDraft());
    setError(null);
    setMessage(null);
  }

  function select(department: Department) {
    setSelected(department);
    setDraft({
      code: department.code,
      name: department.name,
      isActive: department.isActive,
      managerUserId: department.managerUserId,
    });
    setError(null);
    setMessage(null);
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    setMessage(null);

    try {
      const path = selected ? `/api/departments/${selected.id}` : "/api/departments";
      const method = selected ? "PUT" : "POST";
      const saved = await apiRequest<Department>(path, {
        method,
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(draft),
      });
      setSelected(saved);
      setDraft({
        code: saved.code,
        name: saved.name,
        isActive: saved.isActive,
        managerUserId: saved.managerUserId,
      });
      setMessage(selected ? "Compartimentul a fost actualizat." : "Compartimentul a fost creat.");
      await load();
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(false);
    }
  }

  async function deactivate() {
    if (!selected || busy || !window.confirm("Dezactivezi compartimentul?")) return;

    setBusy(true);
    setError(null);
    try {
      await apiRequest<void>(`/api/departments/${selected.id}`, { method: "DELETE" });
      setMessage("Compartimentul a fost dezactivat.");
      setDraft(previous => ({ ...previous, isActive: false }));
      setSelected(previous => previous ? { ...previous, isActive: false } : previous);
      await load();
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(false);
    }
  }

  if (auth.status === "loading") return <p>Se verifică sesiunea…</p>;
  if (auth.status === "anonymous") return <p>Autentifică-te ca administrator.</p>;
  if (auth.status === "error") return <ApiErrorMessage error={auth.error} />;
  if (user?.role !== "Admin") return <p>Acces permis numai administratorilor.</p>;

  return (
    <main className="min-h-screen bg-slate-100 p-4 text-slate-950 sm:p-6">
      <div className="mx-auto grid max-w-6xl gap-6 lg:grid-cols-[minmax(16rem,0.85fr)_minmax(0,1.15fr)]">
        <section className="space-y-4 rounded-2xl border border-slate-200 bg-white p-5">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <Link href="/" className="text-sm text-blue-700 underline">← Cont</Link>
              <h1 className="mt-2 text-2xl font-semibold">Compartimente</h1>
            </div>
            <button type="button" onClick={startNew} className="rounded-lg bg-blue-700 px-3 py-2 text-sm font-medium text-white hover:bg-blue-800">
              Nou
            </button>
          </div>

          {loading ? <p role="status">Se încarcă compartimentele…</p> : (
            <ul className="space-y-2">
              {items.map(item => (
                <li key={item.id}>
                  <button
                    type="button"
                    onClick={() => select(item)}
                    className={selected?.id === item.id
                      ? "w-full rounded-lg border border-blue-700 bg-blue-50 p-3 text-left"
                      : "w-full rounded-lg border border-slate-200 p-3 text-left hover:bg-slate-50"}
                  >
                    <span className="block font-medium">{item.code} — {item.name}</span>
                    <span className="mt-1 block text-sm text-slate-600">
                      {item.isActive ? "Activ" : "Inactiv"}
                      {item.managerEmail ? ` · Manager: ${item.managerEmail}` : ""}
                    </span>
                  </button>
                </li>
              ))}
              {items.length === 0 && <li className="text-slate-600">Nu există compartimente.</li>}
            </ul>
          )}
        </section>

        <section className="rounded-2xl border border-slate-200 bg-white p-5">
          <h2 className="text-xl font-semibold">{selected ? "Editează compartiment" : "Compartiment nou"}</h2>
          <p className="mt-1 text-sm text-slate-600">Managerul curent este păstrat la editare. Atribuirea unui manager este disponibilă prin contractul API al compartimentelor.</p>
          <div className="mt-4 space-y-4">
            {error && <ApiErrorMessage error={error} />}
            {message && <p role="status" className="rounded-lg bg-emerald-50 p-3 text-emerald-900">{message}</p>}
            <form onSubmit={save} className="space-y-4">
              <label className="block text-sm font-medium">
                Cod
                <input
                  required
                  maxLength={20}
                  value={draft.code}
                  onChange={event => setDraft(previous => ({ ...previous, code: event.target.value.toUpperCase() }))}
                  className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2"
                />
              </label>
              <label className="block text-sm font-medium">
                Denumire
                <input
                  required
                  maxLength={200}
                  value={draft.name}
                  onChange={event => setDraft(previous => ({ ...previous, name: event.target.value }))}
                  className="mt-1 w-full rounded-lg border border-slate-300 px-3 py-2"
                />
              </label>
              {selected?.managerEmail && <p className="text-sm text-slate-600">Manager curent: {selected.managerEmail}</p>}
              {selected && (
                <label className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={draft.isActive}
                    onChange={event => setDraft(previous => ({ ...previous, isActive: event.target.checked }))}
                  />
                  Activ
                </label>
              )}
              <div className="flex flex-wrap gap-3">
                <button disabled={busy} className="rounded-lg bg-blue-700 px-4 py-2 font-medium text-white hover:bg-blue-800 disabled:opacity-50">
                  {busy ? "Se salvează…" : "Salvează"}
                </button>
                {selected && selected.isActive && (
                  <button type="button" disabled={busy} onClick={() => void deactivate()} className="rounded-lg border border-red-700 px-4 py-2 font-medium text-red-700 hover:bg-red-50 disabled:opacity-50">
                    Dezactivează
                  </button>
                )}
              </div>
            </form>
          </div>
        </section>
      </div>
    </main>
  );
}
