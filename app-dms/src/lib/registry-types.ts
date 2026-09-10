export const statusLabels = {
  Submitted: "Depusă",
  Registered: "Înregistrată",
  InReview: "În lucru",
  InfoRequested: "Clarificări solicitate",
  Completed: "Finalizată",
  Rejected: "Respinsă",
  Cancelled: "Anulată",
} as const;

export type RegistryType = {
  id: string;
  code: string;
  name: string;
  direction: "In" | "Out" | "Both";
  startNumber: number;
  defaultDeadlineDays: number;
  isClosed: boolean;
};

export type DocumentKind = {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
};

export type Department = {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
};

export type RegistryEntry = {
  id: string;
  registryTypeCode: string;
  registryTypeName: string;
  displayNumber: string;
  direction: "In" | "Out";
  registeredAt: string;
  deadline: string;
  subject: string;
  applicantName: string;
  status: keyof typeof statusLabels;
};

export type PagedResponse<T> = {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
};
