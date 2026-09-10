import { connection } from "next/server";

import PortalHeader from "@/components/portal-header";
import PublicRegistriesPage from "@/components/public-registries-page";

export default async function PublicRegistriesRoute() {
  await connection();
  return <div className="min-h-screen bg-slate-50 text-slate-950"><PortalHeader /><main className="mx-auto max-w-5xl px-4 py-10 sm:px-6"><header className="mb-8"><p className="text-sm font-semibold uppercase tracking-wider text-blue-700">Transparență</p><h1 className="mt-2 text-3xl font-bold">Registre publice</h1></header><PublicRegistriesPage /></main></div>;
}
