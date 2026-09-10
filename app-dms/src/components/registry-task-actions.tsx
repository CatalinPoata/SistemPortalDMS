"use client";

import {
  useState,
  type FormEvent,
} from "react";
import { useApiQuery } from "@/hooks/use-api-query";
import { apiRequest } from "@/lib/auth-store";
import { ApiError, asApiError } from "@/lib/http";
import type {
  RegistryEntryDetails,
  RegistryTaskDetails,
} from "@/lib/registry-details";
import type { Department } from "@/lib/registry-types";
import ApiErrorMessage from "@/components/api-error-message";

const inputClass =
  "mt-1 w-full rounded-lg border border-slate-300 bg-white p-2 " +
  "focus:outline-2 focus:outline-blue-700";

const buttonClass =
  "rounded-lg bg-blue-700 px-4 py-2 text-white hover:bg-blue-800 " +
  "disabled:cursor-not-allowed disabled:opacity-50 " +
  "focus-visible:outline-2 focus-visible:outline-offset-2 " +
  "focus-visible:outline-blue-700";

type Assignee = {
  id: string;
  fullName: string;
  email: string;
  role: "Clerk" | "Admin";
};

export function RegistryTaskAssignmentPanel({
  entryId,
  entryStatus,
  onEntryChanged,
}: {
  entryId: string;
  entryStatus: RegistryEntryDetails["status"];
  onEntryChanged: (message: string) => void;
}) {
  const [revision, setRevision] = useState(0);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  const assignees = useApiQuery<Assignee[]>(
    "/api/users/assignees",
    revision,
  );

  const departments = useApiQuery<Department[]>(
    "/api/departments",
    revision,
  );

  const isFinal = [
    "Completed",
    "Rejected",
    "Cancelled",
  ].includes(entryStatus);

  async function createTask(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const form = event.currentTarget;
    const formData = new FormData(form);

    const title = String(formData.get("title") ?? "").trim();
    const assigneeUserId = String(
      formData.get("assigneeUserId") ?? "",
    ).trim();
    const departmentId = String(
      formData.get("departmentId") ?? "",
    ).trim();
    const dueDate = String(
      formData.get("dueDate") ?? "",
    ).trim();
    const instructions = String(
      formData.get("instructions") ?? "",
    ).trim();

    setError(null);

    if (!title) {
      setError(new ApiError(422, {
        title: "Titlu obligatoriu",
        detail: "Completează titlul repartizării.",
      }));
      return;
    }

    if (!assigneeUserId && !departmentId) {
      setError(new ApiError(422, {
        title: "Destinatar obligatoriu",
        detail: "Alege un utilizator, un compartiment sau ambele.",
      }));
      return;
    }

    const body: Record<string, string> = {
      title,
    };

    if (assigneeUserId) {
      body.assigneeUserId = assigneeUserId;
    }

    if (departmentId) {
      body.departmentId = departmentId;
    }

    if (dueDate) {
      body.dueDate = dueDate;
    }

    if (instructions) {
      body.instructions = instructions;
    }

    setPending(true);

    try {
      await apiRequest(
        `/api/registry-entries/${encodeURIComponent(entryId)}/tasks`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify(body),
        },
      );

      form.reset();
      onEntryChanged("Repartizarea a fost creată.");
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setPending(false);
    }
  }

  if (isFinal) {
    return null;
  }

  const lookupError = assignees.error ?? departments.error;

  if (lookupError) {
    return (
      <section className="space-y-3 rounded-xl bg-white p-5 shadow-sm">
        <h2 className="text-xl font-semibold">Repartizează poziția</h2>

        <ApiErrorMessage error={lookupError} />

        <button
          type="button"
          className={buttonClass}
          onClick={() => setRevision(value => value + 1)}
        >
          Reîncearcă
        </button>
      </section>
    );
  }

  if (!assignees.data || !departments.data) {
    return (
      <section className="rounded-xl bg-white p-5 shadow-sm">
        <h2 className="text-xl font-semibold">Repartizează poziția</h2>
        <p role="status">Încărcăm utilizatorii și compartimentele…</p>
      </section>
    );
  }

  return (
    <section className="space-y-4 rounded-xl bg-white p-5 shadow-sm">
      <h2 className="text-xl font-semibold">Repartizează poziția</h2>

      {error && <ApiErrorMessage error={error} />}

      <form
        onSubmit={createTask}
        className="grid gap-4 sm:grid-cols-2"
      >
        <fieldset disabled={pending} className="contents">
          <label className="sm:col-span-2">
            Titlu
            <input
              required
              name="title"
              maxLength={200}
              className={inputClass}
            />
          </label>

          <label>
            Utilizator
            <select
              name="assigneeUserId"
              defaultValue=""
              className={inputClass}
            >
              <option value="">Fără utilizator specific</option>

              {assignees.data.map(user => (
                <option key={user.id} value={user.id}>
                  {user.fullName} — {user.email} ({user.role})
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
              <option value="">Fără compartiment specific</option>

              {departments.data.map(department => (
                <option key={department.id} value={department.id}>
                  {department.code} — {department.name}
                </option>
              ))}
            </select>
          </label>

          <label>
            Termen
            <input
              name="dueDate"
              type="date"
              className={inputClass}
            />
          </label>

          <label className="sm:col-span-2">
            Instrucțiuni
            <textarea
              name="instructions"
              rows={4}
              className={inputClass}
            />
          </label>

          <div className="sm:col-span-2">
            <button type="submit" className={buttonClass}>
              {pending
                ? "Creăm repartizarea…"
                : "Creează repartizarea"}
            </button>
          </div>
        </fieldset>
      </form>
    </section>
  );
}

export function TaskCompletionForm({
  entryId,
  task,
  onEntryChanged,
}: {
  entryId: string;
  task: RegistryTaskDetails;
  onEntryChanged: (message: string) => void;
}) {
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);

  async function completeTask(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    const note = String(
      new FormData(event.currentTarget).get("resolutionNote") ?? "",
    ).trim();

    setError(null);

    if (!note) {
      setError(new ApiError(422, {
        title: "Notă obligatorie",
        detail: "Completează nota de rezolvare.",
      }));
      return;
    }

    setPending(true);

    try {
      await apiRequest(
        "/api/registry-entries/" +
          encodeURIComponent(entryId) +
          "/tasks/" +
          encodeURIComponent(task.id) +
          "/complete",
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
          },
          body: JSON.stringify({
            resolutionNote: note,
          }),
        },
      );

      onEntryChanged("Task-ul a fost marcat ca rezolvat.");
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setPending(false);
    }
  }

  return (
    <details className="rounded-lg bg-slate-100 p-3">
      <summary className="cursor-pointer text-blue-700">
        Marchează task-ul ca rezolvat
      </summary>

      <form onSubmit={completeTask} className="mt-3 space-y-3">
        {error && <ApiErrorMessage error={error} />}

        <label>
          Notă de rezolvare
          <textarea
            required
            name="resolutionNote"
            maxLength={1000}
            rows={3}
            className={inputClass}
          />
        </label>

        <button
          type="submit"
          className={buttonClass}
          disabled={pending}
        >
          {pending ? "Salvăm…" : "Marchează ca rezolvat"}
        </button>
      </form>
    </details>
  );
}
