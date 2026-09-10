"use client";

import { useEffect, useState, type FormEvent } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { useAuth } from "@/hooks/use-auth";
import { ApiError, asApiError } from "@/lib/http";
import {
  addQuestion,
  createSurvey,
  deleteQuestion,
  deleteSurvey,
  emptyQuestion,
  emptySurvey,
  getResults,
  getSurvey,
  getSurveys,
  publishSurvey,
  type QuestionWrite,
  type Survey,
  type SurveyListItem,
  type SurveyResults,
  type SurveyWrite,
  unpublishSurvey,
  updateQuestion,
  updateSurvey,
} from "@/lib/surveys";

const inputClass = "mt-1 w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-slate-950 focus:border-blue-600 focus:outline-none focus:ring-2 focus:ring-blue-200 disabled:bg-slate-100";
const buttonClass = "rounded-md bg-blue-700 px-3 py-2 text-sm font-medium text-white hover:bg-blue-800 disabled:cursor-wait disabled:opacity-60";
const secondaryButtonClass = "rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-800 hover:bg-slate-50 disabled:cursor-wait disabled:opacity-60";
const dangerButtonClass = "rounded-md bg-red-700 px-3 py-2 text-sm font-medium text-white hover:bg-red-800 disabled:cursor-wait disabled:opacity-60";

function ErrorMessage({ error }: { error: ApiError | null }) {
  if (!error) return null;
  return <ApiErrorPanel error={error} />;
}

function toWrite(survey: Survey): SurveyWrite {
  return {
    code: survey.code,
    title: survey.title,
    description: survey.description ?? "",
    startsAt: survey.startsAt?.slice(0, 16) ?? "",
    endsAt: survey.endsAt?.slice(0, 16) ?? "",
    allowAnonymous: survey.allowAnonymous,
    showResults: survey.showResults,
    isPublished: survey.isPublished,
  };
}

function toQuestionWrite(question: Survey["questions"][number]): QuestionWrite {
  return {
    key: question.key,
    text: question.text,
    type: question.type,
    options: question.options ? JSON.stringify(question.options, null, 2) : "",
    isRequired: question.isRequired,
    displayOrder: question.displayOrder,
  };
}

export default function SurveysPage() {
  const auth = useAuth();
  const [items, setItems] = useState<SurveyListItem[]>([]);
  const [selected, setSelected] = useState<Survey | null>(null);
  const [draft, setDraft] = useState<SurveyWrite>(emptySurvey());
  const [question, setQuestion] = useState<QuestionWrite>(emptyQuestion());
  const [editingKey, setEditingKey] = useState<string | null>(null);
  const [results, setResults] = useState<SurveyResults | null>(null);
  const [showPreview, setShowPreview] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  async function loadList() {
    const response = await getSurveys();
    setItems(response.items);
  }

  useEffect(() => {
    if (auth.status !== "authenticated") return;
    let active = true;
    void getSurveys()
      .then(response => {
        if (active) setItems(response.items);
      })
      .catch(failure => {
        if (active) setError(asApiError(failure));
      });

    return () => {
      active = false;
    };
  }, [auth.status]);

  async function openSurvey(code: string) {
    setBusy(true); setError(null); setMessage(null); setResults(null); setShowPreview(false);
    try {
      const survey = await getSurvey(code);
      setSelected(survey); setDraft(toWrite(survey)); setQuestion(emptyQuestion(survey.questions.length)); setEditingKey(null);
    } catch (failure) { setError(asApiError(failure)); } finally { setBusy(false); }
  }

  function newSurvey() {
    setSelected(null); setDraft(emptySurvey()); setQuestion(emptyQuestion()); setEditingKey(null); setResults(null); setShowPreview(false); setMessage(null); setError(null);
  }

  async function saveSurvey(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError(null); setMessage(null);
    try {
      const saved = selected ? await updateSurvey(selected.code, draft) : await createSurvey(draft);
      setSelected(saved); setDraft(toWrite(saved)); await loadList(); setMessage("Chestionarul a fost salvat.");
    } catch (failure) { setError(asApiError(failure)); } finally { setBusy(false); }
  }

  async function togglePublish() {
    if (!selected) return;
    setBusy(true); setError(null);
    try {
      const updated = selected.isPublished ? await unpublishSurvey(selected.code) : await publishSurvey(selected.code);
      setSelected(updated); setDraft(toWrite(updated)); await loadList(); setMessage(updated.isPublished ? "Chestionarul a fost publicat." : "Chestionarul a fost retras.");
    } catch (failure) { setError(asApiError(failure)); } finally { setBusy(false); }
  }

  async function removeSurvey() {
    if (!selected || !window.confirm("Ștergi chestionarul?")) return;
    setBusy(true); setError(null);
    try { await deleteSurvey(selected.code); newSurvey(); await loadList(); setMessage("Chestionarul a fost șters."); } catch (failure) { setError(asApiError(failure)); } finally { setBusy(false); }
  }

  async function saveQuestion(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected) return;
    try { JSON.parse(question.options || "null"); } catch { setError(new ApiError(422, { detail: "Opțiunile trebuie să fie JSON valid." })); return; }
    setBusy(true); setError(null); setMessage(null);
    try {
      const updated = editingKey ? await updateQuestion(selected.code, editingKey, question) : await addQuestion(selected.code, question);
      setSelected(updated); setQuestion(emptyQuestion(updated.questions.length)); setEditingKey(null); await loadList(); setMessage("Întrebarea a fost salvată.");
    } catch (failure) { setError(asApiError(failure)); } finally { setBusy(false); }
  }

  async function removeQuestion(key: string) {
    if (!selected || !window.confirm("Ștergi întrebarea?")) return;
    setBusy(true); setError(null);
    try { await deleteQuestion(selected.code, key); const updated = await getSurvey(selected.code); setSelected(updated); setQuestion(emptyQuestion(updated.questions.length)); await loadList(); } catch (failure) { setError(asApiError(failure)); } finally { setBusy(false); }
  }

  async function showResults() {
    if (!selected) return;
    setBusy(true); setError(null);
    try { setResults(await getResults(selected.code)); } catch (failure) { setError(asApiError(failure)); } finally { setBusy(false); }
  }

  if (auth.status !== "authenticated") return <p className="rounded-lg bg-slate-100 p-4">Se verifică sesiunea de administrator…</p>;

  return <div className="space-y-6">
    <ErrorMessage error={error} />
    {message && <p className="rounded-lg bg-green-50 p-4 text-green-900">{message}</p>}
    <div className="flex flex-wrap gap-3"><button type="button" className={buttonClass} onClick={newSurvey}>Chestionar nou</button>{selected && <><button type="button" className={secondaryButtonClass} disabled={busy} onClick={() => void togglePublish()}>{selected.isPublished ? "Retrage" : "Publică"}</button><button type="button" className={secondaryButtonClass} disabled={busy} onClick={() => setShowPreview(current => !current)}>{showPreview ? "Ascunde previzualizarea" : "Previzualizează"}</button><button type="button" className={secondaryButtonClass} disabled={busy} onClick={() => void showResults()}>Vezi rezultate</button><button type="button" className={dangerButtonClass} disabled={busy} onClick={() => void removeSurvey()}>Șterge</button></>}</div>
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,2fr)]">
      <section className="space-y-3 rounded-xl border border-slate-200 bg-white p-4 shadow-sm"><h2 className="text-lg font-semibold">Chestionare</h2>{items.length === 0 ? <p className="text-sm text-slate-600">Nu există chestionare.</p> : items.map(item => <button key={item.code} type="button" onClick={() => void openSurvey(item.code)} className={`block w-full rounded-lg border p-3 text-left ${selected?.code === item.code ? "border-blue-600 bg-blue-50" : "border-slate-200 hover:bg-slate-50"}`}><span className="font-medium">{item.title}</span><span className="mt-1 block text-xs text-slate-600">{item.code} · {item.isPublished ? "publicat" : "ciornă"} · {item.responseCount} răspunsuri</span></button>)}</section>
      <section className="space-y-6 rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
        <form onSubmit={saveSurvey} className="grid gap-4 md:grid-cols-2"><h2 className="md:col-span-2 text-lg font-semibold">{selected ? `Editare: ${selected.code}` : "Chestionar nou"}</h2><label>Cod<input required disabled={!!selected || busy} value={draft.code} onChange={event => setDraft({ ...draft, code: event.target.value })} className={inputClass} /></label><label>Titlu<input required disabled={busy} value={draft.title} onChange={event => setDraft({ ...draft, title: event.target.value })} className={inputClass} /></label><label className="md:col-span-2">Descriere HTML<textarea disabled={busy} rows={3} value={draft.description} onChange={event => setDraft({ ...draft, description: event.target.value })} className={inputClass} /></label><label>Începe la<input type="datetime-local" disabled={busy} value={draft.startsAt} onChange={event => setDraft({ ...draft, startsAt: event.target.value })} className={inputClass} /></label><label>Se termină la<input type="datetime-local" disabled={busy} value={draft.endsAt} onChange={event => setDraft({ ...draft, endsAt: event.target.value })} className={inputClass} /></label><label className="flex items-center gap-2"><input type="checkbox" checked={draft.allowAnonymous} onChange={event => setDraft({ ...draft, allowAnonymous: event.target.checked })} /> Permite răspunsuri anonime</label><label className="flex items-center gap-2"><input type="checkbox" checked={draft.showResults} onChange={event => setDraft({ ...draft, showResults: event.target.checked })} /> Afișează rezultatele</label><button type="submit" disabled={busy} className="md:col-span-2 justify-self-start rounded-md bg-blue-700 px-4 py-2 text-sm font-medium text-white disabled:opacity-50">{busy ? "Se salvează…" : "Salvează chestionarul"}</button></form>
        {selected && <><div className="border-t border-slate-200 pt-5"><h3 className="mb-3 font-semibold">Întrebări</h3>{selected.questions.length === 0 ? <p className="text-sm text-slate-600">Adaugă prima întrebare.</p> : <div className="space-y-2">{selected.questions.map(item => <div key={item.key} className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-slate-200 p-3"><div><p className="font-medium">{item.text}</p><p className="text-xs text-slate-600">{item.key} · {item.type}{item.isRequired ? " · obligatorie" : ""}</p></div><div className="flex gap-2"><button type="button" className={secondaryButtonClass} onClick={() => { setEditingKey(item.key); setQuestion(toQuestionWrite(item)); }}>Editează</button><button type="button" className={dangerButtonClass} disabled={busy} onClick={() => void removeQuestion(item.key)}>Șterge</button></div></div>)}</div>}</div><form onSubmit={saveQuestion} className="space-y-3 border-t border-slate-200 pt-5"><h3 className="font-semibold">{editingKey ? "Editare întrebare" : "Întrebare nouă"}</h3><div className="grid gap-3 md:grid-cols-2"><label>Cheie<input required disabled={!!editingKey || busy} value={question.key} onChange={event => setQuestion({ ...question, key: event.target.value })} className={inputClass} /></label><label>Tip<select disabled={busy} value={question.type} onChange={event => setQuestion({ ...question, type: event.target.value as QuestionWrite["type"] })} className={inputClass}><option value="FreeText">Text liber</option><option value="SingleChoice">Alegere unică</option><option value="MultiChoice">Alegere multiplă</option><option value="Rating">Rating</option></select></label></div><label>Text<input required disabled={busy} value={question.text} onChange={event => setQuestion({ ...question, text: event.target.value })} className={inputClass} /></label><label>Opțiuni JSON<textarea disabled={busy} rows={4} placeholder='[{"value":"online","label":"Online"}]' value={question.options} onChange={event => setQuestion({ ...question, options: event.target.value })} className={inputClass} /></label><label className="flex items-center gap-2"><input type="checkbox" checked={question.isRequired} onChange={event => setQuestion({ ...question, isRequired: event.target.checked })} /> Răspuns obligatoriu</label><div className="flex gap-2"><button type="submit" disabled={busy} className={buttonClass}>Salvează întrebarea</button>{editingKey && <button type="button" className={secondaryButtonClass} onClick={() => { setEditingKey(null); setQuestion(emptyQuestion(selected.questions.length)); }}>Anulează</button>}</div></form></>}
        {selected && showPreview && <SurveyPreview survey={selected} draft={draft} />}
        {results && <SurveyResultsPanel results={results} />}
      </section>
    </div>
  </div>;
}

function SurveyPreview({ survey, draft }: { survey: Survey; draft: SurveyWrite }) {
  return (
    <section className="space-y-4 border-t border-slate-200 pt-5" aria-label="Previzualizare chestionar">
      <div>
        <h3 className="font-semibold">Previzualizare pentru cetățean</h3>
        <p className="mt-1 text-sm text-slate-600">Aceasta folosește modificările nesalvate ale titlului și descrierii.</p>
      </div>
      <div className="rounded-lg border border-blue-200 bg-blue-50 p-5">
        <h4 className="text-xl font-semibold">{draft.title || "Titlul chestionarului"}</h4>
        {draft.description && <p className="mt-2 whitespace-pre-wrap text-slate-700">{htmlToPlainText(draft.description)}</p>}
        <div className="mt-5 space-y-4">
          {survey.questions.length === 0 ? <p className="text-sm text-slate-600">Adaugă întrebări pentru a vedea formularul.</p> : survey.questions.map(question => <fieldset key={question.id} className="rounded-md border border-slate-200 bg-white p-4"><legend className="px-1 font-medium">{question.text}{question.isRequired && <span className="text-red-700"> *</span>}</legend><PreviewQuestion question={question} /></fieldset>)}
        </div>
        <button type="button" disabled className="mt-5 rounded-md bg-blue-700 px-4 py-2 text-sm font-medium text-white opacity-60">Trimite răspunsul</button>
      </div>
    </section>
  );
}

function PreviewQuestion({ question }: { question: Survey["questions"][number] }) {
  const options = Array.isArray(question.options) ? question.options.filter((option): option is { value: string; label: string } => typeof option === "object" && option !== null && typeof (option as { value?: unknown }).value === "string" && typeof (option as { label?: unknown }).label === "string") : [];
  if (question.type === "FreeText") return <textarea disabled rows={3} className={inputClass} />;
  if (question.type === "Rating") return <div className="mt-2 flex flex-wrap gap-2">{[1, 2, 3, 4, 5].map(value => <label key={value} className="flex items-center gap-1"><input type="radio" disabled name={question.id} />{value}</label>)}</div>;
  return <div className="mt-2 space-y-2">{options.map(option => <label key={option.value} className="flex items-center gap-2"><input type={question.type === "MultiChoice" ? "checkbox" : "radio"} disabled name={question.id} />{option.label}</label>)}</div>;
}

function SurveyResultsPanel({ results }: { results: SurveyResults }) {
  return (
    <section className="space-y-4 border-t border-slate-200 pt-5">
      <h3 className="font-semibold">Rezultate: {results.totalResponses} răspunsuri</h3>
      <div className="space-y-3">
        {results.items.map(item => <div key={item.key} className="rounded-lg bg-slate-50 p-3"><p className="font-medium">{item.key}</p><ul className="list-disc pl-5 text-sm">{Object.entries(item.valueCounts).map(([value, count]) => <li key={value}>{value}: {count}</li>)}</ul></div>)}
      </div>
      <div>
        <h4 className="font-semibold">Răspunsuri individuale</h4>
        {results.responses.length === 0 ? <p className="mt-2 text-sm text-slate-600">Nu există răspunsuri individuale.</p> : <div className="mt-3 space-y-2">{results.responses.map((response, index) => <details key={response.id} className="rounded-lg border border-slate-200 bg-white p-3"><summary className="cursor-pointer font-medium">Răspunsul #{index + 1} · {new Date(response.submittedAt).toLocaleString("ro-RO")}</summary><pre className="mt-3 max-h-72 overflow-auto rounded bg-slate-950 p-3 text-xs text-slate-100">{JSON.stringify(response.answers, null, 2)}</pre></details>)}</div>}
      </div>
    </section>
  );
}

function htmlToPlainText(html: string) {
  if (typeof window === "undefined") return html;
  return new DOMParser().parseFromString(html, "text/html").body.textContent?.trim() ?? "";
}
