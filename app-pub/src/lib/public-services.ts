import "client-only";

import { request } from "@/lib/http";

export type FieldOption = {
  value: string;
  label: string;
};

export type VisibilityCondition = {
  field: string;
  equals: string | number | boolean | null;
};

export type ServiceFormField = {
  key: string;
  label: string;
  type:
    | "text"
    | "textarea"
    | "number"
    | "date"
    | "boolean"
    | "select"
    | "radio"
    | "checkboxes"
    | "email"
    | "phone"
    | "nationalId"
    | "file"
    | "heading"
    | "paragraph";
  required: boolean;
  minLength?: number;
  maxLength?: number;
  pattern?: string;
  min?: number;
  max?: number;
  integer?: boolean;
  minDate?: string;
  maxDate?: string;
  notInFuture?: boolean;
  options?: FieldOption[];
  minSelected?: number;
  maxSelected?: number;
  accept?: string[] | string;
  maxSizeMb?: number;
  placeholder?: string;
  helpText?: string;
  defaultValue?: unknown;
  visibleWhen?: VisibilityCondition | null;
};

export type ServiceFormSection = {
  key: string;
  title: string;
  fields: ServiceFormField[];
};

export type ServiceFormSchema = {
  sections: ServiceFormSection[];
};

export type PublicServiceListItem = {
  id: string;
  code: string;
  title: string;
  shortDescription: string | null;
  requiresAttachment: boolean;
  maxAttachments: number;
};

export type PublicServiceDetails = PublicServiceListItem & {
  description: string | null;
  formSchema: ServiceFormSchema;
  schemaVersion: number;
};

export type PagedResponse<T> = {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
};

export function getPublicServices() {
  return request<PagedResponse<PublicServiceListItem>>(
    "/api/public/services?page=1&pageSize=100",
  );
}

export function getPublicService(code: string) {
  return request<PublicServiceDetails>(
    `/api/public/services/${encodeURIComponent(code)}`,
  );
}
