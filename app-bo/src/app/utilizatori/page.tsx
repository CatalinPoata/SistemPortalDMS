import UsersPage from "@/components/users-page";

export default function Users() {
  return (
    <main className="min-h-screen bg-slate-100 p-4 text-slate-950 sm:p-8">
      <div className="mx-auto max-w-7xl">
        <header className="mb-6">
          <p className="text-sm font-semibold uppercase tracking-wider text-blue-700">Backoffice</p>
          <h1 className="mt-2 text-3xl font-bold">Utilizatori</h1>
          <p className="mt-2 text-slate-600">Activează, dezactivează și administrează rolurile conturilor Portalului.</p>
        </header>
        <UsersPage />
      </div>
    </main>
  );
}
