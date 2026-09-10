"use client";

import Link from "next/link";
import { useEffect, useState, type FormEvent } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { apiRequest } from "@/lib/auth-store";
import { ApiError, asApiError } from "@/lib/http";

type Paged<T> = { items: T[]; page: number; pageSize: number; total: number };
type AppointmentType = { id: string; code: string; name: string; description: string | null; location: string | null; durationMinutes: number; requiresConfirmation: boolean; maxDaysAhead: number; isActive: boolean };
type Slot = { id: string; startsAt: string; endsAt: string; capacity: number; bookedCount: number; isBlocked: boolean };
type Appointment = { id: string; appointmentTypeCode: string; appointmentTypeName: string; startsAt: string; endsAt: string; status: string; notes: string | null; decisionNote: string | null; referenceCode: string };
type WriteType = Omit<AppointmentType, "id" | "code"> & { code: string };

const statuses = ["Requested", "Confirmed", "Rejected", "Cancelled", "Completed", "NoShow"];
const inputClass = "mt-1 w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-slate-950 disabled:bg-slate-100";
const buttonClass = "rounded-md bg-blue-700 px-3 py-2 text-sm font-medium text-white hover:bg-blue-800 disabled:opacity-60";
const secondaryButtonClass = "rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium text-slate-800 hover:bg-slate-50 disabled:opacity-60";
const dangerButtonClass = "rounded-md bg-red-700 px-3 py-2 text-sm font-medium text-white hover:bg-red-800 disabled:opacity-60";

const emptyType: WriteType = { code: "", name: "", description: "", location: "", durationMinutes: 30, requiresConfirmation: true, maxDaysAhead: 30, isActive: true };

function ErrorMessage({ error }: { error: ApiError | null }) {
  if (!error) return null;
  return <ApiErrorPanel error={error} />;
}

export default function AppointmentsPage() {
  const [types, setTypes] = useState<AppointmentType[]>([]);
  const [selected, setSelected] = useState<AppointmentType | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const [revision, setRevision] = useState(0);

  useEffect(() => {
    let current = true;
    void apiRequest<Paged<AppointmentType>>("/api/appointment-types?page=1&pageSize=100")
      .then(result => { if (current) { setTypes(result.items); setError(null); } })
      .catch(failure => { if (current) setError(asApiError(failure)); });
    return () => { current = false; };
  }, [revision]);

  return <main className="min-h-screen bg-slate-100 p-4 text-slate-950 sm:p-6"><div className="mx-auto max-w-7xl space-y-6">
    <header className="flex flex-wrap items-center justify-between gap-3"><div><p className="text-sm font-semibold uppercase tracking-wider text-blue-700">Portal de servicii</p><h1 className="text-2xl font-semibold">Programări</h1></div><Link href="/" className="text-blue-700 underline">Înapoi la administrare</Link></header>
    <ErrorMessage error={error} />
    <div className="grid gap-6 lg:grid-cols-[19rem_minmax(0,1fr)]">
      <aside className="space-y-3 rounded-xl bg-white p-4 shadow-sm"><div className="flex items-center justify-between"><h2 className="font-semibold">Tipuri</h2><button className={secondaryButtonClass} onClick={() => setSelected(null)}>Tip nou</button></div>
        {types.map(type => <button key={type.id} className={"w-full rounded-lg p-3 text-left hover:bg-slate-100 " + (selected?.id === type.id ? "bg-blue-50 outline outline-2 outline-blue-700" : "bg-slate-50")} onClick={() => setSelected(type)}><span className="block font-medium">{type.name}</span><span className="block text-sm text-slate-600">{type.code} · {type.isActive ? "activ" : "inactiv"}</span></button>)}
        {types.length === 0 && <p className="text-sm text-slate-600">Nu există tipuri configurate.</p>}
      </aside>
      <section className="space-y-6">
        <TypeEditor key={selected?.id ?? "new"} selected={selected} onSaved={type => { setSelected(type); setRevision(value => value + 1); }} onDeleted={() => { setSelected(null); setRevision(value => value + 1); }} />
        {selected && <SlotsPanel type={selected} />}
        <AppointmentsPanel types={types} />
      </section>
    </div>
  </div></main>;
}

function TypeEditor({ selected, onSaved, onDeleted }: { selected: AppointmentType | null; onSaved: (type: AppointmentType) => void; onDeleted: () => void }) {
  const [value, setValue] = useState<WriteType>(selected ? { ...selected, code: selected.code } : emptyType);
  const [busy, setBusy] = useState<"save" | "delete" | null>(null);
  const [error, setError] = useState<ApiError | null>(null);
  const isNew = !selected;

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy("save"); setError(null);
    try {
      const payload = { ...value, code: value.code.trim().toLowerCase(), name: value.name.trim(), description: value.description || null, location: value.location || null };
      const update = {
        name: payload.name,
        description: payload.description,
        location: payload.location,
        durationMinutes: payload.durationMinutes,
        requiresConfirmation: payload.requiresConfirmation,
        maxDaysAhead: payload.maxDaysAhead,
        isActive: payload.isActive,
      };
      const result = await apiRequest<AppointmentType>(isNew ? "/api/appointment-types" : `/api/appointment-types/${encodeURIComponent(selected.code)}`, { method: isNew ? "POST" : "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify(isNew ? payload : update) });
      onSaved(result);
    } catch (failure) { setError(asApiError(failure)); } finally { setBusy(null); }
  }

  async function remove() {
    if (!selected || !window.confirm("Ștergi tipul de programare?")) return;
    setBusy("delete"); setError(null);
    try { await apiRequest<void>(`/api/appointment-types/${encodeURIComponent(selected.code)}`, { method: "DELETE" }); onDeleted(); }
    catch (failure) { setError(asApiError(failure)); } finally { setBusy(null); }
  }

  return <form onSubmit={save} className="space-y-4 rounded-xl bg-white p-5 shadow-sm"><h2 className="text-xl font-semibold">{isNew ? "Tip de programare nou" : `Editează: ${selected.name}`}</h2><ErrorMessage error={error} />
    <div className="grid gap-4 sm:grid-cols-2"><label>Cod<input className={inputClass} required maxLength={40} pattern="[a-z0-9]+(?:-[a-z0-9]+)*" disabled={!isNew || busy !== null} value={value.code} onChange={event => setValue({ ...value, code: event.target.value })} /></label><label>Denumire<input className={inputClass} required maxLength={200} disabled={busy !== null} value={value.name} onChange={event => setValue({ ...value, name: event.target.value })} /></label><label>Durată (minute)<input className={inputClass} type="number" min="1" disabled={busy !== null} value={value.durationMinutes} onChange={event => setValue({ ...value, durationMinutes: Number(event.target.value) })} /></label><label>Zile disponibile în avans<input className={inputClass} type="number" min="0" disabled={busy !== null} value={value.maxDaysAhead} onChange={event => setValue({ ...value, maxDaysAhead: Number(event.target.value) })} /></label><label>Locație<input className={inputClass} maxLength={250} disabled={busy !== null} value={value.location ?? ""} onChange={event => setValue({ ...value, location: event.target.value })} /></label><label className="flex items-center gap-2 self-end pb-2"><input type="checkbox" checked={value.requiresConfirmation} disabled={busy !== null} onChange={event => setValue({ ...value, requiresConfirmation: event.target.checked })} />Necesită confirmare</label><label className="flex items-center gap-2 self-end pb-2"><input type="checkbox" checked={value.isActive} disabled={busy !== null} onChange={event => setValue({ ...value, isActive: event.target.checked })} />Activ</label></div>
    <label>Descriere<textarea className={inputClass} rows={3} maxLength={1000} disabled={busy !== null} value={value.description ?? ""} onChange={event => setValue({ ...value, description: event.target.value })} /></label>
    <div className="flex gap-3"><button className={buttonClass} disabled={busy !== null}>{busy === "save" ? "Salvăm…" : "Salvează"}</button>{!isNew && <button className="rounded-md bg-red-700 px-3 py-2 text-sm font-medium text-white hover:bg-red-800 disabled:opacity-60" type="button" disabled={busy !== null} onClick={() => void remove()}>{busy === "delete" ? "Ștergem…" : "Șterge"}</button>}</div>
  </form>;
}

function SlotsPanel({ type }: { type: AppointmentType }) {
  const [slots, setSlots] = useState<Slot[]>([]);
  const [error, setError] = useState<ApiError | null>(null);
  const [revision, setRevision] = useState(0);
  const [busy, setBusy] = useState(false);
  const [generation, setGeneration] = useState({ startDate: "", endDate: "", dayStartsAt: "09:00", dayEndsAt: "17:00", weekdays: [1, 2, 3, 4, 5], capacity: 1, timeZoneId: "Europe/Bucharest" });

  useEffect(() => { let current = true; void apiRequest<Paged<Slot>>(`/api/appointment-types/${encodeURIComponent(type.code)}/slots?page=1&pageSize=100`).then(result => { if (current) { setSlots(result.items); setError(null); } }).catch(failure => { if (current) setError(asApiError(failure)); }); return () => { current = false; }; }, [type.code, revision]);
  async function generate(event: FormEvent<HTMLFormElement>) { event.preventDefault(); setBusy(true); setError(null); try { await apiRequest<Slot[]>(`/api/appointment-types/${encodeURIComponent(type.code)}/slots/generate`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(generation) }); setRevision(value => value + 1); } catch (failure) { setError(asApiError(failure)); } finally { setBusy(false); } }
  async function block(slot: Slot) { setBusy(true); setError(null); try { await apiRequest<Slot>(`/api/appointment-types/${encodeURIComponent(type.code)}/slots/${slot.id}/block`, { method: "PUT", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ isBlocked: !slot.isBlocked }) }); setRevision(value => value + 1); } catch (failure) { setError(asApiError(failure)); } finally { setBusy(false); } }
  async function remove(slot: Slot) { if (!window.confirm("Ștergi acest interval? Intervalele cu programări nu pot fi șterse.")) return; setBusy(true); setError(null); try { await apiRequest<void>(`/api/appointment-types/${encodeURIComponent(type.code)}/slots/${slot.id}`, { method: "DELETE" }); setRevision(value => value + 1); } catch (failure) { setError(asApiError(failure)); } finally { setBusy(false); } }
  function toggleWeekday(day: number) { setGeneration(current => ({ ...current, weekdays: current.weekdays.includes(day) ? current.weekdays.filter(item => item !== day) : [...current.weekdays, day] })); }
  return <section className="space-y-4 rounded-xl bg-white p-5 shadow-sm"><h2 className="text-xl font-semibold">Intervale: {type.name}</h2><ErrorMessage error={error} />
    <form onSubmit={generate} className="grid gap-3 rounded-lg bg-slate-50 p-4 sm:grid-cols-3"><label>De la<input className={inputClass} type="date" required value={generation.startDate} onChange={event => setGeneration({ ...generation, startDate: event.target.value })} /></label><label>Până la<input className={inputClass} type="date" required value={generation.endDate} onChange={event => setGeneration({ ...generation, endDate: event.target.value })} /></label><label>Capacitate<input className={inputClass} type="number" min="1" value={generation.capacity} onChange={event => setGeneration({ ...generation, capacity: Number(event.target.value) })} /></label><label>Începe la<input className={inputClass} type="time" required value={generation.dayStartsAt} onChange={event => setGeneration({ ...generation, dayStartsAt: event.target.value })} /></label><label>Se termină la<input className={inputClass} type="time" required value={generation.dayEndsAt} onChange={event => setGeneration({ ...generation, dayEndsAt: event.target.value })} /></label><label>Fus orar<input className={inputClass} required value={generation.timeZoneId} onChange={event => setGeneration({ ...generation, timeZoneId: event.target.value })} /></label><fieldset className="sm:col-span-3"><legend className="font-medium">Zile</legend><div className="mt-2 flex flex-wrap gap-3">{[[1,"L"],[2,"Ma"],[3,"Mi"],[4,"J"],[5,"V"],[6,"S"],[7,"D"]].map(([day, label]) => <label key={day as number} className="flex items-center gap-1"><input type="checkbox" checked={generation.weekdays.includes(day as number)} onChange={() => toggleWeekday(day as number)} />{label}</label>)}</div></fieldset><button className={buttonClass + " sm:col-span-3 justify-self-start"} disabled={busy}>{busy ? "Generăm…" : "Generează intervale"}</button></form>
    <div className="overflow-x-auto"><table className="min-w-full text-left text-sm"><thead className="bg-slate-50"><tr><th className="p-3">Interval</th><th className="p-3">Ocupare</th><th className="p-3">Stare</th><th className="p-3">Acțiuni</th></tr></thead><tbody>{slots.map(slot => <tr key={slot.id} className="border-t border-slate-200"><td className="p-3">{new Date(slot.startsAt).toLocaleString("ro-RO")} – {new Date(slot.endsAt).toLocaleTimeString("ro-RO", { hour: "2-digit", minute: "2-digit" })}</td><td className="p-3">{slot.bookedCount} / {slot.capacity}</td><td className="p-3">{slot.isBlocked ? "Blocat" : "Disponibil"}</td><td className="p-3"><div className="flex flex-wrap items-center gap-2"><button className={secondaryButtonClass} disabled={busy} onClick={() => void block(slot)}>{slot.isBlocked ? "Deblochează" : "Blochează"}</button>{slot.bookedCount === 0 ? <button className={dangerButtonClass} disabled={busy} onClick={() => void remove(slot)}>Șterge</button> : <span className="text-xs text-slate-600">Nu se poate șterge: are programări.</span>}</div></td></tr>)}{slots.length === 0 && <tr><td colSpan={4} className="p-3 text-slate-600">Nu există intervale.</td></tr>}</tbody></table></div>
  </section>;
}

function AppointmentsPanel({ types }: { types: AppointmentType[] }) {
  const [items, setItems] = useState<Appointment[]>([]); const [error, setError] = useState<ApiError | null>(null); const [typeCode, setTypeCode] = useState(""); const [status, setStatus] = useState(""); const [from, setFrom] = useState(""); const [to, setTo] = useState(""); const [revision, setRevision] = useState(0); const [busy, setBusy] = useState<string | null>(null);
  useEffect(() => { let current = true; const query = new URLSearchParams({ page: "1", pageSize: "100" }); if (typeCode) query.set("typeCode", typeCode); if (status) query.set("status", status); if (from) query.set("from", new Date(from).toISOString()); if (to) query.set("to", new Date(to).toISOString()); void apiRequest<Paged<Appointment>>(`/api/appointments?${query}`).then(result => { if (current) { setItems(result.items); setError(null); } }).catch(failure => { if (current) setError(asApiError(failure)); }); return () => { current = false; }; }, [typeCode, status, from, to, revision]);
  async function transition(item: Appointment, action: "confirm" | "reject" | "complete" | "no-show") { let decisionNote: string | null = null; if (action === "reject") { decisionNote = window.prompt("Motivul respingerii:")?.trim() ?? null; if (!decisionNote) return; } setBusy(item.id); setError(null); try { await apiRequest<Appointment>(`/api/appointments/${item.id}/${action}`, { method: "POST", ...(decisionNote ? { headers: { "Content-Type": "application/json" }, body: JSON.stringify({ decisionNote }) } : {}) }); setRevision(value => value + 1); } catch (failure) { setError(asApiError(failure)); } finally { setBusy(null); } }
  return <section className="space-y-4 rounded-xl bg-white p-5 shadow-sm"><h2 className="text-xl font-semibold">Cereri de programare</h2><ErrorMessage error={error} /><div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4"><label>Tip<select className={inputClass} value={typeCode} onChange={event => setTypeCode(event.target.value)}><option value="">Toate</option>{types.map(type => <option key={type.id} value={type.code}>{type.name}</option>)}</select></label><label>Stare<select className={inputClass} value={status} onChange={event => setStatus(event.target.value)}><option value="">Toate</option>{statuses.map(item => <option key={item} value={item}>{item}</option>)}</select></label><label>Începe după<input className={inputClass} type="datetime-local" value={from} onChange={event => setFrom(event.target.value)} /></label><label>Începe înainte<input className={inputClass} type="datetime-local" value={to} onChange={event => setTo(event.target.value)} /></label></div><div className="overflow-x-auto"><table className="min-w-full text-left text-sm"><thead className="bg-slate-50"><tr><th className="p-3">Referință</th><th className="p-3">Interval</th><th className="p-3">Stare</th><th className="p-3">Acțiuni</th></tr></thead><tbody>{items.map(item => <tr key={item.id} className="border-t border-slate-200"><td className="p-3">{item.referenceCode}<span className="block text-slate-600">{item.appointmentTypeName}</span></td><td className="p-3">{new Date(item.startsAt).toLocaleString("ro-RO")}</td><td className="p-3">{item.status}{item.decisionNote && <span className="block text-slate-600">{item.decisionNote}</span>}</td><td className="p-3"><div className="flex flex-wrap gap-2">{item.status === "Requested" && <><button className={buttonClass} disabled={busy !== null} onClick={() => void transition(item, "confirm")}>Confirmă</button><button className={secondaryButtonClass} disabled={busy !== null} onClick={() => void transition(item, "reject")}>Respinge</button></>}{item.status === "Confirmed" && <><button className={buttonClass} disabled={busy !== null} onClick={() => void transition(item, "complete")}>Onorată</button><button className={secondaryButtonClass} disabled={busy !== null} onClick={() => void transition(item, "no-show")}>Neprezentat</button></>}</div></td></tr>)}{items.length === 0 && <tr><td colSpan={4} className="p-3 text-slate-600">Nu există programări.</td></tr>}</tbody></table></div></section>;
}
