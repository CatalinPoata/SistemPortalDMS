"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { useAuth } from "@/hooks/use-auth";
import {
  getNotifications,
  markAllNotificationsRead,
  markNotificationRead,
  type Notification,
} from "@/lib/notifications";
import { asApiError, type ApiError } from "@/lib/http";

export default function NotificationsPage() {
  const auth = useAuth();
  const [items, setItems] = useState<Notification[] | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  const isCitizen = auth.status === "authenticated" && auth.user.role === "Citizen";

  useEffect(() => {
    if (!isCitizen) {
      return;
    }

    void getNotifications()
      .then(result => setItems(result.items))
      .catch(failure => setError(asApiError(failure)))
  }, [isCitizen]);

  async function markRead(item: Notification) {
    if (item.isRead || busy) return;
    setBusy(true);
    setError(null);
    try {
      await markNotificationRead(item.id);
      setItems(current => (current ?? []).map(value =>
        value.id === item.id ? { ...value, isRead: true } : value));
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(false);
    }
  }

  async function markAllRead() {
    setBusy(true);
    setError(null);
    try {
      await markAllNotificationsRead();
      setItems(current => (current ?? []).map(item => ({ ...item, isRead: true })));
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(false);
    }
  }

  if (auth.status === "loading" || (isCitizen && items === null)) return <p>Se încarcă notificările…</p>;

  if (!isCitizen) {
    return <p>Autentifică-te cu un cont de cetățean pentru a vedea notificările.</p>;
  }

  return (
    <section className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="text-sm font-semibold uppercase tracking-wider text-blue-700">Contul meu</p>
          <h1 className="mt-1 text-3xl font-bold">Notificări</h1>
        </div>
        <button
          type="button"
          disabled={busy || !(items?.some(item => !item.isRead) ?? false)}
          onClick={() => void markAllRead()}
          className="rounded-lg border border-slate-300 px-4 py-2 text-sm font-medium hover:bg-slate-50 disabled:opacity-60"
        >
          Marchează toate ca citite
        </button>
      </div>

      {error && <ApiErrorPanel error={error} />}

      {items!.length === 0 ? (
        <p className="rounded-xl border border-slate-200 bg-white p-5">Nu ai notificări.</p>
      ) : (
        <ul className="space-y-3">
          {items!.map(item => (
            <li key={item.id} className={`rounded-xl border bg-white p-5 ${item.isRead ? "border-slate-200" : "border-blue-300 bg-blue-50"}`}>
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="space-y-1">
                  <h2 className="font-semibold">{item.subject}</h2>
                  <p>{item.body}</p>
                  <p className="text-sm text-slate-600">
                    {new Intl.DateTimeFormat("ro-RO", { dateStyle: "medium", timeStyle: "short", timeZone: "Europe/Bucharest" }).format(new Date(item.createdAt))}
                  </p>
                </div>
                {!item.isRead && (
                  <button type="button" disabled={busy} onClick={() => void markRead(item)} className="text-sm font-medium text-blue-700 underline disabled:opacity-60">
                    Marchează ca citită
                  </button>
                )}
              </div>
              {item.linkUrl.startsWith("/") && (
                <Link href={item.linkUrl} onClick={() => void markRead(item)} className="mt-3 inline-block text-sm font-medium text-blue-700 underline">
                  Deschide detaliile
                </Link>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
