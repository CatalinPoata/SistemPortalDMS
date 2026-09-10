"use client";

import Link from "next/link";
import {
  useEffect,
  useMemo,
  useState,
  type DragEvent,
  type FormEvent,
} from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { useAuth } from "@/hooks/use-auth";
import { apiDownload, apiRequest } from "@/lib/auth-store";
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

const inputClass =
  "mt-1 w-full rounded-md border border-slate-300 bg-white px-3 py-2 " +
  "text-slate-950 focus:border-blue-600 focus:outline-none focus:ring-2 " +
  "focus:ring-blue-200 disabled:bg-slate-100";

const buttonClass =
  "rounded-md bg-blue-700 px-3 py-2 text-sm font-medium text-white " +
  "hover:bg-blue-800 disabled:cursor-wait disabled:opacity-60";

const secondaryButtonClass =
  "rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium " +
  "text-slate-800 hover:bg-slate-50 disabled:cursor-wait disabled:opacity-60";

type Draft = {
  code: string;
  name: string;
  datasetKey: string;
  definition: ReportDefinition;
  version: number | null;
  isSystem: boolean;
};

type Paged<T> = { items: T[]; page: number; pageSize: number; total: number };
type LookupItem = { id: string; code: string; title?: string; name?: string };

function ErrorMessage({ error }: { error: ApiError | null }) {
  if (!error) return null;
  return <ApiErrorPanel error={error} />;
}

function defaultAlign(field: ReportDatasetField): NonNullable<ReportColumn["align"]> {
  return field.type === "number" || field.type === "date" || field.type === "code"
    ? "center"
    : "left";
}

function normalizeWidths(columns: ReportColumn[]) {
  if (!columns.length) return columns;
  const unit = Math.floor(100 / columns.length);

  return columns.map((column, index) => ({
    ...column,
    widthPct: index === columns.length - 1
      ? 100 - unit * (columns.length - 1)
      : unit,
  }));
}

function newDraft(dataset: ReportDataset): Draft {
  return {
    code: "",
    name: "",
    datasetKey: dataset.key,
    definition: createDefaultDefinition(dataset),
    version: null,
    isSystem: false,
  };
}

function savedDraft(report: ReportDetails): Draft {
  return {
    code: report.code,
    name: report.name,
    datasetKey: report.datasetKey,
    definition: report.definition,
    version: report.version,
    isSystem: report.isSystem,
  };
}

export default function ReportsPage() {
  const auth = useAuth();

  if (auth.status === "loading") {
    return <main className="min-h-screen bg-slate-100 p-6">Verificăm sesiunea…</main>;
  }

  if (auth.status === "error") {
    return <main className="min-h-screen bg-slate-100 p-6"><ErrorMessage error={auth.error} /></main>;
  }

  if (auth.status !== "authenticated") {
    return <main className="min-h-screen bg-slate-100 p-6">Autentifică-te pentru a administra rapoartele.</main>;
  }

  return <ReportsWorkspace />;
}

function ReportsWorkspace() {
  const [reports, setReports] = useState<ReportSummary[] | null>(null);
  const [datasets, setDatasets] = useState<ReportDataset[] | null>(null);
  const [services, setServices] = useState<LookupItem[]>([]);
  const [appointmentTypes, setAppointmentTypes] = useState<LookupItem[]>([]);
  const [error, setError] = useState<ApiError | null>(null);
  const [revision, setRevision] = useState(0);
  const [selectedCode, setSelectedCode] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  useEffect(() => {
    let current = true;

    void Promise.all([
      apiRequest<ReportSummary[]>("/api/report-definitions"),
      apiRequest<ReportDataset[]>("/api/report-definitions/datasets"),
      apiRequest<Paged<LookupItem>>("/api/service-definitions?page=1&pageSize=100"),
      apiRequest<Paged<LookupItem>>("/api/appointment-types?page=1&pageSize=100"),
    ]).then(([nextReports, nextDatasets, servicePage, typePage]) => {
      if (!current) return;
      setReports(nextReports);
      setDatasets(nextDatasets);
      setServices(servicePage.items);
      setAppointmentTypes(typePage.items);
      setError(null);
    }).catch(failure => {
      if (current) setError(asApiError(failure));
    });

    return () => { current = false; };
  }, [revision]);

  function reload(code?: string | null) {
    setRevision(value => value + 1);
    setCreating(false);
    setSelectedCode(code ?? null);
  }

  return (
    <main className="min-h-screen bg-slate-100 p-4 text-slate-950 sm:p-6">
      <div className="mx-auto max-w-7xl space-y-6">
        <header className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <p className="text-sm font-semibold uppercase tracking-wider text-blue-700">Portal de servicii</p>
            <h1 className="text-2xl font-semibold">Rapoarte</h1>
          </div>
          <Link href="/" className="text-blue-700 underline">Înapoi la administrare</Link>
        </header>

        <ErrorMessage error={error} />

        {!reports || !datasets ? (
          <p role="status">Încărcăm definițiile de raport…</p>
        ) : (
          <div className="grid gap-6 lg:grid-cols-[18rem_minmax(0,1fr)]">
            <aside className="space-y-4 rounded-xl bg-white p-4 shadow-sm">
              <div className="flex items-center justify-between gap-2">
                <h2 className="font-semibold">Definiții salvate</h2>
                <button
                  className={secondaryButtonClass}
                  type="button"
                  onClick={() => { setSelectedCode(null); setCreating(true); }}
                >
                  Raport nou
                </button>
              </div>

              {reports.length === 0 ? (
                <p className="text-sm text-slate-600">Nu există încă rapoarte salvate.</p>
              ) : (
                <ul className="space-y-2">
                  {reports.map(report => (
                    <li key={report.id}>
                      <button
                        type="button"
                        className={
                          "w-full rounded-lg p-3 text-left hover:bg-slate-100 " +
                          (selectedCode === report.code ? "bg-blue-50 outline outline-2 outline-blue-700" : "bg-slate-50")
                        }
                        onClick={() => { setCreating(false); setSelectedCode(report.code); }}
                      >
                        <span className="block font-medium">{report.name}</span>
                        <span className="block break-all text-sm text-slate-600">
                          {report.code} · v{report.version}{report.isSystem ? " · sistem" : ""}
                        </span>
                      </button>
                    </li>
                  ))}
                </ul>
              )}
            </aside>

            <section className="min-w-0">
              {creating && datasets[0] && (
                <ReportEditor
                  key="new"
                  initialDraft={newDraft(datasets[0])}
                  datasets={datasets}
                  services={services}
                  appointmentTypes={appointmentTypes}
                  onSaved={report => reload(report.code)}
                  onDeleted={() => reload(null)}
                />
              )}
              {!creating && selectedCode && (
                <ExistingReportEditor
                  code={selectedCode}
                  datasets={datasets}
                  services={services}
                  appointmentTypes={appointmentTypes}
                  onSaved={report => reload(report.code)}
                  onDeleted={() => reload(null)}
                />
              )}
              {!creating && !selectedCode && (
                <div className="rounded-xl bg-white p-6 text-slate-600 shadow-sm">
                  Alege un raport sau creează o definiție nouă.
                </div>
              )}
            </section>
          </div>
        )}
      </div>
    </main>
  );
}

function ExistingReportEditor({
  code,
  datasets,
  services,
  appointmentTypes,
  onSaved,
  onDeleted,
}: {
  code: string;
  datasets: ReportDataset[];
  services: LookupItem[];
  appointmentTypes: LookupItem[];
  onSaved: (report: ReportDetails) => void;
  onDeleted: () => void;
}) {
  const [report, setReport] = useState<ReportDetails | null>(null);
  const [error, setError] = useState<ApiError | null>(null);

  useEffect(() => {
    let current = true;
    void apiRequest<ReportDetails>(`/api/report-definitions/${encodeURIComponent(code)}`)
      .then(value => { if (current) { setReport(value); setError(null); } })
      .catch(failure => { if (current) setError(asApiError(failure)); });
    return () => { current = false; };
  }, [code]);

  if (error) return <ErrorMessage error={error} />;
  if (!report) return <p role="status">Încărcăm definiția…</p>;

  return (
    <ReportEditor
      initialDraft={savedDraft(report)}
      datasets={datasets}
      services={services}
      appointmentTypes={appointmentTypes}
      onSaved={onSaved}
      onDeleted={onDeleted}
    />
  );
}

function ReportEditor({
  initialDraft,
  datasets,
  services,
  appointmentTypes,
  onSaved,
  onDeleted,
}: {
  initialDraft: Draft;
  datasets: ReportDataset[];
  services: LookupItem[];
  appointmentTypes: LookupItem[];
  onSaved: (report: ReportDetails) => void;
  onDeleted: () => void;
}) {
  const [draft, setDraft] = useState(initialDraft);
  const [parameterValues, setParameterValues] = useState<Record<string, string>>({});
  const [preview, setPreview] = useState<ReportPreview | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState<"save" | "preview" | "export" | "delete" | null>(null);
  const [unsaved, setUnsaved] = useState(false);
  const [draggedField, setDraggedField] = useState<string | null>(null);

  const dataset = useMemo(
    () => datasets.find(item => item.key === draft.datasetKey) ?? null,
    [datasets, draft.datasetKey],
  );
  const isNew = draft.version === null;
  const widthTotal = draft.definition.columns.reduce((sum, column) => sum + column.widthPct, 0);
  const canExport = !isNew && !unsaved && busy === null;

  function change(next: Draft) {
    setDraft(next);
    setUnsaved(true);
    setPreview(null);
  }

  function changeDefinition(update: (definition: ReportDefinition) => ReportDefinition) {
    change({ ...draft, definition: update(draft.definition) });
  }

  function selectDataset(key: string) {
    const next = datasets.find(item => item.key === key);
    if (!next) return;
    change({ ...draft, datasetKey: next.key, definition: createDefaultDefinition(next) });
    setParameterValues({});
  }

  function toggleField(field: ReportDatasetField) {
    changeDefinition(definition => {
      const selected = definition.columns.some(item => item.field === field.key);
      const columns = selected
        ? definition.columns.filter(item => item.field !== field.key)
        : [...definition.columns, {
          field: field.key,
          label: field.label,
          type: field.type,
          align: defaultAlign(field),
          widthPct: 1,
        }];
      return { ...definition, columns: definition.renderMode === "table" ? normalizeWidths(columns) : columns };
    });
  }

  function updateColumn(field: string, update: Partial<ReportColumn>) {
    changeDefinition(definition => ({
      ...definition,
      columns: definition.columns.map(column => column.field === field ? { ...column, ...update } : column),
    }));
  }

  function moveColumn(source: string, target: string) {
    if (source === target) return;
    changeDefinition(definition => {
      const columns = [...definition.columns];
      const from = columns.findIndex(item => item.field === source);
      const to = columns.findIndex(item => item.field === target);
      if (from < 0 || to < 0) return definition;
      const [moved] = columns.splice(from, 1);
      columns.splice(to, 0, moved);
      return { ...definition, columns };
    });
  }

  function toggleParameter(parameter: ReportDatasetParameter) {
    changeDefinition(definition => {
      const exists = definition.parameters.some(item => item.name === parameter.name);
      return {
        ...definition,
        parameters: exists
          ? definition.parameters.filter(item => item.name !== parameter.name)
          : [...definition.parameters, { ...parameter, required: false }],
      };
    });
  }

  function updateParameter(name: string, update: Partial<ReportDefinition["parameters"][number]>) {
    changeDefinition(definition => ({
      ...definition,
      parameters: definition.parameters.map(item => item.name === name ? { ...item, ...update } : item),
    }));
  }

  function addSort() {
    const field = dataset?.fields.find(item => !draft.definition.sort.some(sort => sort.field === item.key));
    if (field) changeDefinition(definition => ({ ...definition, sort: [...definition.sort, { field: field.key, dir: "asc" }] }));
  }

  function addTotal() {
    const field = dataset?.fields[0];
    if (field) changeDefinition(definition => ({ ...definition, totals: [...definition.totals, { field: field.key, agg: "count" }] }));
  }

  async function previewReport() {
    setBusy("preview");
    setError(null);
    try {
      const query = createQueryString(parameterValues);
      const path = isNew || unsaved
        ? (query ? `/api/report-definitions/preview?${query}` : "/api/report-definitions/preview")
        : (query ? `/api/report-definitions/${encodeURIComponent(draft.code)}/preview?${query}` : `/api/report-definitions/${encodeURIComponent(draft.code)}/preview`);
      const result = isNew || unsaved
        ? await apiRequest<ReportPreview>(path, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ datasetKey: draft.datasetKey, definition: draft.definition }),
        })
        : await apiRequest<ReportPreview>(path);
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
      const path = isNew ? "/api/report-definitions" : `/api/report-definitions/${encodeURIComponent(draft.code)}`;
      const body = isNew
        ? { code: draft.code.trim().toLowerCase(), name: draft.name.trim(), datasetKey: draft.datasetKey, definition: draft.definition }
        : { name: draft.name.trim(), datasetKey: draft.datasetKey, definition: draft.definition, expectedVersion: draft.version };
      const saved = await apiRequest<ReportDetails>(path, {
        method: isNew ? "POST" : "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });
      setUnsaved(false);
      onSaved(saved);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(null);
    }
  }

  async function exportPdf() {
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

  async function exportCsv() {
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
    if (!window.confirm("Ștergi definitiv această definiție de raport?")) return;
    setBusy("delete");
    setError(null);
    try {
      await apiRequest<void>(`/api/report-definitions/${encodeURIComponent(draft.code)}`, { method: "DELETE" });
      onDeleted();
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(null);
    }
  }

  if (!dataset) return <ErrorMessage error={new ApiError(422, { detail: "Setul de date nu mai este disponibil." })} />;

  return (
    <form onSubmit={save} className="space-y-6 rounded-xl bg-white p-5 shadow-sm">
      <header className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-xl font-semibold">{isNew ? "Definiție nouă" : draft.name}</h2>
          {!isNew && <p className="text-sm text-slate-600">Cod: {draft.code} · versiunea {draft.version}</p>}
        </div>
        <div className="flex flex-wrap gap-2">
          <button type="button" className={secondaryButtonClass} disabled={busy !== null} onClick={() => void previewReport()}>
            {busy === "preview" ? "Generăm…" : "Actualizează previzualizarea"}
          </button>
          <button type="button" className={secondaryButtonClass} disabled={!canExport} onClick={() => void exportPdf()}>
            {busy === "export" ? "Descărcăm…" : "Export PDF"}
          </button>
          <button type="button" className={secondaryButtonClass} disabled={!canExport} onClick={() => void exportCsv()}>
            {busy === "export" ? "Descărcăm…" : "Export CSV"}
          </button>
        </div>
      </header>

      {(isNew || unsaved) && (
        <p className="rounded-lg bg-amber-50 p-3 text-sm text-amber-950">
          Previzualizarea poate rula modificările curente. Salvează definiția înainte de exportul PDF.
        </p>
      )}
      <ErrorMessage error={error} />

      <fieldset className="grid gap-4 sm:grid-cols-2">
        <legend className="mb-2 font-semibold">Identitate și set de date</legend>
        <label>Cod
          <input className={inputClass} required maxLength={50} pattern="[a-z0-9]+(?:-[a-z0-9]+)*" disabled={!isNew || busy !== null} value={draft.code} onChange={event => change({ ...draft, code: event.target.value })} />
        </label>
        <label>Denumire
          <input className={inputClass} required maxLength={200} disabled={busy !== null} value={draft.name} onChange={event => change({ ...draft, name: event.target.value })} />
        </label>
        <label>Set de date
          <select className={inputClass} disabled={busy !== null} value={draft.datasetKey} onChange={event => selectDataset(event.target.value)}>
            {datasets.map(item => <option key={item.key} value={item.key}>{item.label}</option>)}
          </select>
        </label>
        <label>Mod de randare
          <select className={inputClass} disabled={busy !== null} value={draft.definition.renderMode} onChange={event => changeDefinition(definition => ({
            ...definition,
            renderMode: event.target.value as "table" | "record",
            columns: event.target.value === "table" ? normalizeWidths(definition.columns) : definition.columns,
          }))}>
            <option value="table">Tabel</option><option value="record">Fișă / înregistrare</option>
          </select>
        </label>
      </fieldset>

      <section className="space-y-3">
        <h3 className="font-semibold">Câmpuri disponibile</h3>
        <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
          {dataset.fields.map(field => {
            const checked = draft.definition.columns.some(column => column.field === field.key);
            return <label key={field.key} className="flex items-center gap-2 rounded-lg border border-slate-200 p-3">
              <input type="checkbox" checked={checked} disabled={busy !== null} onChange={() => toggleField(field)} />
              <span>{field.label} <span className="text-sm text-slate-600">({field.type})</span></span>
            </label>;
          })}
        </div>
      </section>

      <section className="space-y-3">
        <div className="flex flex-wrap justify-between gap-2">
          <h3 className="font-semibold">Coloane selectate — trage pentru reordonare</h3>
          {draft.definition.renderMode === "table" && <p className={widthTotal === 100 ? "text-sm text-emerald-700" : "text-sm text-red-700"}>Lățime totală: {widthTotal}% / 100%</p>}
        </div>
        {draft.definition.columns.length === 0 ? <p className="rounded-lg bg-amber-50 p-3 text-sm">Selectează cel puțin o coloană.</p> : (
          <div className="space-y-3">
            {draft.definition.columns.map(column => (
              <div key={column.field} draggable={busy === null} onDragStart={() => setDraggedField(column.field)} onDragOver={event => event.preventDefault()} onDrop={(event: DragEvent<HTMLDivElement>) => { event.preventDefault(); if (draggedField) moveColumn(draggedField, column.field); setDraggedField(null); }} className="grid gap-3 rounded-lg border border-slate-200 p-3 md:grid-cols-[9rem_minmax(10rem,2fr)_8rem_7rem_9rem]">
                <p className="self-center font-medium" title="Trage pentru reordonare">↕ {column.field}</p>
                <label>Etichetă
                  <input className={inputClass} maxLength={200} disabled={busy !== null} value={column.label} onChange={event => updateColumn(column.field, { label: event.target.value })} />
                </label>
                <label>Aliniere
                  <select className={inputClass} disabled={busy !== null} value={column.align ?? "left"} onChange={event => updateColumn(column.field, { align: event.target.value as ReportColumn["align"] })}>
                    <option value="left">Stânga</option><option value="center">Centru</option><option value="right">Dreapta</option>
                  </select>
                </label>
                {draft.definition.renderMode === "table" ? <label>Lățime %
                  <input className={inputClass} type="number" min="1" max="100" disabled={busy !== null} value={column.widthPct} onChange={event => updateColumn(column.field, { widthPct: Number(event.target.value) })} />
                </label> : <div />}
                <label>Format
                  <input className={inputClass} placeholder="dd.MM.yyyy" disabled={busy !== null} value={column.format ?? ""} onChange={event => updateColumn(column.field, { format: event.target.value || undefined })} />
                </label>
              </div>
            ))}
          </div>
        )}
      </section>

      <ParametersDesigner parameters={dataset.parameters} selected={draft.definition.parameters} disabled={busy !== null} onToggle={toggleParameter} onUpdate={updateParameter} />

      <section className="grid gap-5 lg:grid-cols-3">
        <fieldset className="space-y-3 rounded-lg border border-slate-200 p-4">
          <legend className="px-1 font-semibold">Sortare</legend>
          {draft.definition.sort.map((sort, index) => <div key={`${sort.field}-${index}`} className="flex gap-2">
            <select className={inputClass} disabled={busy !== null} value={sort.field} onChange={event => changeDefinition(definition => ({ ...definition, sort: definition.sort.map((item, itemIndex) => itemIndex === index ? { ...item, field: event.target.value } : item) }))}>
              {dataset.fields.map(field => <option key={field.key} value={field.key}>{field.label}</option>)}
            </select>
            <select className={inputClass} disabled={busy !== null} value={sort.dir} onChange={event => changeDefinition(definition => ({ ...definition, sort: definition.sort.map((item, itemIndex) => itemIndex === index ? { ...item, dir: event.target.value as "asc" | "desc" } : item) }))}>
              <option value="asc">Crescător</option><option value="desc">Descrescător</option>
            </select>
            <button type="button" className={secondaryButtonClass} disabled={busy !== null} onClick={() => changeDefinition(definition => ({ ...definition, sort: definition.sort.filter((_, itemIndex) => itemIndex !== index) }))}>Elimină</button>
          </div>)}
          <button type="button" className={secondaryButtonClass} disabled={busy !== null} onClick={addSort}>Adaugă sortare</button>
        </fieldset>

        <fieldset className="space-y-3 rounded-lg border border-slate-200 p-4">
          <legend className="px-1 font-semibold">Grupare</legend>
          <label>Câmp (un singur nivel)
            <select className={inputClass} disabled={busy !== null} value={draft.definition.groupBy?.field ?? ""} onChange={event => changeDefinition(definition => ({ ...definition, groupBy: event.target.value ? { field: event.target.value } : null }))}>
              <option value="">Fără grupare</option>
              {dataset.fields.map(field => <option key={field.key} value={field.key}>{field.label}</option>)}
            </select>
          </label>
        </fieldset>

        <TotalsDesigner fields={dataset.fields} totals={draft.definition.totals} disabled={busy !== null} onAdd={addTotal} onChange={totals => changeDefinition(definition => ({ ...definition, totals }))} />
      </section>

      <fieldset className="grid gap-4 rounded-lg border border-slate-200 p-4 sm:grid-cols-2">
        <legend className="px-1 font-semibold">Aspect PDF</legend>
        <label>Titlu
          <input className={inputClass} required maxLength={200} disabled={busy !== null} value={draft.definition.layout.title} onChange={event => changeDefinition(definition => ({ ...definition, layout: { ...definition.layout, title: event.target.value } }))} />
        </label>
        <label>Subtitlu
          <input className={inputClass} maxLength={500} placeholder="Perioada {dateFrom} – {dateTo}" disabled={busy !== null} value={draft.definition.layout.subtitle ?? ""} onChange={event => changeDefinition(definition => ({ ...definition, layout: { ...definition.layout, subtitle: event.target.value || undefined } }))} />
        </label>
        <label>Orientare
          <select className={inputClass} disabled={busy !== null} value={draft.definition.layout.orientation} onChange={event => changeDefinition(definition => ({ ...definition, layout: { ...definition.layout, orientation: event.target.value as "portrait" | "landscape" } }))}>
            <option value="portrait">Portret</option><option value="landscape">Peisaj</option>
          </select>
        </label>
        <label className="flex items-center gap-2 self-end pb-2"><input type="checkbox" checked={draft.definition.layout.showPageNumbers} disabled={busy !== null} onChange={event => changeDefinition(definition => ({ ...definition, layout: { ...definition.layout, showPageNumbers: event.target.checked } }))} /> Afișează numerele de pagină</label>
      </fieldset>

      <RunParameters datasetKey={draft.datasetKey} parameters={draft.definition.parameters} values={parameterValues} services={services} appointmentTypes={appointmentTypes} onChange={(name, value) => setParameterValues(previous => ({ ...previous, [name]: value }))} />

      <section className="flex flex-wrap gap-3">
        <button type="submit" className={buttonClass} disabled={busy !== null || (draft.definition.renderMode === "table" && widthTotal !== 100)}>{busy === "save" ? "Salvăm…" : "Salvează definiția"}</button>
        {!isNew && !draft.isSystem && <button type="button" className="rounded-md bg-red-700 px-3 py-2 text-sm font-medium text-white hover:bg-red-800 disabled:opacity-60" disabled={busy !== null} onClick={() => void deleteReport()}>{busy === "delete" ? "Ștergem…" : "Șterge raportul"}</button>}
      </section>

      {preview && <Preview preview={preview} />}
    </form>
  );
}

function ParametersDesigner({ parameters, selected, disabled, onToggle, onUpdate }: {
  parameters: ReportDatasetParameter[];
  selected: ReportDefinition["parameters"];
  disabled: boolean;
  onToggle: (parameter: ReportDatasetParameter) => void;
  onUpdate: (name: string, update: Partial<ReportDefinition["parameters"][number]>) => void;
}) {
  return <section className="space-y-3"><h3 className="font-semibold">Parametri de rulare</h3>
    {parameters.map(parameter => {
      const item = selected.find(value => value.name === parameter.name);
      return <div key={parameter.name} className="grid gap-3 rounded-lg border border-slate-200 p-3 md:grid-cols-[1fr_2fr_auto]">
        <label className="flex items-center gap-2 self-center"><input type="checkbox" checked={Boolean(item)} disabled={disabled} onChange={() => onToggle(parameter)} /> {parameter.label} <span className="text-sm text-slate-600">({parameter.type})</span></label>
        {item ? <label>Etichetă<input className={inputClass} maxLength={200} disabled={disabled} value={item.label} onChange={event => onUpdate(parameter.name, { label: event.target.value })} /></label> : <div />}
        {item && <label className="flex items-center gap-2 self-end pb-2"><input type="checkbox" checked={item.required} disabled={disabled} onChange={event => onUpdate(parameter.name, { required: event.target.checked })} />Obligatoriu</label>}
      </div>;
    })}
  </section>;
}

function TotalsDesigner({ fields, totals, disabled, onAdd, onChange }: {
  fields: ReportDatasetField[];
  totals: ReportTotal[];
  disabled: boolean;
  onAdd: () => void;
  onChange: (totals: ReportTotal[]) => void;
}) {
  function update(index: number, value: Partial<ReportTotal>) {
    onChange(totals.map((item, itemIndex) => itemIndex === index ? { ...item, ...value } : item));
  }
  return <fieldset className="space-y-3 rounded-lg border border-slate-200 p-4"><legend className="px-1 font-semibold">Totaluri</legend>
    {totals.map((total, index) => {
      const numeric = fields.find(field => field.key === total.field)?.isNumeric;
      return <div key={`${total.field}-${index}`} className="flex gap-2">
        <select className={inputClass} disabled={disabled} value={total.field} onChange={event => update(index, { field: event.target.value, agg: "count" })}>{fields.map(field => <option key={field.key} value={field.key}>{field.label}</option>)}</select>
        <select className={inputClass} disabled={disabled} value={total.agg} onChange={event => update(index, { agg: event.target.value as ReportTotal["agg"] })}>
          <option value="count">count</option>{numeric && <><option value="sum">sum</option><option value="avg">avg</option></>}
        </select>
        <button type="button" className={secondaryButtonClass} disabled={disabled} onClick={() => onChange(totals.filter((_, itemIndex) => itemIndex !== index))}>Elimină</button>
      </div>;
    })}
    <button type="button" className={secondaryButtonClass} disabled={disabled} onClick={onAdd}>Adaugă total</button>
  </fieldset>;
}

function RunParameters({ datasetKey, parameters, values, services, appointmentTypes, onChange }: {
  datasetKey: string;
  parameters: ReportDefinition["parameters"];
  values: Record<string, string>;
  services: LookupItem[];
  appointmentTypes: LookupItem[];
  onChange: (name: string, value: string) => void;
}) {
  if (!parameters.length) return null;
  const submissionStatuses = ["Submitted", "Registered", "InReview", "InfoRequested", "Completed", "Rejected", "Cancelled"];
  const appointmentStatuses = ["Requested", "Confirmed", "Rejected", "Cancelled", "Completed", "NoShow"];
  return <fieldset className="grid gap-4 rounded-lg border border-slate-200 p-4 sm:grid-cols-2"><legend className="px-1 font-semibold">Rulează raportul</legend>
    {parameters.map(parameter => {
      const value = values[parameter.name] ?? "";
      if (parameter.source === "service_definitions") return <label key={parameter.name}>{parameter.label}
        <select className={inputClass} required={parameter.required} value={value} onChange={event => onChange(parameter.name, event.target.value)}><option value="">Toate serviciile</option>{services.map(item => <option key={item.id} value={item.id}>{item.code} — {item.title ?? item.name}</option>)}</select>
      </label>;
      if (parameter.source === "appointment_types") return <label key={parameter.name}>{parameter.label}
        <select className={inputClass} required={parameter.required} value={value} onChange={event => onChange(parameter.name, event.target.value)}><option value="">Toate tipurile</option>{appointmentTypes.map(item => <option key={item.id} value={item.id}>{item.code} — {item.name ?? item.title}</option>)}</select>
      </label>;
      if (parameter.source === "statuses") {
        const statuses = datasetKey === "appointments" ? appointmentStatuses : submissionStatuses;
        return <label key={parameter.name}>{parameter.label}<select className={inputClass} required={parameter.required} value={value} onChange={event => onChange(parameter.name, event.target.value)}><option value="">Toate stările</option>{statuses.map(status => <option key={status} value={status}>{status}</option>)}</select></label>;
      }
      return <label key={parameter.name}>{parameter.label}<input className={inputClass} type={parameter.type === "date" ? "date" : parameter.type === "int" ? "number" : "text"} required={parameter.required} value={value} onChange={event => onChange(parameter.name, event.target.value)} /></label>;
    })}
  </fieldset>;
}

function Preview({ preview }: { preview: ReportPreview }) {
  return <section className="space-y-3 rounded-lg border border-blue-200 bg-blue-50 p-4"><h3 className="font-semibold">Previzualizare</h3><p className="text-sm text-slate-700">{preview.meta.total} rezultate; pagina {preview.meta.page}.</p>
    <div className="overflow-x-auto"><table className="min-w-full border-collapse bg-white text-sm"><thead><tr>{preview.columns.map(column => <th key={column.field} className="border border-slate-300 p-2" style={{ textAlign: column.align }}>{column.label}</th>)}</tr></thead><tbody>
      {preview.rows.map((row, index) => <tr key={index}>{preview.columns.map(column => <td key={column.field} className="border border-slate-300 p-2" style={{ textAlign: column.align }}>{formatPreviewValue(row[column.field])}</td>)}</tr>)}
    </tbody></table></div>
    {Object.keys(preview.totals).length > 0 && <dl className="grid gap-2 sm:grid-cols-3">{Object.entries(preview.totals).map(([key, value]) => <div key={key}><dt className="text-sm text-slate-600">{key}</dt><dd className="font-semibold">{formatPreviewValue(value)}</dd></div>)}</dl>}
  </section>;
}
