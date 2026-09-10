import PublicRegistriesPage from "@/components/public-registries-page";

export default function PublicRegistries() {
  return <main className="min-h-screen bg-slate-100 p-4 text-slate-950 sm:p-8"><div className="mx-auto max-w-7xl"><header className="mb-6"><p className="text-sm font-semibold uppercase tracking-wider text-blue-700">Backoffice</p><h1 className="mt-2 text-3xl font-bold">Registre publice</h1><p className="mt-2 text-slate-600">Administrează pozițiile și documentele publicate.</p></header><PublicRegistriesPage /></div></main>;
}
