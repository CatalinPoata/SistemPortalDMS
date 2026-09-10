export const fieldTypes = [
  "text",
  "textarea",
  "number",
  "date",
  "boolean",
  "select",
  "radio",
  "checkboxes",
  "email",
  "phone",
  "nationalId",
  "file",
  "heading",
  "paragraph",
] as const;

export type FieldType = (typeof fieldTypes)[number];

export type FormOption = {
  value: string;
  label: string;
};

export type VisibleWhen = {
  field: string;
  equals: string;
};

export type FormField = {
  key: string;
  label: string;
  type: FieldType;
  required: boolean;
  maxLength?: number;
  options?: FormOption[];
  visibleWhen?: VisibleWhen;
};

export type FormSection = {
  key: string;
  title: string;
  fields: FormField[];
};

export type ServiceFormSchema = {
  sections: FormSection[];
};

export type ServiceDefinitionListItem = {
  code: string;
  title: string;
  shortDescription: string | null;
  registryTypeCode: string;
  schemaVersion: number;
  requiresAttachment: boolean;
  maxAttachments: number;
  isPublished: boolean;
  displayOrder: number;
  updatedAt: string | null;
};

export type ServiceDefinitionDetails = ServiceDefinitionListItem & {
  description: string | null;
  formSchema: ServiceFormSchema;
  submissionCount: number;
};

export type PagedResponse<T> = {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
};

export type ServiceDefinitionWrite = {
  code: string;
  title: string;
  shortDescription: string;
  description: string;
  registryTypeCode: string;
  formSchema: ServiceFormSchema;
  requiresAttachment: boolean;
  maxAttachments: number;
  displayOrder: number;
};

export function emptyFormSchema(): ServiceFormSchema {
  return {
    sections: [
      {
        key: "date_solicitant",
        title: "Date solicitant",
        fields: [
          {
            key: "nume",
            label: "Nume complet",
            type: "text",
            required: true,
            maxLength: 200,
          },
        ],
      },
    ],
  };
}

export function emptyServiceDefinition(): ServiceDefinitionWrite {
  return {
    code: "",
    title: "",
    shortDescription: "",
    description: "",
    registryTypeCode: "",
    formSchema: emptyFormSchema(),
    requiresAttachment: false,
    maxAttachments: 0,
    displayOrder: 0,
  };
}

export function toWriteModel(
  service: ServiceDefinitionDetails,
): ServiceDefinitionWrite {
  return {
    code: service.code,
    title: service.title,
    shortDescription: service.shortDescription ?? "",
    description: service.description ?? "",
    registryTypeCode: service.registryTypeCode,
    formSchema: service.formSchema,
    requiresAttachment: service.requiresAttachment,
    maxAttachments: service.maxAttachments,
    displayOrder: service.displayOrder,
  };
}
