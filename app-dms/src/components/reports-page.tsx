"use client";

import Link from "next/link";
import {
  useMemo,
  useState,
  type DragEvent,
  type FormEvent,
} from "react";

import ApiErrorMessage from "@/components/api-error-message";
import { useApiQuery } from "@/hooks/use-api-query";
import { useAuth } from "@/hooks/use-auth";
import {
  apiDownload,
  apiRequest,
} from "@/lib/auth-store";
import { ApiError, asApiError } from "@/lib/http";
import {
  createDefaultDefinition,
  createQueryString,
  formatPreviewValue,
  type ReportColumn,
  type ReportDataset,
  type ReportDatasetField,
  type ReportDatasetParameter,
  type ReportDefinition,
  type ReportDetails,
  type ReportPreview,
  type ReportSummary,
  type ReportTotal,
} from "@/lib/reports";
import type {
  Department,
  RegistryType,
} from "@/lib/registry-types";

const inputClass =
  "mt-1 w-full rounded-lg border border-slate-300 bg-white p-2 text-base " +
  "focus:outline-2 focus:outline-blue-700 disabled:bg-slate-100";

const buttonClass =
  "rounded-lg bg-blue-700 px-4 py-2 text-white hover:bg-blue-800 " +
  "disabled:cursor-not-allowed disabled:opacity-50 " +
  "focus-visible:outline-2 focus-visible:outline-offset-2 " +
  "focus-visible:outline-blue-700";

const secondaryButtonClass =
  "rounded-lg border border-slate-300 px-4 py-2 text-slate-900 " +
  "hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50 " +
  "focus-visible:outline-2 focus-visible:outline-offset-2 " +
  "focus-visible:outline-blue-700";

type Draft = {
  code: string;
  name: string;
  datasetKey: string;
  definition: ReportDefinition;
  version: number | null;
  isSystem: boolean;
};

function makeNewDraft(dataset: ReportDataset): Draft {
  return {
    code: "",
    name: "",
    datasetKey: dataset.key,
    definition: createDefaultDefinition(dataset),
    version: null,
    isSystem: false,
  };
}

function makeExistingDraft(report: ReportDetails): Draft {
  return {
    code: report.code,
    name: report.name,
    datasetKey: report.datasetKey,
    definition: report.definition,
    version: report.version,
    isSystem: report.isSystem,
  };
}

function defaultAlign(
  field: ReportDatasetField,
): NonNullable<ReportColumn["align"]> {
  return field.type === "number" ||
    field.type === "date" ||
    field.type === "code"
    ? "center"
    : "left";
}

function normalizeWidths(columns: ReportColumn[]) {
  if (!columns.length) return columns;

  const base = Math.floor(100 / columns.length);

  return columns.map((column, index) => ({
    ...column,
    widthPct:
      index === columns.length - 1
        ? 100 - base * (columns.length - 1)
        : base,
  }));
}

function updateDefinition(
  draft: Draft,
  update: (definition: ReportDefinition) => ReportDefinition,
) {
  return {
    ...draft,
    definition: update(draft.definition),
  };
}

function ErrorBlock({ error }: { error: ApiError | null }) {
  return error ? <ApiErrorMessage error={error} /> : null;
}

export default function ReportsPage() {
  const auth = useAuth();

  return (
    <main className="min-h-screen bg-slate-100 p-4 text-slate-950 sm:p-6">
      <div className="mx-auto max-w-7xl space-y-6">
        <header className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="text-sm font-semibold uppercase tracking-wider text-blue-700">
              DMS
            </p>
            <h1 className="text-2xl font-semibold">Rapoarte</h1>
          </div>

          <Link href="/registratura" className="text-blue-700 underline">
            Înapoi la registratură
          </Link>
        </header>

        {auth.status === "loading" && (
          <p role="status">Verificăm sesiunea…</p>
        )}

        {auth.status === "anonymous" && (
          <p>Autentifică-te pentru a consulta rapoartele.</p>
        )}

        {auth.status === "error" && <ApiErrorMessage error={auth.error} />}

        {auth.status === "authenticated" && (
          <ReportsWorkspace isAdmin={auth.user.role === "Admin"} />
        )}
      </div>
    </main>
  );
}

function ReportsWorkspace({ isAdmin }: { isAdmin: boolean }) {
  const [revision, setRevision] = useState(0);
  const reports = useApiQuery<ReportSummary[]>(
    "/api/report-definitions",
    revision,
  );
  const datasets = useApiQuery<ReportDataset[]>(
    "/api/report-definitions/datasets",
  );
  const [selectedCode, setSelectedCode] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  function refresh(code?: string | null) {
    setRevision(value => value + 1);
    setCreating(false);
    setSelectedCode(code ?? selectedCode);
  }

  if (reports.error || datasets.error) {
    return (
      <section className="space-y-3">
        <ApiErrorMessage error={reports.error ?? datasets.error!} />
        <button className={buttonClass} onClick={() => refresh()}>
          Reîncearcă
        </button>
      </section>
    );
  }

  if (!reports.data || !datasets.data) {
    return <p role="status">Încărcăm rapoartele…</p>;
  }

  const showNew = creating && datasets.data.length > 0;

  return (
    <div className="grid gap-6 lg:grid-cols-[18rem_minmax(0,1fr)]">
      <aside className="space-y-4 rounded-xl bg-white p-4 shadow-sm">
        <div className="flex items-center justify-between gap-3">
          <h2 className="font-semibold">Definiții salvate</h2>

          {isAdmin && (
            <button
              type="button"
              className={secondaryButtonClass}
              onClick={() => {
                setSelectedCode(null);
                setCreating(true);
              }}
            >
              Raport nou
            </button>
          )}
        </div>

        {reports.data.length === 0 ? (
          <p className="text-sm text-slate-600">
            Nu există definiții de raport.
          </p>
        ) : (
          <ul className="space-y-2">
            {reports.data.map(report => (
              <li key={report.id}>
                <button
                  type="button"
                  onClick={() => {
                    setCreating(false);
                    setSelectedCode(report.code);
                  }}
                  className={
                    "w-full rounded-lg p-3 text-left hover:bg-slate-100 " +
                    (selectedCode === report.code
                      ? "bg-blue-50 outline outline-2 outline-blue-700"
                      : "bg-slate-50")
                  }
                >
                  <span className="block font-medium">{report.name}</span>
                  <span className="block break-all text-sm text-slate-600">
                    {report.code} · v{report.version}
                    {report.isSystem ? " · sistem" : ""}
                  </span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </aside>

      <section className="min-w-0">
        {showNew && (
          <ReportEditor
            key="new-report"
            datasets={datasets.data}
            isAdmin={isAdmin}
            initialDraft={makeNewDraft(datasets.data[0])}
            onSaved={report => refresh(report.code)}
            onDeleted={() => refresh(null)}
          />
        )}

        {!showNew && selectedCode && (
          <ExistingReportEditor
            key={selectedCode}
            code={selectedCode}
            datasets={datasets.data}
            isAdmin={isAdmin}
            onSaved={report => refresh(report.code)}
            onDeleted={() => refresh(null)}
          />
        )}

        {!showNew && !selectedCode && (
          <div className="rounded-xl bg-white p-6 text-slate-600 shadow-sm">
            Alege un raport din listă.
            {isAdmin && " Sau creează o definiție nouă."}
          </div>
        )}
      </section>
    </div>
  );
}

function ExistingReportEditor({
  code,
  datasets,
  isAdmin,
  onSaved,
  onDeleted,
}: {
  code: string;
  datasets: ReportDataset[];
  isAdmin: boolean;
  onSaved: (report: ReportDetails) => void;
  onDeleted: () => void;
}) {
  const report = useApiQuery<ReportDetails>(
    `/api/report-definitions/${encodeURIComponent(code)}`,
  );

  if (report.error) {
    return <ApiErrorMessage error={report.error} />;
  }

  if (!report.data) {
    return <p role="status">Încărcăm definiția…</p>;
  }

  return (
    <ReportEditor
      datasets={datasets}
      isAdmin={isAdmin}
      initialDraft={makeExistingDraft(report.data)}
      onSaved={onSaved}
      onDeleted={onDeleted}
    />
  );
}

function ReportEditor({
  datasets,
  isAdmin,
  initialDraft,
  onSaved,
  onDeleted,
}: {
  datasets: ReportDataset[];
  isAdmin: boolean;
  initialDraft: Draft;
  onSaved: (report: ReportDetails) => void;
  onDeleted: () => void;
}) {
  const [draft, setDraft] = useState(initialDraft);
  const [parameterValues, setParameterValues] = useState<
    Record<string, string>
  >({});
  const [preview, setPreview] = useState<ReportPreview | null>(null);
  const [busy, setBusy] = useState<"save" | "preview" | "export" | "delete" | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [draggedField, setDraggedField] = useState<string | null>(null);
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);

  const registryTypes = useApiQuery<RegistryType[]>(
    "/api/registry-types",
  );
  const departments = useApiQuery<Department[]>(
    "/api/departments",
  );

  const dataset = useMemo(
    () => datasets.find(item => item.key === draft.datasetKey) ?? null,
    [datasets, draft.datasetKey],
  );

  const widthTotal = draft.definition.columns.reduce(
    (total, column) => total + column.widthPct,
    0,
  );

  const isNew = draft.version === null;
  const canExport = !isNew && !hasUnsavedChanges;

  function change(next: Draft) {
    setDraft(next);
    setHasUnsavedChanges(true);
    setPreview(null);
  }

  function changeDefinition(
    update: (definition: ReportDefinition) => ReportDefinition,
  ) {
    change(updateDefinition(draft, update));
  }

  function selectDataset(nextDatasetKey: string) {
    const nextDataset = datasets.find(
      item => item.key === nextDatasetKey,
    );

    if (!nextDataset) return;

    change({
      ...draft,
      datasetKey: nextDataset.key,
      definition: createDefaultDefinition(nextDataset),
    });
    setParameterValues({});
  }

  function toggleField(field: ReportDatasetField) {
    changeDefinition(definition => {
      const exists = definition.columns.some(
        column => column.field === field.key,
      );

      const columns: ReportColumn[] = exists
        ? definition.columns.filter(column => column.field !== field.key)
        : [
            ...definition.columns,
            {
              field: field.key,
              label: field.label,
              type: field.type,
              align: defaultAlign(field),
              widthPct: 1,
            },
          ];

      return {
        ...definition,
        columns:
          definition.renderMode === "table"
            ? normalizeWidths(columns)
            : columns,
      };
    });
  }

  function updateColumn(
    field: string,
    update: Partial<ReportColumn>,
  ) {
    changeDefinition(definition => ({
      ...definition,
      columns: definition.columns.map(column =>
        column.field === field ? { ...column, ...update } : column,
      ),
    }));
  }

  function moveColumn(dragged: string, target: string) {
    if (dragged === target) return;

    changeDefinition(definition => {
      const columns = [...definition.columns];
      const from = columns.findIndex(item => item.field === dragged);
      const to = columns.findIndex(item => item.field === target);

      if (from < 0 || to < 0) return definition;

      const [moved] = columns.splice(from, 1);
      columns.splice(to, 0, moved);

      return { ...definition, columns };
    });
  }

  function toggleParameter(parameter: ReportDatasetParameter) {
    changeDefinition(definition => {
      const exists = definition.parameters.some(
        item => item.name === parameter.name,
      );

      return {
        ...definition,
        parameters: exists
          ? definition.parameters.filter(
              item => item.name !== parameter.name,
            )
          : [
              ...definition.parameters,
              { ...parameter, required: false },
            ],
      };
    });
  }

  function updateParameter(
    name: string,
    update: Partial<ReportDatasetParameter & { required: boolean }>,
  ) {
    changeDefinition(definition => ({
      ...definition,
      parameters: definition.parameters.map(parameter =>
        parameter.name === name
          ? { ...parameter, ...update }
          : parameter,
      ),
    }));
  }

  function addSort() {
    const field = dataset?.fields.find(
      item => !draft.definition.sort.some(sort => sort.field === item.key),
    );

    if (!field) return;

    changeDefinition(definition => ({
      ...definition,
      sort: [...definition.sort, { field: field.key, dir: "asc" }],
    }));
  }

  function addTotal() {
    const field = dataset?.fields[0];
    if (!field) return;

    changeDefinition(definition => ({
      ...definition,
      totals: [
        ...definition.totals,
        { field: field.key, agg: "count" },
      ],
    }));
  }

  function changeRenderMode(mode: "table" | "record") {
    changeDefinition(definition => ({
      ...definition,
      renderMode: mode,
      columns:
        mode === "table"
          ? normalizeWidths(definition.columns)
          : definition.columns,
    }));
  }

  async function previewReport() {
    setBusy("preview");
    setError(null);

    try {
      const query = createQueryString(parameterValues);
      const result = isAdmin
        ? await apiRequest<ReportPreview>(
            query
              ? `/api/report-definitions/preview?${query}`
              : "/api/report-definitions/preview",
            {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify({
                datasetKey: draft.datasetKey,
                definition: draft.definition,
              }),
            },
          )
        : await apiRequest<ReportPreview>(
            query
              ? `/api/report-definitions/${encodeURIComponent(draft.code)}/preview?${query}`
              : `/api/report-definitions/${encodeURIComponent(draft.code)}/preview`,
          );

      setPreview(result);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(null);
    }
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy("save");
    setError(null);

    try {
      const body = isNew
        ? {
            code: draft.code.trim().toLowerCase(),
            name: draft.name.trim(),
            datasetKey: draft.datasetKey,
            definition: draft.definition,
          }
        : {
            name: draft.name.trim(),
            datasetKey: draft.datasetKey,
            definition: draft.definition,
            expectedVersion: draft.version,
          };

      const path = isNew
        ? "/api/report-definitions"
        : `/api/report-definitions/${encodeURIComponent(draft.code)}`;

      const saved = await apiRequest<ReportDetails>(path, {
        method: isNew ? "POST" : "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });

      setHasUnsavedChanges(false);
      onSaved(saved);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(null);
    }
  }

  async function downloadPdf() {
    if (!canExport) return;

    setBusy("export");
    setError(null);

    try {
      const query = createQueryString(parameterValues);
      const path = query
        ? `/api/report-definitions/${encodeURIComponent(draft.code)}/export?${query}`
        : `/api/report-definitions/${encodeURIComponent(draft.code)}/export`;

      const download = await apiDownload(path, { method: "POST" });
      const url = URL.createObjectURL(download.blob);
      const link = document.createElement("a");

      link.href = url;
      link.download = download.fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(null);
    }
  }

  async function downloadCsv() {
    if (!canExport) return;

    setBusy("export");
    setError(null);

    try {
      const query = createQueryString(parameterValues);
      const path = query
        ? `/api/report-definitions/${encodeURIComponent(draft.code)}/export.csv?${query}`
        : `/api/report-definitions/${encodeURIComponent(draft.code)}/export.csv`;
      const download = await apiDownload(path, { method: "GET" }, {
        accept: "text/csv, application/problem+json",
        fallbackName: `${draft.code}.csv`,
      });
      const url = URL.createObjectURL(download.blob);
      const link = document.createElement("a");

      link.href = url;
      link.download = download.fileName;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(null);
    }
  }

  async function deleteReport() {
    if (
      !draft.code ||
      !window.confirm("Ștergi definitiv această definiție de raport?")
    ) {
      return;
    }

    setBusy("delete");
    setError(null);

    try {
      await apiRequest<void>(
        `/api/report-definitions/${encodeURIComponent(draft.code)}`,
        { method: "DELETE" },
      );
      onDeleted();
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(null);
    }
  }

  if (!dataset) {
    return (
      <ApiErrorMessage
        error={new ApiError(422, {
          detail: "Setul de date al raportului nu este disponibil.",
        })}
      />
    );
  }

  const lookupError = registryTypes.error ?? departments.error;

  return (
    <form
      onSubmit={save}
      className="space-y-6 rounded-xl bg-white p-5 shadow-sm"
    >
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-xl font-semibold">
            {isNew ? "Definiție nouă" : draft.name}
          </h2>
          {!isNew && (
            <p className="text-sm text-slate-600">
              Cod: {draft.code} · versiunea {draft.version}
              {draft.isSystem ? " · raport de sistem" : ""}
            </p>
          )}
        </div>

        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            className={secondaryButtonClass}
            disabled={busy !== null}
            onClick={() => void previewReport()}
          >
            {busy === "preview" ? "Generăm…" : "Previzualizează"}
          </button>

          <button
            type="button"
            className={secondaryButtonClass}
            disabled={!canExport || busy !== null}
            onClick={() => void downloadPdf()}
            title={
              canExport
                ? "Exportă definiția salvată"
                : "Salvează modificările înainte de export."
            }
          >
            {busy === "export" ? "Descărcăm…" : "Export PDF"}
          </button>

          <button
            type="button"
            className={secondaryButtonClass}
            disabled={!canExport || busy !== null}
            onClick={() => void downloadCsv()}
            title={
              canExport
                ? "Exportă definiția salvată"
                : "Salvează modificările înainte de export."
            }
          >
            {busy === "export" ? "Descărcăm…" : "Export CSV"}
          </button>
        </div>
      </header>

      {isNew && (
        <p className="rounded-lg bg-blue-50 p-3 text-sm text-blue-950">
          Previzualizarea rulează definiția nesalvată, dar exportul PDF
          devine disponibil numai după salvare.
        </p>
      )}

      {hasUnsavedChanges && !isNew && (
        <p className="rounded-lg bg-amber-50 p-3 text-sm text-amber-950">
          Există modificări nesalvate. Exportul folosește numai versiunea
          salvată.
        </p>
      )}

      <ErrorBlock error={error} />

      {lookupError && <ApiErrorMessage error={lookupError} />}

      <fieldset className="grid gap-4 sm:grid-cols-2">
        <legend className="mb-2 font-semibold">Identitate</legend>

        <label>
          Cod
          <input
            value={draft.code}
            onChange={event =>
              change({ ...draft, code: event.target.value })
            }
            disabled={!isAdmin || !isNew || busy !== null}
            required
            maxLength={50}
            pattern="[a-z0-9]+(?:-[a-z0-9]+)*"
            className={inputClass}
          />
        </label>

        <label>
          Denumire
          <input
            value={draft.name}
            onChange={event =>
              change({ ...draft, name: event.target.value })
            }
            disabled={!isAdmin || busy !== null}
            required
            maxLength={200}
            className={inputClass}
          />
        </label>

        <label>
          Set de date
          <select
            value={draft.datasetKey}
            disabled={!isAdmin || busy !== null}
            onChange={event => selectDataset(event.target.value)}
            className={inputClass}
          >
            {datasets.map(item => (
              <option key={item.key} value={item.key}>
                {item.label}
              </option>
            ))}
          </select>
        </label>

        <label>
          Mod de randare
          <select
            value={draft.definition.renderMode}
            disabled={!isAdmin || busy !== null}
            onChange={event =>
              changeRenderMode(
                event.target.value as "table" | "record",
              )
            }
            className={inputClass}
          >
            <option value="table">Tabel</option>
            <option value="record">Fișă / înregistrare</option>
          </select>
        </label>
      </fieldset>

      <section className="space-y-3">
        <h3 className="font-semibold">Coloane disponibile</h3>
        <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
          {dataset.fields.map(field => {
            const checked = draft.definition.columns.some(
              column => column.field === field.key,
            );

            return (
              <label
                key={field.key}
                className="flex items-center gap-2 rounded-lg border border-slate-200 p-3"
              >
                <input
                  type="checkbox"
                  checked={checked}
                  disabled={!isAdmin || busy !== null}
                  onChange={() => toggleField(field)}
                />
                <span>
                  {field.label}
                  <span className="ml-1 text-sm text-slate-600">
                    ({field.type})
                  </span>
                </span>
              </label>
            );
          })}
        </div>
      </section>

      <section className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h3 className="font-semibold">Coloane selectate</h3>
          {draft.definition.renderMode === "table" && (
            <p
              className={
                widthTotal === 100
                  ? "text-sm text-emerald-700"
                  : "text-sm text-red-700"
              }
            >
              Lățime totală: {widthTotal}% / 100%
            </p>
          )}
        </div>

        {draft.definition.columns.length === 0 ? (
          <p className="rounded-lg bg-amber-50 p-3 text-sm text-amber-950">
            Selectează cel puțin o coloană.
          </p>
        ) : (
          <div className="space-y-3">
            {draft.definition.columns.map(column => (
              <div
                key={column.field}
                draggable={isAdmin && busy === null}
                onDragStart={() => setDraggedField(column.field)}
                onDragOver={event => event.preventDefault()}
                onDrop={(event: DragEvent<HTMLDivElement>) => {
                  event.preventDefault();
                  if (draggedField) {
                    moveColumn(draggedField, column.field);
                  }
                  setDraggedField(null);
                }}
                className="grid gap-3 rounded-lg border border-slate-200 p-3 md:grid-cols-[minmax(9rem,1fr)_minmax(10rem,2fr)_8rem_7rem_9rem]"
              >
                <p className="self-center font-medium" title="Trage pentru reordonare">
                  ↕ {column.field}
                </p>

                <label>
                  Etichetă
                  <input
                    value={column.label}
                    disabled={!isAdmin || busy !== null}
                    maxLength={200}
                    onChange={event =>
                      updateColumn(column.field, {
                        label: event.target.value,
                      })
                    }
                    className={inputClass}
                  />
                </label>

                <label>
                  Aliniere
                  <select
                    value={column.align ?? "left"}
                    disabled={!isAdmin || busy !== null}
                    onChange={event =>
                      updateColumn(column.field, {
                        align: event.target.value as
                          | "left"
                          | "center"
                          | "right",
                      })
                    }
                    className={inputClass}
                  >
                    <option value="left">Stânga</option>
                    <option value="center">Centru</option>
                    <option value="right">Dreapta</option>
                  </select>
                </label>

                {draft.definition.renderMode === "table" ? (
                  <label>
                    Lățime %
                    <input
                      type="number"
                      min="1"
                      max="100"
                      value={column.widthPct}
                      disabled={!isAdmin || busy !== null}
                      onChange={event =>
                        updateColumn(column.field, {
                          widthPct: Number(event.target.value),
                        })
                      }
                      className={inputClass}
                    />
                  </label>
                ) : (
                  <div />
                )}

                <label>
                  Format
                  <input
                    value={column.format ?? ""}
                    disabled={!isAdmin || busy !== null}
                    placeholder="dd.MM.yyyy"
                    onChange={event =>
                      updateColumn(column.field, {
                        format: event.target.value || undefined,
                      })
                    }
                    className={inputClass}
                  />
                </label>
              </div>
            ))}
          </div>
        )}
      </section>

      <ReportParametersEditor
        parameters={dataset.parameters}
        selected={draft.definition.parameters}
        disabled={!isAdmin || busy !== null}
        onToggle={toggleParameter}
        onUpdate={updateParameter}
      />

      <section className="grid gap-5 lg:grid-cols-3">
        <fieldset className="space-y-3 rounded-lg border border-slate-200 p-4">
          <legend className="px-1 font-semibold">Sortare</legend>
          {draft.definition.sort.map((sort, index) => (
            <div key={`${sort.field}-${index}`} className="flex gap-2">
              <select
                value={sort.field}
                disabled={!isAdmin || busy !== null}
                onChange={event =>
                  changeDefinition(definition => ({
                    ...definition,
                    sort: definition.sort.map((item, itemIndex) =>
                      itemIndex === index
                        ? { ...item, field: event.target.value }
                        : item,
                    ),
                  }))
                }
                className={inputClass}
              >
                {dataset.fields.map(field => (
                  <option key={field.key} value={field.key}>
                    {field.label}
                  </option>
                ))}
              </select>

              <select
                value={sort.dir}
                disabled={!isAdmin || busy !== null}
                onChange={event =>
                  changeDefinition(definition => ({
                    ...definition,
                    sort: definition.sort.map((item, itemIndex) =>
                      itemIndex === index
                        ? {
                            ...item,
                            dir: event.target.value as "asc" | "desc",
                          }
                        : item,
                    ),
                  }))
                }
                className={inputClass}
              >
                <option value="asc">Crescător</option>
                <option value="desc">Descrescător</option>
              </select>

              <button
                type="button"
                disabled={!isAdmin || busy !== null}
                className={secondaryButtonClass}
                onClick={() =>
                  changeDefinition(definition => ({
                    ...definition,
                    sort: definition.sort.filter(
                      (_, itemIndex) => itemIndex !== index,
                    ),
                  }))
                }
              >
                Elimină
              </button>
            </div>
          ))}

          <button
            type="button"
            disabled={!isAdmin || busy !== null}
            className={secondaryButtonClass}
            onClick={addSort}
          >
            Adaugă sortare
          </button>
        </fieldset>

        <fieldset className="space-y-3 rounded-lg border border-slate-200 p-4">
          <legend className="px-1 font-semibold">Grupare</legend>
          <label>
            Câmp (un singur nivel)
            <select
              value={draft.definition.groupBy?.field ?? ""}
              disabled={!isAdmin || busy !== null}
              onChange={event =>
                changeDefinition(definition => ({
                  ...definition,
                  groupBy: event.target.value
                    ? { field: event.target.value }
                    : null,
                }))
              }
              className={inputClass}
            >
              <option value="">Fără grupare</option>
              {dataset.fields.map(field => (
                <option key={field.key} value={field.key}>
                  {field.label}
                </option>
              ))}
            </select>
          </label>
        </fieldset>

        <TotalsEditor
          fields={dataset.fields}
          totals={draft.definition.totals}
          disabled={!isAdmin || busy !== null}
          onAdd={addTotal}
          onChange={totals =>
            changeDefinition(definition => ({
              ...definition,
              totals,
            }))
          }
        />
      </section>

      <fieldset className="grid gap-4 rounded-lg border border-slate-200 p-4 sm:grid-cols-2">
        <legend className="px-1 font-semibold">Aspect PDF</legend>

        <label>
          Titlu
          <input
            value={draft.definition.layout.title}
            disabled={!isAdmin || busy !== null}
            maxLength={200}
            required
            onChange={event =>
              changeDefinition(definition => ({
                ...definition,
                layout: {
                  ...definition.layout,
                  title: event.target.value,
                },
              }))
            }
            className={inputClass}
          />
        </label>

        <label>
          Subtitlu
          <input
            value={draft.definition.layout.subtitle ?? ""}
            disabled={!isAdmin || busy !== null}
            maxLength={500}
            placeholder="Perioada {dateFrom} – {dateTo}"
            onChange={event =>
              changeDefinition(definition => ({
                ...definition,
                layout: {
                  ...definition.layout,
                  subtitle: event.target.value || undefined,
                },
              }))
            }
            className={inputClass}
          />
        </label>

        <label>
          Orientare
          <select
            value={draft.definition.layout.orientation}
            disabled={!isAdmin || busy !== null}
            onChange={event =>
              changeDefinition(definition => ({
                ...definition,
                layout: {
                  ...definition.layout,
                  orientation: event.target.value as
                    | "portrait"
                    | "landscape",
                },
              }))
            }
            className={inputClass}
          >
            <option value="portrait">Portret</option>
            <option value="landscape">Peisaj</option>
          </select>
        </label>

        <label className="flex items-center gap-2 self-end pb-2">
          <input
            type="checkbox"
            checked={draft.definition.layout.showPageNumbers}
            disabled={!isAdmin || busy !== null}
            onChange={event =>
              changeDefinition(definition => ({
                ...definition,
                layout: {
                  ...definition.layout,
                  showPageNumbers: event.target.checked,
                },
              }))
            }
          />
          Afișează numerele de pagină în PDF
        </label>
      </fieldset>

      <ReportRunParameters
        datasetKey={draft.datasetKey}
        parameters={draft.definition.parameters}
        values={parameterValues}
        registryTypes={registryTypes.data ?? []}
        departments={departments.data ?? []}
        onChange={(name, value) =>
          setParameterValues(previous => ({
            ...previous,
            [name]: value,
          }))
        }
      />

      <section className="flex flex-wrap gap-3">
        {isAdmin && (
          <button
            type="submit"
            disabled={busy !== null ||
              (draft.definition.renderMode === "table" && widthTotal !== 100)}
            className={buttonClass}
          >
            {busy === "save" ? "Salvăm…" : "Salvează definiția"}
          </button>
        )}

        {!isNew && isAdmin && !draft.isSystem && (
          <button
            type="button"
            disabled={busy !== null}
            className="rounded-lg bg-red-700 px-4 py-2 text-white hover:bg-red-800 disabled:opacity-50"
            onClick={() => void deleteReport()}
          >
            {busy === "delete" ? "Ștergem…" : "Șterge raportul"}
          </button>
        )}

        {!isAdmin && (
          <p className="self-center text-sm text-slate-600">
            Poți rula și exporta rapoarte, dar numai un administrator le
            poate modifica.
          </p>
        )}
      </section>

      {preview && <PreviewTable preview={preview} />}
    </form>
  );
}

function ReportParametersEditor({
  parameters,
  selected,
  disabled,
  onToggle,
  onUpdate,
}: {
  parameters: ReportDatasetParameter[];
  selected: ReportDefinition["parameters"];
  disabled: boolean;
  onToggle: (parameter: ReportDatasetParameter) => void;
  onUpdate: (
    name: string,
    update: Partial<ReportDatasetParameter & { required: boolean }>,
  ) => void;
}) {
  return (
    <section className="space-y-3">
      <h3 className="font-semibold">Parametri de rulare</h3>

      {parameters.length === 0 ? (
        <p className="text-sm text-slate-600">
          Acest set de date nu declară parametri.
        </p>
      ) : (
        <div className="space-y-3">
          {parameters.map(parameter => {
            const selectedParameter = selected.find(
              item => item.name === parameter.name,
            );

            return (
              <div
                key={parameter.name}
                className="grid gap-3 rounded-lg border border-slate-200 p-3 md:grid-cols-[1fr_2fr_auto]"
              >
                <label className="flex items-center gap-2 self-center">
                  <input
                    type="checkbox"
                    checked={Boolean(selectedParameter)}
                    disabled={disabled}
                    onChange={() => onToggle(parameter)}
                  />
                  <span>
                    {parameter.label}
                    <span className="ml-1 text-sm text-slate-600">
                      ({parameter.type})
                    </span>
                  </span>
                </label>

                {selectedParameter ? (
                  <label>
                    Etichetă
                    <input
                      value={selectedParameter.label}
                      disabled={disabled}
                      maxLength={200}
                      onChange={event =>
                        onUpdate(parameter.name, {
                          label: event.target.value,
                        })
                      }
                      className={inputClass}
                    />
                  </label>
                ) : (
                  <div />
                )}

                {selectedParameter && (
                  <label className="flex items-center gap-2 self-end pb-2">
                    <input
                      type="checkbox"
                      checked={selectedParameter.required}
                      disabled={disabled}
                      onChange={event =>
                        onUpdate(parameter.name, {
                          required: event.target.checked,
                        })
                      }
                    />
                    Obligatoriu
                  </label>
                )}
              </div>
            );
          })}
        </div>
      )}
    </section>
  );
}

function TotalsEditor({
  fields,
  totals,
  disabled,
  onAdd,
  onChange,
}: {
  fields: ReportDatasetField[];
  totals: ReportTotal[];
  disabled: boolean;
  onAdd: () => void;
  onChange: (totals: ReportTotal[]) => void;
}) {
  function update(index: number, change: Partial<ReportTotal>) {
    onChange(
      totals.map((total, totalIndex) =>
        totalIndex === index ? { ...total, ...change } : total,
      ),
    );
  }

  return (
    <fieldset className="space-y-3 rounded-lg border border-slate-200 p-4">
      <legend className="px-1 font-semibold">Totaluri</legend>

      {totals.map((total, index) => {
        const field = fields.find(item => item.key === total.field);
        const aggregations = field?.isNumeric
          ? ["count", "sum", "avg"] as const
          : ["count"] as const;

        return (
          <div key={`${total.field}-${index}`} className="flex gap-2">
            <select
              value={total.field}
              disabled={disabled}
              onChange={event =>
                update(index, {
                  field: event.target.value,
                  agg: "count",
                })
              }
              className={inputClass}
            >
              {fields.map(item => (
                <option key={item.key} value={item.key}>
                  {item.label}
                </option>
              ))}
            </select>

            <select
              value={total.agg}
              disabled={disabled}
              onChange={event =>
                update(index, {
                  agg: event.target.value as ReportTotal["agg"],
                })
              }
              className={inputClass}
            >
              {aggregations.map(aggregation => (
                <option key={aggregation} value={aggregation}>
                  {aggregation}
                </option>
              ))}
            </select>

            <button
              type="button"
              disabled={disabled}
              className={secondaryButtonClass}
              onClick={() =>
                onChange(
                  totals.filter((_, totalIndex) => totalIndex !== index),
                )
              }
            >
              Elimină
            </button>
          </div>
        );
      })}

      <button
        type="button"
        disabled={disabled || fields.length === 0}
        className={secondaryButtonClass}
        onClick={onAdd}
      >
        Adaugă total
      </button>
    </fieldset>
  );
}

function ReportRunParameters({
  datasetKey,
  parameters,
  values,
  registryTypes,
  departments,
  onChange,
}: {
  datasetKey: string;
  parameters: ReportDefinition["parameters"];
  values: Record<string, string>;
  registryTypes: RegistryType[];
  departments: Department[];
  onChange: (name: string, value: string) => void;
}) {
  if (parameters.length === 0) return null;

  return (
    <fieldset className="grid gap-4 rounded-lg border border-slate-200 p-4 sm:grid-cols-2 lg:grid-cols-3">
      <legend className="px-1 font-semibold">Valori pentru rulare</legend>

      {parameters.map(parameter => (
        <label key={parameter.name}>
          {parameter.label}
          <ParameterInput
            datasetKey={datasetKey}
            parameter={parameter}
            value={values[parameter.name] ?? ""}
            registryTypes={registryTypes}
            departments={departments}
            onChange={value => onChange(parameter.name, value)}
          />
        </label>
      ))}
    </fieldset>
  );
}

function ParameterInput({
  datasetKey,
  parameter,
  value,
  registryTypes,
  departments,
  onChange,
}: {
  datasetKey: string;
  parameter: ReportDefinition["parameters"][number];
  value: string;
  registryTypes: RegistryType[];
  departments: Department[];
  onChange: (value: string) => void;
}) {
  if (parameter.type === "lookup" && parameter.source === "registry_types") {
    return (
      <select
        required={parameter.required}
        value={value}
        onChange={event => onChange(event.target.value)}
        className={inputClass}
      >
        <option value="">Alege registrul</option>
        {registryTypes.map(item => (
          <option key={item.id} value={item.id}>
            {item.code} — {item.name}
          </option>
        ))}
      </select>
    );
  }

  if (parameter.type === "lookup" && parameter.source === "departments") {
    return (
      <select
        required={parameter.required}
        value={value}
        onChange={event => onChange(event.target.value)}
        className={inputClass}
      >
        <option value="">Alege compartimentul</option>
        {departments.map(item => (
          <option key={item.id} value={item.id}>
            {item.code} — {item.name}
          </option>
        ))}
      </select>
    );
  }

  if (parameter.type === "lookup" && parameter.source === "statuses") {
    const statuses = datasetKey === "tasks"
      ? ["Open", "Done", "Cancelled"]
      : [
          "Submitted",
          "Registered",
          "InReview",
          "InfoRequested",
          "Completed",
          "Rejected",
          "Cancelled",
        ];

    return (
      <select
        required={parameter.required}
        value={value}
        onChange={event => onChange(event.target.value)}
        className={inputClass}
      >
        <option value="">Alege starea</option>
        {statuses.map(status => (
          <option key={status} value={status}>
            {status}
          </option>
        ))}
      </select>
    );
  }

  if (parameter.type === "bool") {
    return (
      <select
        required={parameter.required}
        value={value}
        onChange={event => onChange(event.target.value)}
        className={inputClass}
      >
        <option value="">Alege</option>
        <option value="true">Da</option>
        <option value="false">Nu</option>
      </select>
    );
  }

  return (
    <input
      type={parameter.type === "date" ? "date" : parameter.type === "int" ? "number" : "text"}
      required={parameter.required}
      value={value}
      onChange={event => onChange(event.target.value)}
      className={inputClass}
    />
  );
}

function PreviewTable({ preview }: { preview: ReportPreview }) {
  const alignmentClass = {
    left: "text-left",
    center: "text-center",
    right: "text-right",
  } as const;

  return (
    <section className="space-y-4 rounded-lg border border-blue-200 bg-blue-50 p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h3 className="font-semibold">Previzualizare</h3>
        <p className="text-sm text-slate-700">
          {preview.meta.total} rânduri · pagina {preview.meta.page}
        </p>
      </div>

      {preview.rows.length === 0 ? (
        <p>Nu există date pentru parametrii selectați.</p>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white">
          <table className="w-full min-w-[44rem] table-fixed text-sm">
            <thead className="bg-slate-200">
              <tr>
                {preview.columns.map(column => (
                  <th
                    key={column.field}
                    scope="col"
                    style={{ width: `${column.widthPct}%` }}
                    className={`p-3 ${alignmentClass[column.align]}`}
                  >
                    {column.label}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {preview.rows.map((row, rowIndex) => (
                <tr key={rowIndex} className="border-t border-slate-200 align-top">
                  {preview.columns.map(column => (
                    <td
                      key={column.field}
                      className={
                        "break-words p-3 " + alignmentClass[column.align]
                      }
                    >
                      {formatPreviewValue(row[column.field])}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {Object.keys(preview.totals).length > 0 && (
        <dl className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
          {Object.entries(preview.totals).map(([key, value]) => (
            <div key={key} className="rounded bg-white p-3">
              <dt className="text-sm text-slate-600">{key}</dt>
              <dd className="font-semibold">
                {new Intl.NumberFormat("ro-RO").format(value)}
              </dd>
            </div>
          ))}
        </dl>
      )}
    </section>
  );
}
