"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";

import ApiErrorMessage from "@/components/api-error-message";
import { useApiQuery } from "@/hooks/use-api-query";
import { useAuth } from "@/hooks/use-auth";
import { apiRequest } from "@/lib/auth-store";
import { ApiError, asApiError } from "@/lib/http";
import {
  type Department,
  type RegistryType,
} from "@/lib/registry-types";

type EntryDirection = "In" | "Out";

type CreatedRegistryEntry = {
  id: string;
  displayNumber: string;
  deadline: string;
};

const inputClass =
  "mt-1 w-full rounded-lg border border-slate-300 bg-white p-2 " +
  "focus:outline-2 focus:outline-blue-700";

const buttonClass =
  "rounded-lg bg-blue-700 px-4 py-2 text-white hover:bg-blue-800 " +
  "disabled:cursor-not-allowed disabled:opacity-50 " +
  "focus-visible:outline-2 focus-visible:outline-offset-2 " +
  "focus-visible:outline-blue-700";

function allowedDirections(
  registry: RegistryType | undefined,
): EntryDirection[] {
  if (!registry || registry.direction === "Both") {
    return ["In", "Out"];
  }

  return [registry.direction];
}

function utcToday() {
  return new Date().toISOString().slice(0, 10);
}

function addDaysUtc(days: number) {
  const date = new Date();
  date.setUTCDate(date.getUTCDate() + days);

  return date.toISOString().slice(0, 10);
}

function textValue(formData: FormData, name: string) {
  const value = formData.get(name);

  return typeof value === "string" ? value.trim() : "";
}

export default function RegistryEntryCreatePage() {
  const auth = useAuth();

  return (
    <main className="min-h-screen bg-slate-100 p-4 text-slate-950 sm:p-6">
      <div className="mx-auto max-w-4xl space-y-6">
        <header className="flex flex-wrap items-center justify-between gap-3">
          <h1 className="text-2xl font-semibold">
            Înregistrare manuală
          </h1>

          <Link
            href="/registratura"
            className="text-blue-700 underline"
          >
            Înapoi la registratură
          </Link>
        </header>

        {auth.status === "loading" && (
          <p role="status">Verificăm sesiunea…</p>
        )}

        {auth.status === "anonymous" && (
          <p>Autentifică-te pentru a înregistra o poziție.</p>
        )}

        {auth.status === "error" && (
          <ApiErrorMessage error={auth.error} />
        )}

        {auth.status === "authenticated" && (
          <CreateRegistryEntryForm />
        )}
      </div>
    </main>
  );
}

function CreateRegistryEntryForm() {
  const router = useRouter();

  const [lookupRevision, setLookupRevision] = useState(0);
  const [registryTypeId, setRegistryTypeId] = useState("");
  const [direction, setDirection] =
    useState<EntryDirection>("In");
  const [deadline, setDeadline] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  const registries = useApiQuery<RegistryType[]>(
    "/api/registry-types",
    lookupRevision,
  );

  const departments = useApiQuery<Department[]>(
    "/api/departments",
    lookupRevision,
  );

  const activeRegistries = (registries.data ?? []).filter(
    registry => !registry.isClosed,
  );

  const activeDepartments = (departments.data ?? []).filter(
    department => department.isActive,
  );

  const selectedRegistry = activeRegistries.find(
    registry => registry.id === registryTypeId,
  );

  const directions = allowedDirections(selectedRegistry);

  function changeRegistry(nextRegistryTypeId: string) {
    const registry = activeRegistries.find(
      item => item.id === nextRegistryTypeId,
    );

    const nextDirections = allowedDirections(registry);

    setRegistryTypeId(nextRegistryTypeId);
    setDirection(previous =>
      nextDirections.includes(previous)
        ? previous
        : nextDirections[0] ?? "In",
    );

    setDeadline(
      registry
        ? addDaysUtc(registry.defaultDeadlineDays)
        : "",
    );
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    const formData = new FormData(event.currentTarget);

    const subject = textValue(formData, "subject");
    const applicantName = textValue(formData, "applicantName");
    const sourceDocNumber = textValue(
      formData,
      "sourceDocNumber",
    );
    const sourceDocDate = textValue(
      formData,
      "sourceDocDate",
    );
    const departmentId = textValue(formData, "departmentId");

    if (
      !registryTypeId ||
      !subject ||
      !applicantName ||
      !sourceDocNumber ||
      !sourceDocDate ||
      !departmentId ||
      !deadline
    ) {
      setError(
        new ApiError(422, {
          detail: "Completează toate câmpurile obligatorii.",
        }),
      );
      return;
    }

    if (sourceDocDate > utcToday()) {
      setError(
        new ApiError(422, {
          detail: "Data documentului sursă nu poate fi în viitor.",
        }),
      );
      return;
    }

    setPending(true);

    try {
      const created = await apiRequest<CreatedRegistryEntry>(
        "/api/registry-entries",
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            registryTypeId,
            direction,
            subject,
            applicantName,
            applicantNationalId:
              textValue(formData, "applicantNationalId") ||
              undefined,
            applicantEmail:
              textValue(formData, "applicantEmail") || undefined,
            applicantPhone:
              textValue(formData, "applicantPhone") || undefined,
            applicantAddress:
              textValue(formData, "applicantAddress") || undefined,
            sourceDocNumber,
            sourceDocDate,
            departmentId,
            deadline,
          }),
        },
      );

      router.push(`/registratura/${created.id}`);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setPending(false);
    }
  }

  const lookupError = registries.error ?? departments.error;

  if (lookupError) {
    return (
      <section className="space-y-3">
        <ApiErrorMessage error={lookupError} />

        <button
          className={buttonClass}
          onClick={() => setLookupRevision(value => value + 1)}
        >
          Reîncearcă
        </button>
      </section>
    );
  }

  if (!registries.data || !departments.data) {
    return <p role="status">Încărcăm nomenclatoarele…</p>;
  }

  if (!activeRegistries.length) {
    return (
      <ApiErrorMessage
        error={
          new ApiError(422, {
            detail:
              "Nu există niciun registru deschis pentru înregistrare.",
          })
        }
      />
    );
  }

  return (
    <form
      onSubmit={submit}
      className="space-y-6 rounded-xl bg-white p-5 shadow-sm"
    >
      <p className="rounded-lg bg-blue-50 p-4 text-sm text-blue-950">
        Numărul, anul, data înregistrării și starea sunt stabilite
        exclusiv de server. După salvare vei fi trimis direct la
        poziția creată, unde poți încărca documentele.
      </p>

      {error && <ApiErrorMessage error={error} />}

      <fieldset className="grid gap-4 sm:grid-cols-2">
        <legend className="mb-3 font-semibold">
          Date de registratură
        </legend>

        <label>
          Registru

          <select
            value={registryTypeId}
            onChange={event => changeRegistry(event.target.value)}
            className={inputClass}
            required
          >
            <option value="">Alege registrul</option>

            {activeRegistries.map(registry => (
              <option key={registry.id} value={registry.id}>
                {registry.code} — {registry.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Direcție

          <select
            value={direction}
            onChange={event =>
              setDirection(event.target.value as EntryDirection)
            }
            className={inputClass}
            disabled={!selectedRegistry}
            required
          >
            {directions.map(item => (
              <option key={item} value={item}>
                {item === "In" ? "Intrare" : "Ieșire"}
              </option>
            ))}
          </select>
        </label>

        <label className="sm:col-span-2">
          Obiectul lucrării

          <textarea
            name="subject"
            rows={4}
            maxLength={1000}
            className={inputClass}
            required
          />
        </label>

        <label>
          {direction === "In"
            ? "Emitent / solicitant"
            : "Destinatar"}

          <input
            name="applicantName"
            maxLength={200}
            className={inputClass}
            required
          />
        </label>

        <label>
          CNP / identificator național (opțional)

          <input
            name="applicantNationalId"
            maxLength={13}
            className={inputClass}
          />
        </label>

        <label>
          E-mail (opțional)

          <input
            name="applicantEmail"
            type="email"
            maxLength={256}
            className={inputClass}
          />
        </label>

        <label>
          Telefon (opțional)

          <input
            name="applicantPhone"
            maxLength={30}
            className={inputClass}
          />
        </label>

        <label className="sm:col-span-2">
          Adresă (opțional)

          <textarea
            name="applicantAddress"
            rows={2}
            maxLength={500}
            className={inputClass}
          />
        </label>
      </fieldset>

      <fieldset className="grid gap-4 sm:grid-cols-2">
        <legend className="mb-3 font-semibold">
          Document sursă și repartizare
        </legend>

        <label>
          Număr document sursă

          <input
            name="sourceDocNumber"
            maxLength={60}
            className={inputClass}
            required
          />
        </label>

        <label>
          Data documentului sursă

          <input
            name="sourceDocDate"
            type="date"
            max={utcToday()}
            className={inputClass}
            required
          />
        </label>

        <label>
          Compartiment

          <select
            name="departmentId"
            className={inputClass}
            required
          >
            <option value="">Alege compartimentul</option>

            {activeDepartments.map(department => (
              <option key={department.id} value={department.id}>
                {department.code} — {department.name}
              </option>
            ))}
          </select>
        </label>

        <label>
          Termen

          <input
            name="deadline"
            type="date"
            min={utcToday()}
            value={deadline}
            onChange={event => setDeadline(event.target.value)}
            className={inputClass}
            required
          />

          <span className="mt-1 block text-sm text-slate-600">
            {selectedRegistry
              ? `Valoarea inițială este termenul implicit al registrului: ${selectedRegistry.defaultDeadlineDays} zile.`
              : "Alege mai întâi un registru."}
          </span>
        </label>
      </fieldset>

      <div className="flex flex-wrap gap-3">
        <button
          type="submit"
          className={buttonClass}
          disabled={pending}
        >
          {pending
            ? "Înregistrăm poziția…"
            : "Înregistrează poziția"}
        </button>

        <Link
          href="/registratura"
          className="rounded-lg border border-slate-300 px-4 py-2 hover:bg-slate-100"
        >
          Renunță
        </Link>
      </div>
    </form>
  );
}
