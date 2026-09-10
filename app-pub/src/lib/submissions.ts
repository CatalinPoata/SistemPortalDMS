import "client-only";

import type { ServiceFormSchema } from "@/lib/public-services";
import { authenticatedFetch, authenticatedRequest } from "@/lib/auth-store";

export type SubmissionStatus =
  | "Submitted"
  | "Registered"
  | "InReview"
  | "InfoRequested"
  | "Completed"
  | "Rejected"
  | "Cancelled";

export type SubmissionListItem = {
  id: string;
  externalId: string;
  serviceCode: string;
  serviceTitle: string;
  status: SubmissionStatus;
  submittedAt: string;
  registryDisplayNumber: string | null;
};

export type SubmissionEvent = {
  id: string;
  occurredAt: string;
  type: string;
  message: string;
  fileId: string | null;
};

export type SubmissionDetails = SubmissionListItem & {
  schemaVersion: number;
  formSnapshot: ServiceFormSchema;
  values: Record<string, unknown>;
  statusDetails: string | null;
  registryNumber: number | null;
  registryYear: number | null;
  registeredAt: string | null;
  files: SubmissionFile[];
  events: SubmissionEvent[];
};

export type PagedSubmissions = {
  items: SubmissionListItem[];
  page: number;
  pageSize: number;
  total: number;
};

export type SubmissionFile = {
  id: string;
  fieldKey: string | null;
  kind: "Application" | "Attachment" | "Response";
  originalName: string;
  contentType: string;
  sizeBytes: number;
  sha256: string;
};

export type SubmissionUpload = {
  file: File;
  fieldKey: string | null;
};

export type SubmissionFileDownloadUrl = {
  fileId: string;
  url: string;
  expiresAt: string;
};

export function createSubmission(
  serviceCode: string,
  values: Record<string, unknown>,
  uploads: SubmissionUpload[] = [],
) {
  if (uploads.length > 0) {
    const body = new FormData();
    body.append("ServiceCode", serviceCode);
    body.append("Values", JSON.stringify(values));

    for (const upload of uploads) {
      body.append("Files", upload.file, upload.file.name);
      body.append(
        "FileKeys",
        upload.fieldKey ?? "__attachment__",
      );
    }

    return authenticatedRequest<SubmissionDetails>("/api/submissions/with-files", {
      method: "POST",
      body,
    });
  }

  return authenticatedRequest<SubmissionDetails>("/api/submissions", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ serviceCode, values }),
  });
}

export function getMySubmissions(page = 1, pageSize = 10) {
  const query = new URLSearchParams({
    page: String(Math.max(1, page)),
    pageSize: String(Math.max(1, pageSize)),
  });

  return authenticatedRequest<PagedSubmissions>(
    `/api/submissions?${query.toString()}`,
  );
}

export function getSubmission(id: string) {
  return authenticatedRequest<SubmissionDetails>(
    `/api/submissions/${encodeURIComponent(id)}`,
  );
}

export function withdrawSubmission(id: string, reason: string) {
  return authenticatedRequest<SubmissionDetails>(
    `/api/submissions/${encodeURIComponent(id)}/withdraw`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ reason }),
    },
  );
}

export function uploadClarifications(
  submissionId: string,
  files: File[],
) {
  const body = new FormData();

  for (const file of files) {
    body.append("Files", file, file.name);
  }

  return authenticatedRequest<SubmissionDetails>(
    `/api/submissions/${encodeURIComponent(submissionId)}/clarifications`,
    {
      method: "POST",
      body,
    },
  );
}

export function createSubmissionFileDownloadUrl(
  submissionId: string,
  fileId: string,
) {
  return authenticatedRequest<SubmissionFileDownloadUrl>(
    `/api/submissions/${encodeURIComponent(submissionId)}` +
      `/files/${encodeURIComponent(fileId)}/download-url`,
    { method: "POST" },
  );
}

export async function downloadRegistrationReceipt(submissionId: string) {
  const response = await authenticatedFetch(
    `/api/submissions/${encodeURIComponent(submissionId)}/registration-receipt`,
  );
  const disposition = response.headers.get("Content-Disposition") ?? "";
  const matchedName = /filename\*?=(?:UTF-8''|\")?([^;\"]+)/i.exec(disposition);

  return {
    blob: await response.blob(),
    fileName: matchedName
      ? decodeURIComponent(matchedName[1].replace(/^\"|\"$/g, ""))
      : "dovada-inregistrare.pdf",
  };
}
