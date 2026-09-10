"use client";

import Link from "next/link";
import { useState, type FormEvent } from "react";
import { useAuth } from "@/hooks/use-auth";
import { useApiQuery } from "@/hooks/use-api-query";
import ApiErrorMessage from "@/components/api-error-message";

import {
  statusLabels,
  type RegistryType,
  type Department,
  type RegistryEntry,
  type PagedResponse,
} from "@/lib/registry-types";

const inputClass =
  "mt-1 w-full rounded-lg border border-slate-300 bg-white p-2 text-base " +
  "focus:outline-2 focus:outline-blue-700";

const buttonClass =
  "rounded-lg bg-blue-700 px-4 py-2 text-white hover:bg-blue-800 " +
  "disabled:cursor-not-allowed disabled:opacity-50 " +
  "focus-visible:outline-2 focus-visible:outline-offset-2 " +
  "focus-visible:outline-blue-700";

const defaultQuery = new URLSearchParams({
  page: "1",
  pageSize: "25",
  sort: "registeredAt:desc",
}).toString();

const filterInputs = [
  ["year", "An", "number"],
  ["fromDate", "De la data (UTC)", "date"],
  ["toDate", "Până la data (UTC)", "date"],
  ["minNumber", "Număr de la", "number"],
  ["maxNumber", "Număr până la", "number"],
  ["search", "Obiect sau solicitant", "search"],
] as const;

function dateOnly(value: string) {
  const [year, month, day] = value.split("-");
  return `${day}.${month}.${year}`;
}

function isOverdue(entry: RegistryEntry) {
  return !["Completed", "Rejected", "Cancelled"].includes(entry.status) &&
    entry.deadline < new Date().toISOString().slice(0, 10);
}

const registeredDate = new Intl.DateTimeFormat("ro-RO", {
  dateStyle: "short",
  timeStyle: "short",
  timeZone: "UTC",
});

export default function RegistryPage() {
  const auth = useAuth();

  return (
    <main className="min-h-screen min-w-0 bg-slate-100 p-4 text-slate-950 sm:p-6">
      <div className="mx-auto max-w-7xl space-y-6">
        <header className="flex flex-wrap items-center justify-between gap-3">
          <h1 className="text-2xl font-semibold">
            Registratură
          </h1>

          <div className="flex flex-wrap items-center gap-3">
            <Link
                href="/registratura/noua"
                className={buttonClass}
            >
                Înregistrează poziție
            </Link>

            <Link href="/" className="text-blue-700 underline">
                Cont și deconectare
            </Link>
            </div>
        </header>

        {auth.status === "loading" && (
          <p role="status">Verificăm sesiunea…</p>
        )}

        {auth.status === "anonymous" && (
          <p>
            Autentifică-te din pagina de cont pentru a consulta
            registratura.
          </p>
        )}

        {auth.status === "error" && (
          <ApiErrorMessage error={auth.error} />
        )}

        {auth.status === "authenticated" && (
          <RegistryScreen
            key={auth.user.id}
            isAdmin={auth.user.role === "Admin"}
          />
        )}
      </div>
    </main>
  );
}

function RegistryScreen({ isAdmin }: { isAdmin: boolean }) {
  const [lookupRevision, setLookupRevision] = useState(0);

  const [request, setRequest] = useState({
    query: defaultQuery,
    revision: 0,
  });

  const registries = useApiQuery<RegistryType[]>(
    "/api/registry-types",
    lookupRevision,
  );

  const departments = useApiQuery<Department[]>(
    isAdmin
      ? "/api/departments?includeInactive=true"
      : "/api/departments",
    lookupRevision,
  );

  function load(query: string) {
    setRequest(previous => ({
      query,
      revision: previous.revision + 1,
    }));
  }

  function applyFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const query = new URLSearchParams();

    for (const [name, value] of new FormData(event.currentTarget)) {
      if (typeof value === "string" && value.trim()) {
        query.set(name, value.trim());
      }
    }

    query.set("page", "1");
    load(query.toString());
  }

  function changePage(page: number) {
    const query = new URLSearchParams(request.query);
    query.set("page", String(page));
    load(query.toString());
  }

  const lookupError = registries.error ?? departments.error;

  if (lookupError) {
    return (
      <div className="space-y-3">
        <ApiErrorMessage error={lookupError} />

        <button
          className={buttonClass}
          onClick={() => setLookupRevision(value => value + 1)}
        >
          Reîncearcă
        </button>
      </div>
    );
  }

  if (!registries.data || !departments.data) {
    return (
      <p role="status">
        Încărcăm registrele și compartimentele…
      </p>
    );
  }

  return (
    <>
      <form
        onSubmit={applyFilters}
        onReset={() => load(defaultQuery)}
        className="space-y-4 rounded-xl bg-white p-4 shadow-sm"
      >
        <fieldset className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <legend className="mb-3 font-semibold">
            Filtre
          </legend>

          <label>
            Registru

            <select
              name="registryTypeId"
              defaultValue=""
              className={inputClass}
            >
              <option value="">Toate registrele</option>

              {registries.data.map(registry => (
                <option key={registry.id} value={registry.id}>
                  {registry.code} — {registry.name}
                  {registry.isClosed ? " (închis)" : ""}
                </option>
              ))}
            </select>
          </label>

          {filterInputs.map(([name, label, type]) => (
            <label key={name}>
              {label}

              <input
                name={name}
                type={type}
                min={type === "number" ? 1 : undefined}
                step={type === "number" ? 1 : undefined}
                className={inputClass}
              />
            </label>
          ))}

          <label>
            Stare

            <select
              name="status"
              defaultValue=""
              className={inputClass}
            >
              <option value="">Toate stările</option>

              {Object.entries(statusLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </label>

          <label>
            Compartiment

            <select
              name="departmentId"
              defaultValue=""
              className={inputClass}
            >
              <option value="">Toate compartimentele</option>

              {departments.data.map(department => (
                <option key={department.id} value={department.id}>
                  {department.code} — {department.name}
                  {department.isActive ? "" : " (inactiv)"}
                </option>
              ))}
            </select>
          </label>

          <label>
            Sortare

            <select
              name="sort"
              defaultValue="registeredAt:desc"
              className={inputClass}
            >
              <option value="registeredAt:desc">
                Cele mai recente
              </option>
              <option value="registeredAt:asc">
                Cele mai vechi
              </option>
              <option value="number:asc">
                Număr crescător
              </option>
              <option value="number:desc">
                Număr descrescător
              </option>
              <option value="subject:asc">
                Obiect A–Z
              </option>
              <option value="subject:desc">
                Obiect Z–A
              </option>
            </select>
          </label>

          <label>
            Poziții pe pagină

            <select
              name="pageSize"
              defaultValue="25"
              className={inputClass}
            >
              {[10, 25, 50, 100].map(size => (
                <option key={size} value={size}>
                  {size}
                </option>
              ))}
            </select>
          </label>
        </fieldset>

        <div className="flex flex-wrap gap-3">
          <button type="submit" className={buttonClass}>
            Aplică filtrele
          </button>

          <button type="reset" className={buttonClass}>
            Resetează filtrele
          </button>
        </div>
      </form>

      <EntryResults
        key={request.revision}
        query={request.query}
        onPageChange={changePage}
        onReload={() => load(request.query)}
      />
    </>
  );
}

function EntryResults({
  query,
  onPageChange,
  onReload,
}: {
  query: string;
  onPageChange: (page: number) => void;
  onReload: () => void;
}) {
  const { data, error } = useApiQuery<PagedResponse<RegistryEntry>>(
    `/api/registry-entries?${query}`,
  );

  if (error) {
    return (
      <div className="space-y-3">
        <ApiErrorMessage error={error} />

        <button className={buttonClass} onClick={onReload}>
          Reîncearcă
        </button>
      </div>
    );
  }

  if (!data) {
    return <p role="status">Încărcăm pozițiile…</p>;
  }

  const pages = Math.max(
    1,
    Math.ceil(data.total / data.pageSize),
  );

  return (
    <section
      aria-label="Poziții de registratură"
      className="space-y-4"
    >
      <div className="flex flex-wrap items-center justify-between gap-3">
        <p role="status">
          {data.total} poziții · Pagina {data.page}
        </p>

        <button className={buttonClass} onClick={onReload}>
          Reîncarcă lista
        </button>
      </div>

      {data.items.length === 0 ? (
        <p className="rounded-xl bg-white p-4">
          Nu există poziții pe această pagină. Modifică filtrele
          sau revino la prima pagină.
        </p>
      ) : (
        <div
          role="region"
          aria-label="Tabel derulabil cu pozițiile de registratură"
          tabIndex={0}
          className="overflow-x-auto rounded-xl border border-slate-200 bg-white focus:outline-2 focus:outline-blue-700"
        >
          <table className="w-full min-w-[950px] text-left text-sm">
            <caption className="sr-only">
              Pozițiile corespunzătoare filtrelor aplicate
            </caption>

            <thead className="bg-slate-200">
              <tr>
                {[
                  "Registru / Număr",
                  "Sens",
                  "Obiect",
                  "Solicitant",
                  "Înregistrat (UTC)",
                  "Termen",
                  "Stare",
                ].map(label => (
                  <th key={label} scope="col" className="p-3">
                    {label}
                  </th>
                ))}
              </tr>
            </thead>

            <tbody>
              {data.items.map(entry => (
                <tr
                  key={entry.id}
                  className="border-t border-slate-200 align-top"
                >
                  <td
                    className="p-3"
                    title={entry.registryTypeName}
                  >
                    <p className="font-semibold">
                      {entry.registryTypeCode}
                    </p>
                    <Link
                    href={`/registratura/${entry.id}`}
                    className="text-blue-700 underline focus-visible:outline-2 focus-visible:outline-blue-700"
                    aria-label={`Deschide poziția ${entry.displayNumber} din registrul ${entry.registryTypeCode}`}
                    >
                    {entry.displayNumber}
                    </Link>
                  </td>

                  <td className="p-3">
                    {entry.direction === "In" ? "Intrare" : "Ieșire"}
                  </td>

                  <td className="max-w-sm break-words p-3">
                    {entry.subject}
                  </td>

                  <td className="max-w-xs break-words p-3">
                    {entry.applicantName}
                  </td>

                  <td className="whitespace-nowrap p-3">
                    {registeredDate.format(
                      new Date(entry.registeredAt),
                    )}
                  </td>

                  <td
                    className={
                      "whitespace-nowrap p-3 " +
                      (isOverdue(entry) ? "font-semibold text-red-700" : "")
                    }
                  >
                    {dateOnly(entry.deadline)}
                    {isOverdue(entry) && (
                      <span className="ml-2 rounded-full bg-red-100 px-2 py-0.5 text-xs font-semibold text-red-800">
                        Depășit
                      </span>
                    )}
                  </td>

                  <td className="p-3">
                    {statusLabels[entry.status] ?? entry.status}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <nav
        aria-label="Paginarea pozițiilor"
        className="flex flex-wrap gap-3"
      >
        <button
          className={buttonClass}
          disabled={data.page <= 1}
          onClick={() => onPageChange(1)}
        >
          Prima
        </button>

        <button
          className={buttonClass}
          disabled={data.page <= 1}
          onClick={() => onPageChange(data.page - 1)}
        >
          Anterioară
        </button>

        <button
          className={buttonClass}
          disabled={data.page >= pages}
          onClick={() => onPageChange(data.page + 1)}
        >
          Următoare
        </button>
      </nav>
    </section>
  );
}
