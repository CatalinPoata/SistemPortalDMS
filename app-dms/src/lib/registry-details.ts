import type { RegistryEntry } from "@/lib/registry-types";

export type JsonValue =
  | string
  | number
  | boolean
  | null
  | JsonValue[]
  | { [key: string]: JsonValue };

export const taskStatusLabels = {
  Open: "Deschisă",
  Done: "Rezolvată",
  Cancelled: "Anulată",
} as const;

export const eventTypeLabels = {
  Created: "Creare",
  StatusChanged: "Schimbare de stare",
  Assigned: "Repartizare",
  DocumentAdded: "Document adăugat",
  TaskCompleted: "Sarcină rezolvată",
} as const;

export type RegistryDocumentDetails = {
  id: string;
  externalFileId: string | null;
  direction: "In" | "Out";
  documentKindId: string;
  documentKindCode: string;
  documentKindName: string;
  documentDate: string | null;
  issuer: string | null;
  note: string | null;
  originalName: string;
  contentType: string;
  sizeBytes: number;
  sha256: string;
  uploadedByUserId: string;
};

export type RegistryTaskDetails = {
  id: string;
  assigneeUserId: string | null;
  assigneeEmail: string | null;
  departmentId: string | null;
  departmentCode: string | null;
  departmentName: string | null;
  title: string;
  instructions: string | null;
  dueDate: string | null;
  status: keyof typeof taskStatusLabels;
  resolutionNote: string | null;
  completedAt: string | null;
  createdByUserId: string;
};

export type RegistryEventDetails = {
  id: string;
  occurredAt: string;
  actorUserId: string | null;
  type: keyof typeof eventTypeLabels;
  message: string;
  payload: JsonValue;
};

export type RegistryEntryDetails = RegistryEntry & {
  externalId: string | null;
  registryTypeId: string;
  year: number;
  number: number;
  submittedAt: string | null;
  applicantNationalId: string | null;
  applicantEmail: string | null;
  applicantPhone: string | null;
  applicantAddress: string | null;
  sourceDocNumber: string | null;
  sourceDocDate: string | null;
  departmentId: string | null;
  departmentCode: string | null;
  departmentName: string | null;
  serviceCode: string | null;
  formValues: JsonValue;
  statusNote: string | null;
  createdByUserId: string;
  documents: RegistryDocumentDetails[];
  tasks: RegistryTaskDetails[];
  events: RegistryEventDetails[];
};
