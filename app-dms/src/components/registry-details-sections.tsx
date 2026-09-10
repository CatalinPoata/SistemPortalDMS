import type { ReactNode } from "react";
import { statusLabels } from "@/lib/registry-types";
import {
  eventTypeLabels,
  taskStatusLabels,
  type JsonValue,
  type RegistryDocumentDetails,
  type RegistryEntryDetails,
} from "@/lib/registry-details";

import {
  DocumentDownloadButton,
  DocumentUploadPanel,
} from "@/components/registry-document-actions";

import RegistryWorkflowActions from "@/components/registry-workflow-actions";
import {
  RegistryTaskAssignmentPanel,
  TaskCompletionForm,
} from "@/components/registry-task-actions";

const utcFormatter = new Intl.DateTimeFormat("ro-RO", {
  dateStyle: "short",
  timeStyle: "medium",
  timeZone: "UTC",
});

function dateTime(value: string | null) {
  if (!value) return "—";
  return `${utcFormatter.format(new Date(value))} UTC`;
}

function dateOnly(value: string | null) {
  if (!value) return "—";
  const [year, month, day] = value.split("-");
  return `${day}.${month}.${year}`;
}

function Panel({ title, children }: {
  title: string;
  children: ReactNode;
}) {
  return (
    <section className="space-y-4 rounded-xl bg-white p-5 shadow-sm">
      <h2 className="text-xl font-semibold">{title}</h2>
      {children}
    </section>
  );
}

function Fields({ rows }: {
  rows: ReadonlyArray<readonly [string, string | number | null]>;
}) {
  return (
    <dl className="grid gap-4 sm:grid-cols-2">
      {rows.map(([label, value]) => (
        <div key={label} className="min-w-0">
          <dt className="text-sm text-slate-600">{label}</dt>
          <dd className="whitespace-pre-wrap break-words">
            {value === null || value === "" ? "—" : value}
          </dd>
        </div>
      ))}
    </dl>
  );
}

function JsonText({ value }: { value: JsonValue }) {
  return (
    <pre className="max-h-96 overflow-auto whitespace-pre-wrap break-words rounded-lg bg-slate-100 p-3 text-sm">
      {JSON.stringify(value, null, 2)}
    </pre>
  );
}

function Documents({
  entryId,
  title,
  documents,
}: {
  entryId: string;
  title: string;
  documents: RegistryDocumentDetails[];
}) {
  return (
    <Panel title={title}>
      {documents.length === 0 ? (
        <p>Nu există documente în această categorie.</p>
      ) : (
        <ul className="space-y-4">
          {documents.map(document => (
            <li
              key={document.id}
              className="space-y-3 rounded-lg border border-slate-200 p-4"
            >
              <h3 className="break-words font-semibold">
                {document.originalName}
              </h3>

              <Fields rows={[
                [
                  "Tip document",
                  `${document.documentKindCode} — ${document.documentKindName}`,
                ],
                ["Data documentului", dateOnly(document.documentDate)],
                ["Emitent", document.issuer],
                ["Observații", document.note],
                ["Format", document.contentType],
                [
                  "Dimensiune",
                  `${document.sizeBytes.toLocaleString("ro-RO")} bytes`,
                ],
              ]} />
              <DocumentDownloadButton
                entryId={entryId}
                document={document}
                />
            </li>
          ))}
        </ul>
      )}
    </Panel>
  );
}

export default function RegistryDetailsSections({
  entry,
  onEntryChanged,
}: {
  entry: RegistryEntryDetails;
  onEntryChanged: (message: string) => void;
}) {
  const events = [...entry.events].sort((a, b) =>
    Date.parse(a.occurredAt) - Date.parse(b.occurredAt)
    || a.id.localeCompare(b.id),
  );

  return (
    <>
      <Panel title={`${entry.registryTypeCode} · ${entry.displayNumber}`}>
        <Fields rows={[
          ["Registru", entry.registryTypeName],
          ["Sens", entry.direction === "In" ? "Intrare" : "Ieșire"],
          ["Obiect", entry.subject],
          ["Stare", statusLabels[entry.status] ?? entry.status],
          ["Înregistrat la", dateTime(entry.registeredAt)],
          ["Depus la", dateTime(entry.submittedAt)],
          ["Termen", dateOnly(entry.deadline)],
          ["Compartiment", entry.departmentName],
          ["Cod compartiment", entry.departmentCode],
          ["Cod serviciu", entry.serviceCode],
          ["Număr document sursă", entry.sourceDocNumber],
          ["Data documentului sursă", dateOnly(entry.sourceDocDate)],
          ["Observații privind starea", entry.statusNote],
        ]} />
      </Panel>
      <RegistryWorkflowActions
        entry={entry}
        onEntryChanged={onEntryChanged}
        />
      

      <Panel title="Solicitant">
        <Fields rows={[
          ["Nume", entry.applicantName],
          ["CNP / identificator", entry.applicantNationalId],
          ["Email", entry.applicantEmail],
          ["Telefon", entry.applicantPhone],
          ["Adresă", entry.applicantAddress],
        ]} />
      </Panel>

      <Panel title="Valorile formularului">
        {entry.formValues === null ? (
          <p>Nu există valori de formular pentru această poziție.</p>
        ) : (
          <JsonText value={entry.formValues} />
        )}
      </Panel>

      <DocumentUploadPanel
        entryId={entry.id}
        onDocumentUploaded={() =>
            onEntryChanged("Documentul a fost încărcat.")
        }
        />

      <Documents
        entryId={entry.id}
        title="Documente de intrare"
        documents={entry.documents.filter(
            document => document.direction === "In",
        )}
        />

      <Documents
            entryId={entry.id}
            title="Documente de ieșire"
            documents={entry.documents.filter(
                document => document.direction === "Out",
            )}
        />

        <RegistryTaskAssignmentPanel
            entryId={entry.id}
            entryStatus={entry.status}
            onEntryChanged={onEntryChanged}
            />

      <Panel title="Repartizări și sarcini">
        {entry.tasks.length === 0 ? (
          <p>Nu există repartizări.</p>
        ) : (
          <ul className="space-y-4">
            {entry.tasks.map(task => (
              <li
                key={task.id}
                className="space-y-3 rounded-lg border border-slate-200 p-4"
              >
                <h3 className="break-words font-semibold">
                  {task.title}
                </h3>

                <Fields rows={[
                  ["Responsabil", task.assigneeEmail ?? task.assigneeUserId],
                  ["Compartiment", task.departmentName],
                  ["Cod compartiment", task.departmentCode],
                  ["Instrucțiuni", task.instructions],
                  ["Termen", dateOnly(task.dueDate)],
                  ["Stare", taskStatusLabels[task.status] ?? task.status],
                  ["Notă de rezolvare", task.resolutionNote],
                  ["Rezolvată la", dateTime(task.completedAt)],
                ]} />

                {task.status === "Open" && (
                <TaskCompletionForm
                    entryId={entry.id}
                    task={task}
                    onEntryChanged={onEntryChanged}
                />
                )}
              </li>
            ))}
          </ul>
        )}
      </Panel>


      <Panel title="Cronologie">
        {events.length === 0 ? (
          <p>Nu există evenimente.</p>
        ) : (
          <ol className="space-y-4">
            {events.map(event => (
              <li
                key={event.id}
                className="space-y-2 border-l-2 border-blue-700 pl-4"
              >
                <p className="font-semibold">
                  {eventTypeLabels[event.type] ?? event.type}
                </p>

                <time
                  dateTime={event.occurredAt}
                  className="text-sm text-slate-600"
                >
                  {dateTime(event.occurredAt)}
                </time>

                <p className="whitespace-pre-wrap break-words">
                  {event.message}
                </p>

                <p className="break-all text-sm text-slate-600">
                  Autor: {event.actorUserId ?? "Sistem"}
                </p>

                <details>
                  <summary className="cursor-pointer text-blue-700">
                    Datele evenimentului
                  </summary>
                  <JsonText value={event.payload} />
                </details>
              </li>
            ))}
          </ol>
        )}
      </Panel>
    </>
  );
}
