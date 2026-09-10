import "client-only";

import { authenticatedRequest } from "@/lib/auth-store";
import { request } from "@/lib/http";

export type SurveyQuestionType = "SingleChoice" | "MultiChoice" | "Rating" | "FreeText";

export type SurveyQuestion = {
  id: string;
  key: string;
  text: string;
  type: SurveyQuestionType;
  options: unknown | null;
  isRequired: boolean;
  displayOrder: number;
};

export type Survey = {
  id: string;
  code: string;
  title: string;
  description: string | null;
  startsAt: string | null;
  endsAt: string | null;
  allowAnonymous: boolean;
  showResults: boolean;
  isPublished: boolean;
  questions: SurveyQuestion[];
  hasResponded: boolean;
};

export type SurveyResult = {
  key: string;
  type: SurveyQuestionType;
  responseCount: number;
  valueCounts: Record<string, number>;
};

export type SurveySubmission = {
  id: string;
  code: string;
  showResults: boolean;
  submittedAt: string;
  results: { totalResponses: number; items: SurveyResult[] } | null;
};

type Paged<T> = { items: T[]; page: number; pageSize: number; total: number };
type SurveyListItem = Omit<Survey, "questions" | "hasResponded"> & {
  questionCount: number;
  responseCount: number;
};

export function getPublicSurveys() {
  return request<Paged<SurveyListItem>>("/api/public/surveys?page=1&pageSize=100");
}

export function getPublicSurvey(code: string) {
  return request<Survey>(`/api/public/surveys/${encodeURIComponent(code)}`);
}

export function submitSurvey(
  code: string,
  answers: Record<string, unknown>,
  authenticated: boolean,
) {
  const init = {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ answers }),
  };

  return authenticated
    ? authenticatedRequest<SurveySubmission>(
        `/api/public/surveys/${encodeURIComponent(code)}/responses`,
        init,
      )
    : request<SurveySubmission>(
        `/api/public/surveys/${encodeURIComponent(code)}/responses`,
        init,
      );
}
