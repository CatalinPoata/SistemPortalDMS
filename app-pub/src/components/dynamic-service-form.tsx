"use client";

import Link from "next/link";
import { useMemo, useState, type FormEvent } from "react";

import ApiErrorPanel, {
  FieldErrors,
  fieldErrorsFor,
} from "@/components/api-error-panel";
import { useAuth } from "@/hooks/use-auth";
import { ApiError, asApiError } from "@/lib/http";
import type {
  PublicServiceDetails,
  ServiceFormField,
} from "@/lib/public-services";
import { createSubmission } from "@/lib/submissions";

const inputClass =
  "mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 " +
  "text-base text-slate-950 focus:border-blue-600 focus:outline-none " +
  "focus:ring-2 focus:ring-blue-200 disabled:bg-slate-100";

function buildInitialValues(service: PublicServiceDetails) {
  const result: Record<string, unknown> = {};

  for (const section of service.formSchema.sections) {
    for (const field of section.fields) {
      if (field.defaultValue !== undefined && field.defaultValue !== null) {
        result[field.key] = field.defaultValue;
      } else if (field.type === "boolean") {
        result[field.key] = false;
      } else if (field.type === "checkboxes") {
        result[field.key] = [];
      }
    }
  }

  return result;
}

function isVisible(
  field: ServiceFormField,
  values: Record<string, unknown>,
) {
  if (!field.visibleWhen) return true;

  return Object.is(
    values[field.visibleWhen.field],
    field.visibleWhen.equals,
  );
}

function hasValue(field: ServiceFormField) {
  return !["heading", "paragraph", "file"].includes(field.type);
}

function valuesForSubmission(
  service: PublicServiceDetails,
  values: Record<string, unknown>,
) {
  const result: Record<string, unknown> = {};

  for (const section of service.formSchema.sections) {
    for (const field of section.fields) {
      if (!hasValue(field) || !isVisible(field, values)) continue;

      const value = values[field.key];

      if (value === undefined || value === null || value === "") continue;

      result[field.key] = value;
    }
  }

  return result;
}

function FieldHelp({ field }: { field: ServiceFormField }) {
  if (!field.helpText) return null;

  return (
    <p id={`${field.key}-help`} className="mt-1 text-sm text-slate-600">
      {field.helpText}
    </p>
  );
}

type FieldProps = {
  field: ServiceFormField;
  values: Record<string, unknown>;
  update: (key: string, value: unknown) => void;
  file: File | null;
  updateFile: (key: string, file: File | null) => void;
  disabled: boolean;
};

function FormField({
  field,
  values,
  update,
  file,
  updateFile,
  disabled,
}: FieldProps) {
  if (field.type === "heading") {
    return <h3 className="text-xl font-semibold text-slate-900">{field.label}</h3>;
  }

  if (field.type === "paragraph") {
    return <p className="leading-7 text-slate-700">{field.helpText ?? field.label}</p>;
  }

  const describedBy = field.helpText ? `${field.key}-help` : undefined;
  const rawValue = values[field.key];
  const stringValue = typeof rawValue === "string" ? rawValue : "";
  const inputValue =
    rawValue === undefined || rawValue === null
      ? ""
      : String(rawValue);

  if (field.type === "textarea") {
    return (
      <div>
        <label htmlFor={field.key} className="text-sm font-medium">
          {field.label} {field.required && <span aria-hidden="true">*</span>}
        </label>
        <textarea
          id={field.key}
          name={field.key}
          required={field.required}
          minLength={field.minLength}
          maxLength={field.maxLength}
          placeholder={field.placeholder}
          aria-describedby={describedBy}
          disabled={disabled}
          value={stringValue}
          onChange={event => update(field.key, event.target.value)}
          className={`${inputClass} min-h-28`}
        />
        <FieldHelp field={field} />
      </div>
    );
  }

  if (["text", "email", "phone", "nationalId", "date", "number"].includes(field.type)) {
    const inputType =
      field.type === "phone"
        ? "tel"
        : field.type === "nationalId"
          ? "text"
          : field.type;
    const today = new Date().toISOString().slice(0, 10);

    return (
      <div>
        <label htmlFor={field.key} className="text-sm font-medium">
          {field.label} {field.required && <span aria-hidden="true">*</span>}
        </label>
        <input
          id={field.key}
          name={field.key}
          type={inputType}
          required={field.required}
          minLength={field.minLength}
          maxLength={field.maxLength}
          pattern={field.pattern}
          min={field.type === "date" ? field.minDate : field.min}
          max={
            field.type === "date"
              ? field.notInFuture
                ? [field.maxDate, today].filter(Boolean).sort()[0]
                : field.maxDate
              : field.max
          }
          step={field.type === "number" && field.integer ? 1 : undefined}
          placeholder={field.placeholder}
          aria-describedby={describedBy}
          disabled={disabled}
          value={inputValue}
          onChange={event => {
            const next = event.target.value;
            update(
              field.key,
              field.type === "number" && next !== "" ? Number(next) : next,
            );
          }}
          className={inputClass}
        />
        <FieldHelp field={field} />
      </div>
    );
  }

  if (field.type === "boolean") {
    return (
      <div>
        <label className="flex items-start gap-3 rounded-lg border border-slate-200 p-3">
          <input
            name={field.key}
            type="checkbox"
            required={field.required}
            disabled={disabled}
            checked={values[field.key] === true}
            onChange={event => update(field.key, event.target.checked)}
            className="mt-1 size-4 accent-blue-700"
          />
          <span className="text-sm font-medium">
            {field.label} {field.required && <span aria-hidden="true">*</span>}
          </span>
        </label>
        <FieldHelp field={field} />
      </div>
    );
  }

  if (field.type === "select") {
    return (
      <div>
        <label htmlFor={field.key} className="text-sm font-medium">
          {field.label} {field.required && <span aria-hidden="true">*</span>}
        </label>
        <select
          id={field.key}
          name={field.key}
          required={field.required}
          disabled={disabled}
          aria-describedby={describedBy}
          value={stringValue}
          onChange={event => update(field.key, event.target.value)}
          className={inputClass}
        >
          <option value="">Selectează…</option>
          {(field.options ?? []).map(option => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </select>
        <FieldHelp field={field} />
      </div>
    );
  }

  if (field.type === "radio") {
    return (
      <fieldset className="space-y-2">
        <legend className="text-sm font-medium">
          {field.label} {field.required && <span aria-hidden="true">*</span>}
        </legend>
        {(field.options ?? []).map(option => (
          <label key={option.value} className="flex items-center gap-2">
            <input
              name={field.key}
              type="radio"
              required={field.required}
              disabled={disabled}
              value={option.value}
              checked={stringValue === option.value}
              onChange={() => update(field.key, option.value)}
              className="size-4 accent-blue-700"
            />
            <span>{option.label}</span>
          </label>
        ))}
        <FieldHelp field={field} />
      </fieldset>
    );
  }

  if (field.type === "checkboxes") {
    const selected = Array.isArray(values[field.key])
      ? (values[field.key] as string[])
      : [];

    return (
      <fieldset className="space-y-2">
        <legend className="text-sm font-medium">
          {field.label} {field.required && <span aria-hidden="true">*</span>}
        </legend>
        {(field.options ?? []).map(option => (
          <label key={option.value} className="flex items-center gap-2">
            <input
              name={field.key}
              type="checkbox"
              disabled={disabled}
              checked={selected.includes(option.value)}
              onChange={event => {
                update(
                  field.key,
                  event.target.checked
                    ? [...selected, option.value]
                    : selected.filter(value => value !== option.value),
                );
              }}
              className="size-4 accent-blue-700"
            />
            <span>{option.label}</span>
          </label>
        ))}
        <FieldHelp field={field} />
      </fieldset>
    );
  }

  if (field.type === "file") {
    const accept = Array.isArray(field.accept)
      ? field.accept.join(",")
      : field.accept;

    return (
      <div>
        <label htmlFor={field.key} className="text-sm font-medium">
          {field.label} {field.required && <span aria-hidden="true">*</span>}
        </label>
        <input
          id={field.key}
          name={field.key}
          type="file"
          accept={accept}
          required={field.required}
          disabled={disabled}
          onChange={event =>
            updateFile(field.key, event.target.files?.[0] ?? null)
          }
          className={inputClass}
        />
        <p className="mt-1 text-sm text-slate-600">
          Maximum {Math.min(field.maxSizeMb ?? 10, 10)} MB.
          {file && ` Selectat: ${file.name}`}
        </p>
        <FieldHelp field={field} />
      </div>
    );
  }

  return null;
}

export default function DynamicServiceForm({
  service,
}: {
  service: PublicServiceDetails;
}) {
  const auth = useAuth();
  const initialValues = useMemo(() => buildInitialValues(service), [service]);
  const [values, setValues] = useState<Record<string, unknown>>(initialValues);
  const [fieldFiles, setFieldFiles] = useState<Record<string, File | null>>({});
  const [attachments, setAttachments] = useState<File[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [createdId, setCreatedId] = useState<string | null>(null);

  function update(key: string, value: unknown) {
    setError(null);
    setValues(current => ({ ...current, [key]: value }));
  }

  function updateFile(key: string, file: File | null) {
    setError(null);
    setFieldFiles(current => ({ ...current, [key]: file }));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (busy || auth.status !== "authenticated") return;

    setBusy(true);
    setError(null);

    try {
      const uploads: Array<{ file: File; fieldKey: string | null }> = [];
      const maximumFileSize = 10 * 1024 * 1024;

      for (const section of service.formSchema.sections) {
        for (const field of section.fields) {
          if (field.type !== "file" || !isVisible(field, values)) continue;

          const file = fieldFiles[field.key];

          if (!file) {
            if (field.required) {
              throw new ApiError(422, {
                detail: "Selectează toate fișierele obligatorii.",
                errors: { [field.key]: ["Fișierul este obligatoriu."] },
              });
            }

            continue;
          }

          const fieldMaximum =
            Math.min(field.maxSizeMb ?? 10, 10) * 1024 * 1024;

          if (file.size > fieldMaximum) {
            throw new ApiError(422, {
              detail: "Un fișier depășește limita permisă.",
              errors: {
                [field.key]: [
                  `Fișierul poate avea cel mult ${fieldMaximum / 1024 / 1024} MB.`,
                ],
              },
            });
          }

          uploads.push({ file, fieldKey: field.key });
        }
      }

      if (service.requiresAttachment && attachments.length === 0) {
        throw new ApiError(422, {
          detail: "Adaugă cel puțin un atașament general.",
          errors: { attachments: ["Atașamentul este obligatoriu."] },
        });
      }

      if (attachments.length > service.maxAttachments) {
        throw new ApiError(422, {
          detail: "Ai selectat prea multe atașamente.",
          errors: {
            attachments: [
              `Sunt permise cel mult ${service.maxAttachments} atașamente.`,
            ],
          },
        });
      }

      if (attachments.some(file => file.size > maximumFileSize)) {
        throw new ApiError(413, {
          detail: "Dimensiunea maximă este de 10 MB per fișier.",
        });
      }

      uploads.push(
        ...attachments.map(file => ({ file, fieldKey: null })),
      );

      const submission = await createSubmission(
        service.code,
        valuesForSubmission(service, values),
        uploads,
      );

      setCreatedId(submission.id);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(false);
    }
  }

  if (createdId) {
    return (
      <div
        role="status"
        className="space-y-4 rounded-2xl border border-green-200 bg-green-50 p-6 text-green-950"
      >
        <h2 className="text-xl font-semibold">Cererea a fost depusă</h2>
        <p>
          Identificator: <span className="font-mono text-sm">{createdId}</span>
        </p>
        <Link
          href={`/cereri/${createdId}`}
          className="inline-flex rounded-lg bg-green-800 px-4 py-2 font-medium text-white hover:bg-green-900"
        >
          Vezi cererea
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-5">
      {error && <ApiErrorPanel error={error} showFieldErrors={false} />}

      {auth.status === "anonymous" && (
        <div className="rounded-xl border border-blue-200 bg-blue-50 p-4 text-blue-950">
          <Link
            href={`/login?next=${encodeURIComponent(
              `/servicii/${service.code}`,
            )}`}
            className="font-medium underline"
          >
            Autentifică-te
          </Link>{" "}
          pentru a depune cererea.
        </div>
      )}

      {auth.status === "authenticated" && auth.user.role !== "Citizen" && (
        <div className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-amber-950">
          Numai un cont de cetățean poate depune cereri.
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-6" aria-busy={busy}>
        {service.formSchema.sections.map(section => (
          <fieldset
            key={section.key}
            disabled={busy}
            className="space-y-5 rounded-2xl border border-slate-200 bg-white p-5 sm:p-6"
          >
            <legend className="px-2 text-lg font-semibold text-slate-950">
              {section.title}
            </legend>

            {section.fields.map(field =>
              isVisible(field, values) ? (
                <div key={field.key}>
                  <FormField
                    field={field}
                    values={values}
                    update={update}
                    file={fieldFiles[field.key] ?? null}
                    updateFile={updateFile}
                    disabled={busy}
                  />
                  <FieldErrors messages={fieldErrorsFor(error, field.key)} />
                </div>
              ) : null,
            )}
          </fieldset>
        ))}

        {service.maxAttachments > 0 && (
          <fieldset
            disabled={busy}
            className="space-y-3 rounded-2xl border border-slate-200 bg-white p-5 sm:p-6"
          >
            <legend className="px-2 text-lg font-semibold text-slate-950">
              Atașamente generale
            </legend>
            <label htmlFor="general-attachments" className="block text-sm font-medium">
              Fișiere {service.requiresAttachment && <span aria-hidden="true">*</span>}
            </label>
            <input
              id="general-attachments"
              type="file"
              multiple
              required={service.requiresAttachment}
              disabled={busy}
              accept=".pdf,.jpg,.jpeg,.png,.docx,.odt"
              onChange={event =>
                setAttachments(Array.from(event.target.files ?? []))
              }
              className={inputClass}
            />
            <p className="text-sm text-slate-600">
              Maximum {service.maxAttachments} fișiere, câte 10 MB fiecare.
            </p>
            <FieldErrors messages={fieldErrorsFor(error, "attachments")} />
          </fieldset>
        )}

        <button
          type="submit"
          disabled={
            busy ||
            auth.status !== "authenticated" ||
            auth.user.role !== "Citizen"
          }
          className="w-full rounded-lg bg-blue-700 px-5 py-3 font-semibold text-white hover:bg-blue-800 disabled:cursor-not-allowed disabled:opacity-50 sm:w-auto"
        >
          {busy ? "Se depune…" : "Depune cererea"}
        </button>
      </form>
    </div>
  );
}
