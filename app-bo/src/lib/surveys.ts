import "client-only";

import { apiRequest } from "@/lib/auth-store";

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

export type SurveyListItem = Omit<Survey, "questions" | "hasResponded"> & {
  questionCount: number;
  responseCount: number;
};

export type SurveyWrite = {
  code: string;
  title: string;
  description: string;
  startsAt: string;
  endsAt: string;
  allowAnonymous: boolean;
  showResults: boolean;
  isPublished: boolean;
};

export type QuestionWrite = {
  key: string;
  text: string;
  type: SurveyQuestionType;
  options: string;
  isRequired: boolean;
  displayOrder: number;
};

export type SurveyResults = {
  surveyId: string;
  code: string;
  totalResponses: number;
  items: Array<{
    key: string;
    type: SurveyQuestionType;
    responseCount: number;
    valueCounts: Record<string, number>;
  }>;
  responses: Array<{ id: string; submittedAt: string; answers: unknown }>;
};

type Paged<T> = { items: T[]; page: number; pageSize: number; total: number };

export function emptySurvey(): SurveyWrite {
  return {
    code: "",
    title: "",
    description: "",
    startsAt: "",
    endsAt: "",
    allowAnonymous: false,
    showResults: false,
    isPublished: false,
  };
}

export function emptyQuestion(order = 0): QuestionWrite {
  return {
    key: "",
    text: "",
    type: "FreeText",
    options: "",
    isRequired: false,
    displayOrder: order,
  };
}

export function getSurveys() {
  return apiRequest<Paged<SurveyListItem>>("/api/surveys?page=1&pageSize=100");
}

export function getSurvey(code: string) {
  return apiRequest<Survey>(`/api/surveys/${encodeURIComponent(code)}`);
}

export function createSurvey(survey: SurveyWrite) {
  return apiRequest<Survey>("/api/surveys", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(toSurveyPayload(survey)) });
}

export function updateSurvey(code: string, survey: SurveyWrite) {
  return apiRequest<Survey>(`/api/surveys/${encodeURIComponent(code)}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ ...toSurveyPayload(survey), isPublished: survey.isPublished }) });
}

export function deleteSurvey(code: string) {
  return apiRequest<void>(`/api/surveys/${encodeURIComponent(code)}`, { method: "DELETE" });
}

export function publishSurvey(code: string) {
  return apiRequest<Survey>(`/api/surveys/${encodeURIComponent(code)}/publish`, { method: "POST" });
}

export function unpublishSurvey(code: string) {
  return apiRequest<Survey>(`/api/surveys/${encodeURIComponent(code)}/unpublish`, { method: "POST" });
}

export function addQuestion(code: string, question: QuestionWrite) {
  return apiRequest<Survey>(`/api/surveys/${encodeURIComponent(code)}/questions`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(toQuestionPayload(question)) });
}

export function updateQuestion(code: string, key: string, question: QuestionWrite) {
  return apiRequest<Survey>(`/api/surveys/${encodeURIComponent(code)}/questions/${encodeURIComponent(key)}`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(toQuestionPayload(question)) });
}

export function deleteQuestion(code: string, key: string) {
  return apiRequest<void>(`/api/surveys/${encodeURIComponent(code)}/questions/${encodeURIComponent(key)}`, { method: "DELETE" });
}

export function getResults(code: string) {
  return apiRequest<SurveyResults>(`/api/surveys/${encodeURIComponent(code)}/results`);
}

function toSurveyPayload(survey: SurveyWrite) {
  return {
    code: survey.code.trim(),
    title: survey.title.trim(),
    description: survey.description.trim() || null,
    startsAt: survey.startsAt ? new Date(survey.startsAt).toISOString() : null,
    endsAt: survey.endsAt ? new Date(survey.endsAt).toISOString() : null,
    allowAnonymous: survey.allowAnonymous,
    showResults: survey.showResults,
  };
}

function toQuestionPayload(question: QuestionWrite) {
  let options: unknown = null;
  if (question.options.trim()) options = JSON.parse(question.options);
  return {
    key: question.key.trim(),
    text: question.text.trim(),
    type: question.type,
    options,
    isRequired: question.isRequired,
    displayOrder: question.displayOrder,
  };
}
