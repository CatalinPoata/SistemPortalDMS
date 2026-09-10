"use client";

import Link from "next/link";
import { useEffect, useState, type FormEvent } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { useAuth } from "@/hooks/use-auth";
import { asApiError, type ApiError } from "@/lib/http";
import {
  getUsers,
  setUserActive,
  setUserRole,
  type PortalUser,
} from "@/lib/users";

function ErrorMessage({ error }: { error: ApiError | null }) {
  if (!error) return null;
  return <ApiErrorPanel error={error} />;
}

export default function UsersPage() {
  const auth = useAuth();
  const [items, setItems] = useState<PortalUser[]>([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [search, setSearch] = useState("");
  const [appliedSearch, setAppliedSearch] = useState("");
  const [loading, setLoading] = useState(true);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [error, setError] = useState<ApiError | null>(null);

  async function load(nextPage = page, nextSearch = appliedSearch) {
    setLoading(true);
    setError(null);

    try {
      const result = await getUsers(nextPage, nextSearch);
      setItems(result.items);
      setPage(result.page);
      setTotal(result.total);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    if (auth.status === "authenticated" && auth.user.role === "Admin") {
      const timeout = window.setTimeout(() => {
        void load(1, appliedSearch);
      }, 0);

      return () => window.clearTimeout(timeout);
    }

    return undefined;
  }, [auth.status, auth.user, appliedSearch]);

  async function mutate(action: () => Promise<PortalUser>) {
    try {
      const updated = await action();
      setItems(current => current.map(item =>
        item.id === updated.id ? updated : item,
      ));
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusyId(null);
    }
  }

  function applySearch(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setAppliedSearch(search);
  }

  if (auth.status === "loading") return <p>Se verifică sesiunea…</p>;
  if (auth.status === "anonymous") {
    return <p>Autentifică-te ca administrator pentru a gestiona utilizatorii.</p>;
  }
  if (auth.status === "error") return <ErrorMessage error={auth.error} />;
  if (auth.status === "totp") {
    return <p>Completează verificarea TOTP pentru a accesa administrarea utilizatorilor.</p>;
  }
  if (auth.user.role !== "Admin") return <p>Acces permis numai administratorilor.</p>;

  const lastPage = Math.max(1, Math.ceil(total / 25));

  return (
    <section className="space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <Link href="/" className="text-sm text-blue-700 underline">← Panou administrare</Link>
        <p className="text-sm text-slate-600">{total} utilizatori</p>
      </div>

      <form onSubmit={applySearch} className="flex flex-wrap gap-2">
        <label className="sr-only" htmlFor="user-search">Caută utilizator</label>
        <input
          id="user-search"
          value={search}
          onChange={event => setSearch(event.target.value)}
          placeholder="Caută după nume sau e-mail"
          className="min-w-64 rounded-lg border border-slate-300 px-3 py-2"
        />
        <button className="rounded-lg bg-blue-700 px-4 py-2 font-medium text-white hover:bg-blue-800">
          Caută
        </button>
      </form>

      {error && <ErrorMessage error={error} />}
      {loading ? <p role="status">Se încarcă utilizatorii…</p> : (
        <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white">
          <table className="min-w-full divide-y divide-slate-200 text-left text-sm">
            <thead className="bg-slate-50 text-slate-700">
              <tr>
                <th className="px-4 py-3">Utilizator</th>
                <th className="px-4 py-3">Confirmat</th>
                <th className="px-4 py-3">Rol</th>
                <th className="px-4 py-3">Stare</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {items.map(user => {
                const busy = busyId === user.id;
                const isSelf = user.id === auth.user.id;

                return (
                  <tr key={user.id}>
                    <td className="px-4 py-3">
                      <p className="font-medium text-slate-950">{user.fullName}</p>
                      <p className="text-slate-600">{user.email}</p>
                    </td>
                    <td className="px-4 py-3">{user.emailConfirmed ? "Da" : "Nu"}</td>
                    <td className="px-4 py-3">
                      <select
                        value={user.role}
                        disabled={busy || isSelf}
                        onChange={event => {
                          setBusyId(user.id);
                          void mutate(() => setUserRole(
                            user.id,
                            event.target.value as PortalUser["role"],
                          ));
                        }}
                        className="rounded border border-slate-300 bg-white px-2 py-1"
                      >
                        <option value="Citizen">Cetățean</option>
                        <option value="Admin">Administrator</option>
                      </select>
                    </td>
                    <td className="px-4 py-3">
                      <button
                        type="button"
                        disabled={busy || isSelf}
                        onClick={() => {
                          setBusyId(user.id);
                          void mutate(() => setUserActive(user.id, !user.isActive));
                        }}
                        className={user.isActive
                          ? "rounded border border-red-700 px-3 py-1 text-red-700 hover:bg-red-50 disabled:opacity-50"
                          : "rounded border border-emerald-700 px-3 py-1 text-emerald-700 hover:bg-emerald-50 disabled:opacity-50"}
                      >
                        {busy ? "Se salvează…" : user.isActive ? "Dezactivează" : "Activează"}
                      </button>
                    </td>
                  </tr>
                );
              })}
              {items.length === 0 && (
                <tr><td colSpan={4} className="px-4 py-6 text-center text-slate-600">Nu există utilizatori.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      <div className="flex items-center gap-3">
        <button
          type="button"
          disabled={loading || page <= 1}
          onClick={() => void load(page - 1)}
          className="rounded border border-slate-300 px-3 py-2 disabled:opacity-50"
        >
          Înapoi
        </button>
        <span className="text-sm text-slate-600">Pagina {page} din {lastPage}</span>
        <button
          type="button"
          disabled={loading || page >= lastPage}
          onClick={() => void load(page + 1)}
          className="rounded border border-slate-300 px-3 py-2 disabled:opacity-50"
        >
          Înainte
        </button>
      </div>
    </section>
  );
}
