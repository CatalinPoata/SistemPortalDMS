"use client";

import { useEffect, useMemo, useState } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { useAuth } from "@/hooks/use-auth";
import { apiRequest } from "@/lib/auth-store";
import { ApiError, asApiError } from "@/lib/http";
import {
  emptyServiceDefinition,
  fieldTypes,
  toWriteModel,
  type FieldType,
  type FormField,
  type PagedResponse,
  type ServiceDefinitionDetails,
  type ServiceDefinitionListItem,
  type ServiceDefinitionWrite,
} from "@/lib/services";

const inputClass =
  "mt-1 w-full rounded-md border border-slate-300 bg-white px-3 py-2 " +
  "text-slate-950 focus:border-blue-600 focus:outline-none focus:ring-2 " +
  "focus:ring-blue-200 disabled:bg-slate-100";

const buttonClass =
  "rounded-md bg-blue-700 px-3 py-2 text-sm font-medium text-white " +
  "hover:bg-blue-800 focus-visible:outline-2 focus-visible:outline-offset-2 " +
  "focus-visible:outline-blue-700 disabled:cursor-wait disabled:opacity-60";

const secondaryButtonClass =
  "rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium " +
  "text-slate-800 hover:bg-slate-50 disabled:cursor-wait disabled:opacity-60";

const destructiveButtonClass =
  "rounded-md bg-red-700 px-3 py-2 text-sm font-medium text-white " +
  "hover:bg-red-800 disabled:cursor-wait disabled:opacity-60";

const optionFieldTypes = new Set<FieldType>([
  "select",
  "radio",
  "checkboxes",
]);

type DmsRegistryType = {
  code: string;
  name: string;
  direction: string;
  isClosed: boolean;
};

function ErrorMessage({ error }: { error: ApiError | null }) {
  if (!error) return null;
  return <ApiErrorPanel error={error} />;
}

function nextKey(prefix: string, usedKeys: string[]) {
  let index = 1;

  while (usedKeys.includes(`${prefix}${index}`)) index += 1;

  return `${prefix}${index}`;
}

function createField(usedKeys: string[]): FormField {
  return {
    key: nextKey("camp", usedKeys),
    label: "Câmp nou",
    type: "text",
    required: false,
    maxLength: 200,
  };
}

function schemaText(schema: ServiceDefinitionWrite["formSchema"]) {
  return JSON.stringify(schema);
}

function updateAt<T>(items: T[], index: number, value: T) {
  return items.map((item, itemIndex) => (itemIndex === index ? value : item));
}

function move<T>(items: T[], from: number, to: number) {
  if (to < 0 || to >= items.length) return items;

  const copy = [...items];
  const [item] = copy.splice(from, 1);
  copy.splice(to, 0, item);
  return copy;
}

function FormPreview({ schema }: { schema: ServiceDefinitionWrite["formSchema"] }) {
  return (
    <div className="space-y-5 rounded-lg border border-slate-200 bg-slate-50 p-4">
      <h3 className="font-semibold">Previzualizare formular</h3>

      {schema.sections.map(section => (
        <section key={section.key} className="space-y-3 rounded-md bg-white p-4 shadow-sm">
          <h4 className="font-medium">{section.title || "Secțiune fără titlu"}</h4>

          {section.fields.map(field => {
            if (field.type === "heading") {
              return <h5 key={field.key} className="text-lg font-semibold">{field.label}</h5>;
            }

            if (field.type === "paragraph") {
              return <p key={field.key} className="text-sm text-slate-600">{field.label}</p>;
            }

            const options = field.options ?? [];

            return (
              <div key={field.key} className="space-y-1">
                <label className="text-sm font-medium">
                  {field.label || field.key}
                  {field.required && <span className="text-red-700"> *</span>}
                </label>

                {field.type === "textarea" ? (
                  <textarea className={inputClass} disabled rows={3} />
                ) : optionFieldTypes.has(field.type) ? (
                  <div className="space-y-1 text-sm">
                    {options.map(option => (
                      <label key={option.value} className="flex items-center gap-2">
                        <input
                          disabled
                          type={field.type === "checkboxes" ? "checkbox" : "radio"}
                          name={field.key}
                        />
                        {option.label || option.value}
                      </label>
                    ))}
                  </div>
                ) : field.type === "boolean" ? (
                  <input disabled type="checkbox" />
                ) : (
                  <input
                    disabled
                    className={inputClass}
                    type={field.type === "nationalId" ? "text" : field.type}
                  />
                )}
              </div>
            );
          })}
        </section>
      ))}
    </div>
  );
}

type EditorProps = {
  code: string | null;
  onSaved: (service: ServiceDefinitionDetails) => void;
  onDeleted: () => void;
};

function ServiceEditor({ code, onSaved, onDeleted }: EditorProps) {
  const [model, setModel] = useState<ServiceDefinitionWrite>(emptyServiceDefinition);
  const [loaded, setLoaded] = useState<ServiceDefinitionDetails | null>(null);
  const [baselineSchema, setBaselineSchema] = useState("");
  const [registryTypes, setRegistryTypes] = useState<DmsRegistryType[] | null>(null);
  const [registryTypesError, setRegistryTypesError] = useState<ApiError | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [busy, setBusy] = useState(false);

  const isNew = code === null;
  const allFieldKeys = useMemo(
    () => model.formSchema.sections.flatMap(section => section.fields.map(field => field.key)),
    [model.formSchema],
  );

  useEffect(() => {
    const controller = new AbortController();

    void apiRequest<DmsRegistryType[]>(
      "/api/service-definitions/dms-registry-types",
      { signal: controller.signal },
    )
      .then(items => {
        setRegistryTypes(items);
        setRegistryTypesError(null);
      })
      .catch(failure => {
        if (controller.signal.aborted) return;
        setRegistryTypesError(asApiError(failure));
      });

    return () => controller.abort();
  }, []);

  useEffect(() => {
    if (code === null) {
      return;
    }

    const controller = new AbortController();

    void apiRequest<ServiceDefinitionDetails>(`/api/service-definitions/${encodeURIComponent(code)}`, {
      signal: controller.signal,
    })
      .then(service => {
        const writeModel = toWriteModel(service);
        setLoaded(service);
        setModel(writeModel);
        setBaselineSchema(schemaText(writeModel.formSchema));
      })
      .catch(failure => {
        if (controller.signal.aborted) return;
        setError(asApiError(failure));
      });

    return () => controller.abort();
  }, [code]);

  function setSchema(nextSchema: ServiceDefinitionWrite["formSchema"]) {
    setModel(current => ({ ...current, formSchema: nextSchema }));
  }

  function updateSectionTitle(sectionIndex: number, title: string) {
    const section = model.formSchema.sections[sectionIndex];

    setSchema({
      sections: updateAt(model.formSchema.sections, sectionIndex, { ...section, title }),
    });
  }

  function addSection() {
    const usedSectionKeys = model.formSchema.sections.map(section => section.key);
    const sectionKey = nextKey("sectiune", usedSectionKeys);

    setSchema({
      sections: [
        ...model.formSchema.sections,
        { key: sectionKey, title: "Secțiune nouă", fields: [] },
      ],
    });
  }

  function removeSection(sectionIndex: number) {
    setSchema({
      sections: model.formSchema.sections.filter((_, index) => index !== sectionIndex),
    });
  }

  function moveSection(sectionIndex: number, direction: -1 | 1) {
    setSchema({
      sections: move(
        model.formSchema.sections,
        sectionIndex,
        sectionIndex + direction,
      ),
    });
  }

  function addField(sectionIndex: number) {
    const section = model.formSchema.sections[sectionIndex];

    setSchema({
      sections: updateAt(model.formSchema.sections, sectionIndex, {
        ...section,
        fields: [...section.fields, createField(allFieldKeys)],
      }),
    });
  }

  function updateField(
    sectionIndex: number,
    fieldIndex: number,
    patch: Partial<FormField>,
  ) {
    const section = model.formSchema.sections[sectionIndex];
    const field = section.fields[fieldIndex];

    setSchema({
      sections: updateAt(model.formSchema.sections, sectionIndex, {
        ...section,
        fields: updateAt(section.fields, fieldIndex, { ...field, ...patch }),
      }),
    });
  }

  function removeField(sectionIndex: number, fieldIndex: number) {
    const section = model.formSchema.sections[sectionIndex];

    setSchema({
      sections: updateAt(model.formSchema.sections, sectionIndex, {
        ...section,
        fields: section.fields.filter((_, index) => index !== fieldIndex),
      }),
    });
  }

  function moveField(sectionIndex: number, fieldIndex: number, direction: -1 | 1) {
    const section = model.formSchema.sections[sectionIndex];

    setSchema({
      sections: updateAt(model.formSchema.sections, sectionIndex, {
        ...section,
        fields: move(section.fields, fieldIndex, fieldIndex + direction),
      }),
    });
  }

  async function save() {
    setBusy(true);
    setError(null);

    try {
      const schemaChanged = baselineSchema !== schemaText(model.formSchema);

      if (loaded && loaded.submissionCount > 0 && schemaChanged) {
        const shouldSave = window.confirm(
          `Acest serviciu are ${loaded.submissionCount} cereri. Modificarea schemei nu schimbă valorile deja depuse. Continui?`,
        );

        if (!shouldSave) return;
      }

      const body = isNew
        ? model
        : {
            title: model.title,
            shortDescription: model.shortDescription,
            description: model.description,
            registryTypeCode: model.registryTypeCode,
            formSchema: model.formSchema,
            requiresAttachment: model.requiresAttachment,
            maxAttachments: model.maxAttachments,
            displayOrder: model.displayOrder,
            expectedSchemaVersion: loaded?.schemaVersion,
          };
      const path = isNew
        ? "/api/service-definitions"
        : `/api/service-definitions/${encodeURIComponent(code)}`;

      const saved = await apiRequest<ServiceDefinitionDetails>(path, {
        method: isNew ? "POST" : "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
      });

      const writeModel = toWriteModel(saved);
      setLoaded(saved);
      setModel(writeModel);
      setBaselineSchema(schemaText(writeModel.formSchema));
      onSaved(saved);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(false);
    }
  }

  async function changePublication(publish: boolean) {
    if (!loaded) return;

    setBusy(true);
    setError(null);

    try {
      const saved = await apiRequest<ServiceDefinitionDetails>(
        `/api/service-definitions/${encodeURIComponent(loaded.code)}/${publish ? "publish" : "unpublish"}`,
        { method: "POST" },
      );

      setLoaded(saved);
      setModel(toWriteModel(saved));
      onSaved(saved);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(false);
    }
  }

  async function deleteService() {
    if (!loaded || !window.confirm(`Ștergi serviciul „${loaded.title}”?`)) return;

    setBusy(true);
    setError(null);

    try {
      await apiRequest<void>(`/api/service-definitions/${encodeURIComponent(loaded.code)}`, {
        method: "DELETE",
      });
      onDeleted();
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="space-y-6 rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-xl font-semibold">
            {isNew ? "Serviciu nou" : `Editează: ${loaded?.title ?? code}`}
          </h2>
          {!isNew && loaded && (
            <p className="mt-1 text-sm text-slate-600">
              Versiunea schemei: {loaded.schemaVersion} · Cereri depuse: {loaded.submissionCount}
            </p>
          )}
        </div>

        {!isNew && loaded && (
          <span
            className={
              loaded.isPublished
                ? "rounded-full bg-emerald-100 px-3 py-1 text-sm font-medium text-emerald-800"
                : "rounded-full bg-slate-100 px-3 py-1 text-sm font-medium text-slate-700"
            }
          >
            {loaded.isPublished ? "Publicat" : "Ciornă"}
          </span>
        )}
      </div>

      <ErrorMessage error={error} />

      {!isNew && !loaded ? (
        <p role="status">Încărcăm serviciul…</p>
      ) : (
        <>
          <div className="grid gap-4 md:grid-cols-2">
            <label className="text-sm font-medium">
              Cod serviciu
              <input
                className={inputClass}
                value={model.code}
                disabled={!isNew || busy}
                onChange={event => setModel(current => ({ ...current, code: event.target.value }))}
                placeholder="certificat-fiscal"
              />
              <span className="mt-1 block text-xs font-normal text-slate-500">
                Litere mici, cifre și cratimă. Codul nu se modifică după creare.
              </span>
            </label>

            <label className="text-sm font-medium">
              Registru DMS
              <select
                className={inputClass}
                value={model.registryTypeCode}
                disabled={busy || registryTypes === null}
                onChange={event => setModel(current => ({
                  ...current,
                  registryTypeCode: event.target.value,
                }))}
              >
                <option value="">
                  {registryTypes === null
                    ? "Se încarcă registrele…"
                    : "Selectează registrul"}
                </option>
                {registryTypes !== null &&
                  model.registryTypeCode &&
                  !registryTypes.some(
                    item => item.code === model.registryTypeCode,
                  ) && (
                    <option value={model.registryTypeCode} disabled>
                      {model.registryTypeCode} — indisponibil în DMS
                    </option>
                  )}
                {registryTypes?.map(registry => (
                  <option
                    key={registry.code}
                    value={registry.code}
                    disabled={registry.isClosed}
                  >
                    {registry.code} — {registry.name} ({registry.direction})
                    {registry.isClosed ? " — închis" : ""}
                  </option>
                ))}
              </select>
              <span className="mt-1 block text-xs font-normal text-slate-500">
                Registrele închise sunt afișate, dar nu pot fi selectate.
              </span>
              {registryTypesError && (
                <span className="mt-1 block text-xs font-normal text-red-700">
                  {registryTypesError.message}
                </span>
              )}
            </label>

            <label className="text-sm font-medium md:col-span-2">
              Titlu
              <input
                className={inputClass}
                value={model.title}
                disabled={busy}
                onChange={event => setModel(current => ({ ...current, title: event.target.value }))}
              />
            </label>

            <label className="text-sm font-medium md:col-span-2">
              Descriere scurtă
              <textarea
                className={inputClass}
                value={model.shortDescription}
                disabled={busy}
                rows={2}
                onChange={event => setModel(current => ({ ...current, shortDescription: event.target.value }))}
              />
            </label>

            <label className="text-sm font-medium md:col-span-2">
              Descriere HTML
              <textarea
                className={inputClass}
                value={model.description}
                disabled={busy}
                rows={5}
                onChange={event => setModel(current => ({ ...current, description: event.target.value }))}
              />
              <span className="mt-1 block text-xs font-normal text-slate-500">
                HTML-ul va fi sanitizat pe server la salvare.
              </span>
            </label>

            <label className="flex items-center gap-2 text-sm font-medium">
              <input
                type="checkbox"
                checked={model.requiresAttachment}
                disabled={busy}
                onChange={event => setModel(current => ({
                  ...current,
                  requiresAttachment: event.target.checked,
                  maxAttachments: event.target.checked ? Math.max(1, current.maxAttachments) : current.maxAttachments,
                }))}
              />
              Solicită cel puțin un atașament
            </label>

            <label className="text-sm font-medium">
              Număr maxim de atașamente
              <input
                className={inputClass}
                type="number"
                min="0"
                value={model.maxAttachments}
                disabled={busy}
                onChange={event => setModel(current => ({
                  ...current,
                  maxAttachments: Math.max(0, Number(event.target.value) || 0),
                }))}
              />
            </label>

            <label className="text-sm font-medium">
              Ordine de afișare
              <input
                className={inputClass}
                type="number"
                value={model.displayOrder}
                disabled={busy}
                onChange={event => setModel(current => ({
                  ...current,
                  displayOrder: Number(event.target.value) || 0,
                }))}
              />
            </label>
          </div>

          <div className="space-y-4 border-t border-slate-200 pt-6">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h3 className="text-lg font-semibold">Schema formularului</h3>
                <p className="text-sm text-slate-600">
                  Construiește secțiunile și câmpurile afișate cetățeanului.
                </p>
              </div>
              <button type="button" disabled={busy} className={secondaryButtonClass} onClick={addSection}>
                Adaugă secțiune
              </button>
            </div>

            {model.formSchema.sections.map((section, sectionIndex) => (
              <fieldset key={section.key} className="space-y-4 rounded-lg border border-slate-300 p-4">
                <div className="flex flex-wrap items-end gap-3">
                  <label className="min-w-56 flex-1 text-sm font-medium">
                    Titlu secțiune
                    <input
                      className={inputClass}
                      value={section.title}
                      disabled={busy}
                      onChange={event => updateSectionTitle(sectionIndex, event.target.value)}
                    />
                  </label>
                  <span className="pb-2 text-xs text-slate-500">cheie: {section.key}</span>
                  <button
                    type="button"
                    disabled={busy || sectionIndex === 0}
                    className={secondaryButtonClass}
                    onClick={() => moveSection(sectionIndex, -1)}
                  >
                    Sus
                  </button>
                  <button
                    type="button"
                    disabled={busy || sectionIndex === model.formSchema.sections.length - 1}
                    className={secondaryButtonClass}
                    onClick={() => moveSection(sectionIndex, 1)}
                  >
                    Jos
                  </button>
                  <button
                    type="button"
                    disabled={busy}
                    className={destructiveButtonClass}
                    onClick={() => removeSection(sectionIndex)}
                  >
                    Șterge secțiunea
                  </button>
                </div>

                <div className="space-y-3">
                  {section.fields.map((field, fieldIndex) => (
                    <div key={`${sectionIndex}-${fieldIndex}`} className="space-y-3 rounded-md bg-slate-50 p-3">
                      <div className="grid gap-3 md:grid-cols-4">
                        <label className="text-sm font-medium">
                          Cheie
                          <input
                            className={inputClass}
                            value={field.key}
                            disabled={busy}
                            onChange={event => updateField(sectionIndex, fieldIndex, { key: event.target.value })}
                          />
                        </label>

                        <label className="text-sm font-medium md:col-span-2">
                          Etichetă
                          <input
                            className={inputClass}
                            value={field.label}
                            disabled={busy}
                            onChange={event => updateField(sectionIndex, fieldIndex, { label: event.target.value })}
                          />
                        </label>

                        <label className="text-sm font-medium">
                          Tip
                          <select
                            className={inputClass}
                            value={field.type}
                            disabled={busy}
                            onChange={event => {
                              const type = event.target.value as FieldType;
                              updateField(sectionIndex, fieldIndex, {
                                type,
                                options: optionFieldTypes.has(type)
                                  ? field.options?.length
                                    ? field.options
                                    : [{ value: "optiune1", label: "Opțiunea 1" }]
                                  : undefined,
                              });
                            }}
                          >
                            {fieldTypes.map(type => <option key={type}>{type}</option>)}
                          </select>
                        </label>
                      </div>

                      <div className="flex flex-wrap items-end gap-3">
                        <label className="flex items-center gap-2 text-sm font-medium">
                          <input
                            type="checkbox"
                            checked={field.required}
                            disabled={busy}
                            onChange={event => updateField(sectionIndex, fieldIndex, { required: event.target.checked })}
                          />
                          Obligatoriu
                        </label>

                        {(field.type === "text" || field.type === "textarea") && (
                          <label className="text-sm font-medium">
                            Lungime maximă
                            <input
                              className={inputClass}
                              type="number"
                              min="1"
                              value={field.maxLength ?? ""}
                              disabled={busy}
                              onChange={event => updateField(sectionIndex, fieldIndex, {
                                maxLength: event.target.value ? Number(event.target.value) : undefined,
                              })}
                            />
                          </label>
                        )}

                        <label className="text-sm font-medium">
                          Vizibil când
                          <select
                            className={inputClass}
                            value={field.visibleWhen?.field ?? ""}
                            disabled={busy}
                            onChange={event => updateField(sectionIndex, fieldIndex, {
                              visibleWhen: event.target.value
                                ? { field: event.target.value, equals: field.visibleWhen?.equals ?? "" }
                                : undefined,
                            })}
                          >
                            <option value="">Întotdeauna</option>
                            {allFieldKeys.filter(key => key !== field.key).map(key => (
                              <option key={key} value={key}>{key}</option>
                            ))}
                          </select>
                        </label>

                        {field.visibleWhen && (
                          <label className="text-sm font-medium">
                            are valoarea
                            <input
                              className={inputClass}
                              value={field.visibleWhen.equals}
                              disabled={busy}
                              onChange={event => updateField(sectionIndex, fieldIndex, {
                                visibleWhen: { ...field.visibleWhen!, equals: event.target.value },
                              })}
                            />
                          </label>
                        )}
                      </div>

                      {optionFieldTypes.has(field.type) && (
                        <div className="space-y-2 rounded border border-slate-200 bg-white p-3">
                          <p className="text-sm font-medium">Opțiuni</p>
                          {(field.options ?? []).map((option, optionIndex) => (
                            <div key={`${field.key}-${optionIndex}`} className="flex flex-wrap gap-2">
                              <input
                                className="rounded border border-slate-300 px-2 py-1 text-sm"
                                value={option.value}
                                aria-label="Valoare opțiune"
                                disabled={busy}
                                onChange={event => updateField(sectionIndex, fieldIndex, {
                                  options: (field.options ?? []).map((current, index) =>
                                    index === optionIndex ? { ...current, value: event.target.value } : current,
                                  ),
                                })}
                              />
                              <input
                                className="min-w-48 flex-1 rounded border border-slate-300 px-2 py-1 text-sm"
                                value={option.label}
                                aria-label="Etichetă opțiune"
                                disabled={busy}
                                onChange={event => updateField(sectionIndex, fieldIndex, {
                                  options: (field.options ?? []).map((current, index) =>
                                    index === optionIndex ? { ...current, label: event.target.value } : current,
                                  ),
                                })}
                              />
                              <button
                                type="button"
                                className={secondaryButtonClass}
                                disabled={busy}
                                onClick={() => updateField(sectionIndex, fieldIndex, {
                                  options: (field.options ?? []).filter((_, index) => index !== optionIndex),
                                })}
                              >
                                Elimină
                              </button>
                            </div>
                          ))}
                          <button
                            type="button"
                            className={secondaryButtonClass}
                            disabled={busy}
                            onClick={() => updateField(sectionIndex, fieldIndex, {
                              options: [
                                ...(field.options ?? []),
                                { value: `optiune${(field.options?.length ?? 0) + 1}`, label: "Opțiune nouă" },
                              ],
                            })}
                          >
                            Adaugă opțiune
                          </button>
                        </div>
                      )}

                      <div className="flex flex-wrap gap-2">
                        <button type="button" className={secondaryButtonClass} disabled={busy || fieldIndex === 0} onClick={() => moveField(sectionIndex, fieldIndex, -1)}>
                          Sus
                        </button>
                        <button type="button" className={secondaryButtonClass} disabled={busy || fieldIndex === section.fields.length - 1} onClick={() => moveField(sectionIndex, fieldIndex, 1)}>
                          Jos
                        </button>
                        <button type="button" className={destructiveButtonClass} disabled={busy} onClick={() => removeField(sectionIndex, fieldIndex)}>
                          Șterge câmpul
                        </button>
                      </div>
                    </div>
                  ))}
                </div>

                <button type="button" disabled={busy} className={secondaryButtonClass} onClick={() => addField(sectionIndex)}>
                  Adaugă câmp
                </button>
              </fieldset>
            ))}
          </div>

          <FormPreview schema={model.formSchema} />

          <div className="flex flex-wrap gap-3 border-t border-slate-200 pt-5">
            <button type="button" className={buttonClass} disabled={busy} onClick={() => void save()}>
              {busy ? "Se salvează…" : "Salvează"}
            </button>

            {loaded && (
              <>
                <button
                  type="button"
                  className={secondaryButtonClass}
                  disabled={busy}
                  onClick={() => void changePublication(!loaded.isPublished)}
                >
                  {loaded.isPublished ? "Retrage din publicare" : "Publică"}
                </button>
                <button type="button" className={destructiveButtonClass} disabled={busy} onClick={() => void deleteService()}>
                  Șterge serviciul
                </button>
              </>
            )}
          </div>
        </>
      )}
    </section>
  );
}

export function ServicesPage() {
  const auth = useAuth();
  const [services, setServices] = useState<ServiceDefinitionListItem[]>([]);
  const [selectedCode, setSelectedCode] = useState<string | null>(null);
  const [listError, setListError] = useState<ApiError | null>(null);
  const [revision, setRevision] = useState(0);

  useEffect(() => {
    if (auth.status !== "authenticated") return;

    const controller = new AbortController();
    void apiRequest<PagedResponse<ServiceDefinitionListItem>>(
      "/api/service-definitions?page=1&pageSize=100&sort=displayOrder:asc",
      { signal: controller.signal },
    )
      .then(result => {
        setServices(result.items);
        setListError(null);
      })
      .catch(failure => {
        if (!controller.signal.aborted) setListError(asApiError(failure));
      })
    return () => controller.abort();
  }, [auth.status, revision]);

  if (auth.status === "loading") {
    return <main className="p-6" role="status">Verificăm sesiunea…</main>;
  }

  if (auth.status !== "authenticated") {
    return (
      <main className="p-6">
        <p>Autentifică-te ca administrator pentru a administra serviciile.</p>
      </main>
    );
  }

  return (
    <main className="min-h-screen bg-slate-100 p-4 text-slate-950 sm:p-6">
      <div className="mx-auto max-w-7xl space-y-6">
        <header>
          <p className="text-sm font-semibold uppercase tracking-wider text-blue-700">Back-office</p>
          <h1 className="text-3xl font-semibold">Definiții servicii</h1>
          <p className="mt-2 max-w-3xl text-slate-600">
            Configurează formularul, atașamentele și publicarea serviciilor disponibile în portalul public.
          </p>
        </header>

        <ErrorMessage error={listError} />

        <div className="grid gap-6 lg:grid-cols-[19rem_minmax(0,1fr)]">
          <aside className="space-y-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm">
            <button type="button" className={`${buttonClass} w-full`} onClick={() => setSelectedCode(null)}>
              Serviciu nou
            </button>

            {services.length === 0 && (
              <p className="text-sm text-slate-600">Nu există încă servicii configurate.</p>
            )}

            <nav aria-label="Servicii configurate" className="space-y-2">
              {services.map(service => (
                <button
                  type="button"
                  key={service.code}
                  onClick={() => setSelectedCode(service.code)}
                  className={
                    "w-full rounded-md border p-3 text-left " +
                    (selectedCode === service.code
                      ? "border-blue-700 bg-blue-50"
                      : "border-slate-200 hover:bg-slate-50")
                  }
                >
                  <span className="block font-medium">{service.title}</span>
                  <span className="block text-sm text-slate-600">{service.code}</span>
                  <span className="mt-1 block text-xs text-slate-500">
                    {service.isPublished ? "Publicat" : "Ciornă"} · v{service.schemaVersion}
                  </span>
                </button>
              ))}
            </nav>
          </aside>

          <ServiceEditor
            key={selectedCode ?? "new"}
            code={selectedCode}
            onSaved={service => {
              setSelectedCode(service.code);
              setRevision(current => current + 1);
            }}
            onDeleted={() => {
              setSelectedCode(null);
              setRevision(current => current + 1);
            }}
          />
        </div>
      </div>
    </main>
  );
}
