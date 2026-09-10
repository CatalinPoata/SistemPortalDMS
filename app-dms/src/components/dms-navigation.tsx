"use client";

import Link from "next/link";
import { useAuth } from "@/hooks/use-auth";

const mainLinks = [
  ["/", "Panou"],
  ["/registratura", "Registratură"],
  ["/registratura/noua", "Poziție nouă"],
  ["/registre", "Registre & documente"],
  ["/rapoarte", "Rapoarte"],
  ["/compartimente", "Compartimente"],
  ["/securitate", "Securitate / TOTP"],
] as const;

const adminLinks = [
  ["/outbox", "Outbox"],
] as const;

export function DmsNavigation() {
  const auth = useAuth();
  const links = auth.status === "authenticated" && auth.user.role === "Admin"
    ? [...mainLinks, ...adminLinks]
    : mainLinks;

  return (
    <header className="sticky top-0 z-40 border-b border-slate-800 bg-slate-950 text-white shadow-lg">
      <div className="mx-auto flex max-w-7xl flex-wrap items-center gap-x-6 gap-y-3 px-4 py-3 sm:px-6">
        <Link href="/" className="flex items-center gap-3 font-semibold tracking-tight">
          <span className="grid size-9 place-items-center rounded-lg bg-blue-600 text-sm shadow-sm">DMS</span>
          <span>Registratură digitală</span>
        </Link>
        <nav aria-label="Navigație principală" className="flex flex-1 flex-wrap gap-x-1 gap-y-1 text-sm">
          {links.map(([href, label]) => (
            <Link
              key={href}
              href={href}
              className="rounded-md px-3 py-2 text-slate-200 transition hover:bg-slate-800 hover:text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-blue-300"
            >
              {label}
            </Link>
          ))}
        </nav>
      </div>
    </header>
  );
}
