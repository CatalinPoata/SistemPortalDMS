export type ReportSummary = {
  id: string;
  code: string;
  name: string;
  datasetKey: string;
  version: number;
  isSystem: boolean;
  createdAt: string;
  updatedAt: string | null;
};

export type ReportDetails = ReportSummary & {
  definition: ReportDefinition;
};

export type ReportDatasetField = {
  key: string;
  label: string;
  type: "number" | "date" | "code" | "text";
  isNumeric: boolean;
};

export type ReportDatasetParameter = {
  name: string;
  label: string;
  type: "date" | "int" | "string" | "bool" | "lookup";
  source: string | null;
};

export type ReportDataset = {
  key: string;
  label: string;
  fields: ReportDatasetField[];
  parameters: ReportDatasetParameter[];
};

export type ReportParameter = ReportDatasetParameter & { required: boolean };

export type ReportColumn = {
  field: string;
  label: string;
  type: ReportDatasetField["type"];
  align?: "left" | "center" | "right";
  widthPct: number;
  format?: string;
};

export type ReportSort = { field: string; dir: "asc" | "desc" };

export type ReportTotal = {
  field: string;
  agg: "count" | "sum" | "avg";
};

export type ReportDefinition = {
  renderMode: "table" | "record";
  datasetKey: string;
  parameters: ReportParameter[];
  columns: ReportColumn[];
  sort: ReportSort[];
  groupBy: { field: string } | null;
  totals: ReportTotal[];
  layout: {
    orientation: "portrait" | "landscape";
    title: string;
    subtitle?: string;
    showPageNumbers: boolean;
  };
};

export type ReportPreview = {
  columns: Array<{
    field: string;
    label: string;
    type: string;
    align: "left" | "center" | "right";
    widthPct: number;
    format: string | null;
  }>;
  rows: Array<Record<string, unknown>>;
  totals: Record<string, number>;
  meta: { page: number; pageSize: number; total: number };
};

function defaultAlign(type: ReportDatasetField["type"]): ReportColumn["align"] {
  return type === "number" || type === "date" || type === "code"
    ? "center"
    : "left";
}

export function createDefaultDefinition(dataset: ReportDataset): ReportDefinition {
  const fields = dataset.fields.slice(0, Math.min(4, dataset.fields.length));
  const baseWidth = Math.floor(100 / Math.max(fields.length, 1));

  return {
    renderMode: "table",
    datasetKey: dataset.key,
    parameters: [],
    columns: fields.map((field, index) => ({
      field: field.key,
      label: field.label,
      type: field.type,
      align: defaultAlign(field.type),
      widthPct: index === fields.length - 1
        ? 100 - baseWidth * (fields.length - 1)
        : baseWidth,
    })),
    sort: [],
    groupBy: null,
    totals: [],
    layout: {
      orientation: "portrait",
      title: "Raport nou",
      subtitle: "",
      showPageNumbers: true,
    },
  };
}

export function createQueryString(values: Record<string, string>) {
  const query = new URLSearchParams();

  for (const [key, value] of Object.entries(values)) {
    if (value.trim()) query.set(key, value.trim());
  }

  return query.toString();
}

export function formatPreviewValue(value: unknown) {
  if (value === null || value === undefined || value === "") return "—";
  if (typeof value === "number") return new Intl.NumberFormat("ro-RO").format(value);
  if (typeof value === "boolean") return value ? "Da" : "Nu";
  return String(value);
}
