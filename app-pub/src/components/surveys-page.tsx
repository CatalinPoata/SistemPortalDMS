"use client";

import { useEffect, useState } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { useAuth } from "@/hooks/use-auth";
import { asApiError, type ApiError } from "@/lib/http";
import { getPublicSurvey, getPublicSurveys, submitSurvey, type Survey, type SurveyQuestion } from "@/lib/surveys";

type Answer = string | string[] | number;

function choices(question: SurveyQuestion) {
  return Array.isArray(question.options)
    ? question.options.filter((item): item is { value: string; label: string } =>
        typeof item === "object" && item !== null &&
        typeof (item as { value?: unknown }).value === "string" &&
        typeof (item as { label?: unknown }).label === "string")
    : [];
}

function ratingRange(question: SurveyQuestion) {
  const options = question.options;
  if (!options || typeof options !== "object" || Array.isArray(options)) return { min: 1, max: 5 };
  const value = options as { min?: unknown; max?: unknown };
  return {
    min: typeof value.min === "number" ? value.min : 1,
    max: typeof value.max === "number" ? value.max : 5,
  };
}

export default function SurveysPage() {
  const auth = useAuth();
  const [surveys, setSurveys] = useState<{ code: string; title: string; description: string | null }[]>([]);
  const [selected, setSelected] = useState<Survey | null>(null);
  const [answers, setAnswers] = useState<Record<string, Answer>>({});
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<ApiError | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [submission, setSubmission] = useState<Awaited<ReturnType<typeof submitSurvey>> | null>(null);

  useEffect(() => {
    void getPublicSurveys()
      .then(result => setSurveys(result.items))
      .catch(failure => setError(asApiError(failure)))
      .finally(() => setLoading(false));
  }, []);

  async function selectSurvey(code: string) {
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      const survey = await getPublicSurvey(code);
      setSelected(survey);
      setAnswers({});
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(false);
    }
  }

  function update(key: string, value: Answer) {
    setError(null);
    setAnswers(current => ({ ...current, [key]: value }));
  }

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected) return;
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      const created = await submitSurvey(
        selected.code,
        answers,
        auth.status === "authenticated",
      );
      setSubmission(created);
      setMessage("Răspunsul a fost înregistrat.");
      const refreshed = await getPublicSurvey(selected.code);
      setSelected(refreshed);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(false);
    }
  }

  const canSubmit = selected && !selected.hasResponded &&
    (selected.allowAnonymous || auth.status === "authenticated");
  if (loading) return <p className="text-slate-600">Se încarcă chestionarele…</p>;
  return (
    <div className="space-y-6">
      {error && <ApiErrorPanel error={error} showFieldErrors={false} />}
      {message && <p className="rounded-xl border border-green-200 bg-green-50 p-4 text-green-900">{message}</p>}

      {!selected ? (
        surveys.length === 0 ? <p className="text-slate-600">Nu există chestionare active.</p> :
          <div className="grid gap-4 md:grid-cols-2">
            {surveys.map(survey => (
              <article key={survey.code} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                <h2 className="text-xl font-semibold">{survey.title}</h2>
                {survey.description && <div className="rich-content mt-3 max-w-none text-sm" dangerouslySetInnerHTML={{ __html: survey.description }} />}
                <button type="button" onClick={() => void selectSurvey(survey.code)} className="mt-5 rounded-lg bg-blue-700 px-4 py-2 text-white hover:bg-blue-800 disabled:opacity-50" disabled={busy}>Completează</button>
              </article>
            ))}
          </div>
      ) : (
        <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <button type="button" onClick={() => setSelected(null)} className="text-sm text-blue-700 underline">← Înapoi la chestionare</button>
          <h2 className="mt-4 text-2xl font-bold">{selected.title}</h2>
          {selected.description && <div className="rich-content mt-3 max-w-none" dangerouslySetInnerHTML={{ __html: selected.description }} />}
          {selected.hasResponded ? <p className="mt-6 rounded-lg bg-slate-100 p-4">Ai răspuns deja la acest chestionar.</p> : !canSubmit ? <p className="mt-6 rounded-lg bg-amber-50 p-4 text-amber-900">Autentifică-te pentru a răspunde la acest chestionar.</p> :
            <form onSubmit={submit} className="mt-6 space-y-6">
              {selected.questions.map(question => <Question key={question.key} question={question} value={answers[question.key]} errors={error?.problem.errors?.[`answers.${question.key}`] ?? []} onChange={value => update(question.key, value)} />)}
              <button type="submit" disabled={busy} className="rounded-lg bg-blue-700 px-5 py-2 text-white hover:bg-blue-800 disabled:opacity-50">{busy ? "Se trimite…" : "Trimite răspunsul"}</button>
            </form>}
          {submission?.results && (
            <div className="mt-6 space-y-3 rounded-xl bg-slate-50 p-4">
              <h3 className="font-semibold">Rezultate agregate</h3>
              <p className="text-sm text-slate-600">Răspunsuri totale: {submission.results.totalResponses}</p>
              {submission.results.items.map(item => (
                <div key={item.key} className="text-sm">
                  <p className="font-medium">{item.key}</p>
                  <ul className="list-disc pl-5">{Object.entries(item.valueCounts).map(([value, count]) => <li key={value}>{value}: {count}</li>)}</ul>
                </div>
              ))}
            </div>
          )}
        </section>
      )}
    </div>
  );
}

function Question({ question, value, errors, onChange }: { question: SurveyQuestion; value: Answer | undefined; errors: string[]; onChange: (value: Answer) => void }) {
  const options = choices(question);
  return <fieldset className={`space-y-3 rounded-xl border p-4 ${errors.length > 0 ? "border-red-400" : "border-slate-200"}`}>
    <legend className="px-1 font-medium">{question.text}{question.isRequired && <span className="text-red-700"> *</span>}</legend>
    {question.type === "FreeText" && <textarea required={question.isRequired} value={typeof value === "string" ? value : ""} onChange={event => onChange(event.target.value)} rows={4} className="w-full rounded-lg border border-slate-300 p-3" />}
    {question.type === "SingleChoice" && options.map(option => <label key={option.value} className="flex gap-2"><input type="radio" name={question.key} required={question.isRequired} checked={value === option.value} onChange={() => onChange(option.value)} />{option.label}</label>)}
    {question.type === "MultiChoice" && options.map(option => { const current = Array.isArray(value) ? value : []; return <label key={option.value} className="flex gap-2"><input type="checkbox" checked={current.includes(option.value)} onChange={event => onChange(event.target.checked ? [...current, option.value] : current.filter(item => item !== option.value))} />{option.label}</label>; })}
    {question.type === "Rating" && <select required={question.isRequired} value={typeof value === "number" ? value : ""} onChange={event => onChange(Number(event.target.value))} className="rounded-lg border border-slate-300 p-2"><option value="">Alege un scor</option>{Array.from({ length: ratingRange(question).max - ratingRange(question).min + 1 }, (_, index) => { const score = ratingRange(question).min + index; return <option key={score} value={score}>{score}</option>; })}</select>}
    {errors.length > 0 && <ul role="alert" className="list-disc pl-5 text-sm text-red-700">{errors.map((message, index) => <li key={`${message}-${index}`}>{message}</li>)}</ul>}
  </fieldset>;
}
