"use client";

import Link from "next/link";
import { useEffect, useState } from "react";

import ApiErrorPanel from "@/components/api-error-panel";
import { useAuth } from "@/hooks/use-auth";
import {
  cancelAppointment,
  createAppointment,
  getAppointmentSlots,
  getAppointmentTypes,
  getMyAppointments,
  type Appointment,
  type AppointmentSlot,
  type AppointmentType,
} from "@/lib/appointments";
import { asApiError, type ApiError } from "@/lib/http";

function formatDate(value: string) {
  return new Intl.DateTimeFormat("ro-RO", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "Europe/Bucharest",
  }).format(new Date(value));
}

function dateInRomania(date: Date) {
  const parts = new Intl.DateTimeFormat("en-CA", {
    timeZone: "Europe/Bucharest",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(date);
  const value = (kind: Intl.DateTimeFormatPartTypes) =>
    parts.find(part => part.type === kind)?.value ?? "";
  return `${value("year")}-${value("month")}-${value("day")}`;
}

function todayInRomania() {
  return dateInRomania(new Date());
}

function addDays(date: string, days: number) {
  const value = new Date(`${date}T12:00:00Z`);
  value.setUTCDate(value.getUTCDate() + days);
  return value.toISOString().slice(0, 10);
}

type CalendarMonth = { year: number; month: number };

function monthFor(date: string): CalendarMonth {
  const [year, month] = date.split("-").map(Number);
  return { year, month: month - 1 };
}

function dateKey(year: number, month: number, day: number) {
  const value = new Date(Date.UTC(year, month, day));
  return `${value.getUTCFullYear()}-${String(value.getUTCMonth() + 1).padStart(2, "0")}-${String(value.getUTCDate()).padStart(2, "0")}`;
}

function addMonths(month: CalendarMonth, delta: number): CalendarMonth {
  const value = new Date(Date.UTC(month.year, month.month + delta, 1));
  return { year: value.getUTCFullYear(), month: value.getUTCMonth() };
}

function daysInCalendar(month: CalendarMonth) {
  const firstWeekday = new Date(Date.UTC(month.year, month.month, 1)).getUTCDay();
  const leadingDays = (firstWeekday + 6) % 7;
  const currentDays = new Date(Date.UTC(month.year, month.month + 1, 0)).getUTCDate();
  const previousDays = new Date(Date.UTC(month.year, month.month, 0)).getUTCDate();

  return Array.from({ length: 42 }, (_, index) => {
    const currentDay = index - leadingDays + 1;
    if (currentDay < 1) {
      return {
        key: dateKey(month.year, month.month - 1, previousDays + currentDay),
        day: previousDays + currentDay,
        inCurrentMonth: false,
      };
    }
    if (currentDay > currentDays) {
      return {
        key: dateKey(month.year, month.month + 1, currentDay - currentDays),
        day: currentDay - currentDays,
        inCurrentMonth: false,
      };
    }
    return { key: dateKey(month.year, month.month, currentDay), day: currentDay, inCurrentMonth: true };
  });
}

function monthLabel(month: CalendarMonth) {
  return new Intl.DateTimeFormat("ro-RO", {
    month: "long",
    year: "numeric",
    timeZone: "Europe/Bucharest",
  }).format(new Date(Date.UTC(month.year, month.month, 1)));
}

function statusLabel(status: Appointment["status"]) {
  return {
    Requested: "În așteptarea confirmării",
    Confirmed: "Confirmată",
    Rejected: "Respinsă",
    Cancelled: "Anulată",
    Completed: "Onorată",
    NoShow: "Neprezentare",
  }[status];
}

export default function AppointmentsPage() {
  const auth = useAuth();
  const [types, setTypes] = useState<AppointmentType[]>([]);
  const [slots, setSlots] = useState<AppointmentSlot[]>([]);
  const [calendarSlots, setCalendarSlots] = useState<AppointmentSlot[]>([]);
  const [appointments, setAppointments] = useState<Appointment[]>([]);
  const [selectedCode, setSelectedCode] = useState("");
  const [selectedDate, setSelectedDate] = useState("");
  const [calendarMonth, setCalendarMonth] = useState<CalendarMonth>(() =>
    monthFor(todayInRomania()),
  );
  const [selectedSlot, setSelectedSlot] = useState("");
  const [notes, setNotes] = useState("");
  const [loading, setLoading] = useState(true);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [calendarSlotsLoading, setCalendarSlotsLoading] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<ApiError | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const isCitizen = auth.status === "authenticated" && auth.user.role === "Citizen";

  useEffect(() => {
    let active = true;
    void Promise.all([
      getAppointmentTypes(),
      isCitizen
        ? getMyAppointments().then(items => ({ items }))
        : Promise.resolve({ items: [] as Appointment[] }),
    ])
      .then(([typeResult, appointmentResult]) => {
        if (!active) return;
        setTypes(typeResult.items);
        setAppointments(appointmentResult.items);
        if (typeResult.items[0]) {
          setSelectedCode(typeResult.items[0].code);
        }
      })
      .catch(failure => {
        if (active) setError(asApiError(failure));
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
    };
  }, [isCitizen]);

  useEffect(() => {
    if (!selectedCode || !selectedDate) {
      setSlots([]);
      setSelectedSlot("");
      return;
    }

    let active = true;
    setSlotsLoading(true);
    setSlots([]);
    setSelectedSlot("");
    setError(null);
    void getAppointmentSlots(selectedCode, selectedDate || undefined)
      .then(result => {
        if (active) {
          setSlots(result.items);
        }
      })
      .catch(failure => {
        if (active) {
          setError(asApiError(failure));
        }
      })
      .finally(() => {
        if (active) setSlotsLoading(false);
      });

    return () => {
      active = false;
    };
  }, [selectedCode, selectedDate]);

  useEffect(() => {
    if (!selectedCode) {
      setCalendarSlots([]);
      return;
    }

    let active = true;
    setCalendarSlotsLoading(true);
    void getAppointmentSlots(selectedCode)
      .then(result => {
        if (active) setCalendarSlots(result.items);
      })
      .catch(failure => {
        if (active) setError(asApiError(failure));
      })
      .finally(() => {
        if (active) setCalendarSlotsLoading(false);
      });

    return () => {
      active = false;
    };
  }, [selectedCode]);

  async function book() {
    if (!selectedCode || !selectedSlot) return;
    setBusy(true);
    setError(null);
    setMessage(null);
    try {
      const created = await createAppointment(
        selectedCode,
        selectedSlot,
        notes,
      );
      setAppointments(current => [created, ...current]);
      setNotes("");
      setSelectedSlot("");
      setMessage(`Programarea ${created.referenceCode} a fost solicitată.`);
      const refreshed = await getAppointmentSlots(
        selectedCode,
        selectedDate || undefined,
      );
      setSlots(refreshed.items);
      const refreshedCalendar = await getAppointmentSlots(selectedCode);
      setCalendarSlots(refreshedCalendar.items);
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(false);
    }
  }

  async function cancel(id: string) {
    setBusy(true);
    setError(null);
    try {
      const updated = await cancelAppointment(id);
      setAppointments(current => current.map(item =>
        item.id === id ? updated : item,
      ));
      setMessage("Programarea a fost anulată.");
      if (selectedCode) {
        const refreshed = await getAppointmentSlots(
          selectedCode,
          selectedDate || undefined,
        );
        setSlots(refreshed.items);
        const refreshedCalendar = await getAppointmentSlots(selectedCode);
        setCalendarSlots(refreshedCalendar.items);
      }
    } catch (failure) {
      setError(asApiError(failure));
    } finally {
      setBusy(false);
    }
  }

  if (loading) return <p role="status">Se încarcă programările…</p>;
  if (error && types.length === 0) return <ApiErrorPanel error={error} />;

  if (auth.status !== "authenticated") {
    return (
      <section className="rounded-2xl border border-blue-200 bg-blue-50 p-6">
        <p>Poți consulta intervalele disponibile fără cont.</p>
        <Link href="/login?next=/programari" className="mt-3 inline-block text-blue-700 underline">
          Autentifică-te pentru a solicita o programare.
        </Link>
      </section>
    );
  }

  if (auth.user.role !== "Citizen") {
    return <p>Programările sunt disponibile pentru conturile de cetățean.</p>;
  }

  const selectedType = types.find(type => type.code === selectedCode);
  const firstSelectableDate = todayInRomania();
  const lastSelectableDate = selectedType
    ? addDays(firstSelectableDate, selectedType.maxDaysAhead)
    : undefined;
  const previousMonth = addMonths(calendarMonth, -1);
  const nextMonth = addMonths(calendarMonth, 1);
  const canGoPrevious = dateKey(previousMonth.year, previousMonth.month, 1) >=
    dateKey(Number(firstSelectableDate.slice(0, 4)), Number(firstSelectableDate.slice(5, 7)) - 1, 1);
  const canGoNext = !lastSelectableDate ||
    dateKey(nextMonth.year, nextMonth.month, 1) <= lastSelectableDate;
  const availableDates = new Set(calendarSlots.map(slot =>
    dateInRomania(new Date(slot.startsAt)),
  ));

  return (
    <div className="space-y-8">
      {message && <p role="status" className="rounded-xl bg-emerald-50 p-4 text-emerald-800">{message}</p>}
      {error && <ApiErrorPanel error={error} />}

      <section className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <h2 className="text-xl font-semibold">Solicită o programare</h2>
        {types.length === 0 ? (
          <p className="mt-4 text-slate-600">Nu există tipuri de programare active.</p>
        ) : (
          <div className="mt-5 grid gap-4 md:grid-cols-2">
            <label className="text-sm font-medium">
              Tipul programării
              <select
                value={selectedCode}
                onChange={event => {
                  setSelectedCode(event.target.value);
                  setSelectedDate("");
                  setCalendarMonth(monthFor(todayInRomania()));
                }}
                className="mt-2 w-full rounded-lg border border-slate-300 p-3"
              >
                {types.map(type => <option key={type.code} value={type.code}>{type.name}</option>)}
              </select>
            </label>
            <section className="rounded-xl border border-slate-200 p-4 md:col-span-2" aria-labelledby="appointment-calendar-title">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <h3 id="appointment-calendar-title" className="font-medium">Alege data programării</h3>
                  <p className="mt-1 text-sm text-slate-600">Alege o zi marcată pentru a vedea intervalele libere din următoarele {selectedType?.maxDaysAhead ?? 0} zile.</p>
                </div>
                <div className="flex gap-2">
                  <button type="button" onClick={() => setCalendarMonth(previousMonth)} disabled={!canGoPrevious} aria-label="Luna anterioară" className="rounded-lg border border-slate-300 px-3 py-2 disabled:cursor-not-allowed disabled:opacity-50">←</button>
                  <button type="button" onClick={() => setCalendarMonth(nextMonth)} disabled={!canGoNext} aria-label="Luna următoare" className="rounded-lg border border-slate-300 px-3 py-2 disabled:cursor-not-allowed disabled:opacity-50">→</button>
                </div>
              </div>
              <p className="mt-4 text-center font-semibold capitalize">{monthLabel(calendarMonth)}</p>
              <div className="mt-3 grid grid-cols-7 gap-1 text-center text-xs font-medium text-slate-500" aria-hidden="true">
                {['L', 'Ma', 'Mi', 'J', 'V', 'S', 'D'].map(day => <span key={day}>{day}</span>)}
              </div>
              <div className="mt-1 grid grid-cols-7 gap-1" role="grid" aria-label="Calendar intervale disponibile">
                {daysInCalendar(calendarMonth).map(day => {
                  const selectable = day.inCurrentMonth && day.key >= firstSelectableDate &&
                    (!lastSelectableDate || day.key <= lastSelectableDate) &&
                    availableDates.has(day.key);
                  const selected = day.key === selectedDate;
                  return (
                    <button key={day.key} type="button" role="gridcell" disabled={!selectable}
                      onClick={() => { setSelectedDate(day.key); setSelectedSlot(""); }}
                      aria-pressed={selected}
                      className={`min-h-10 rounded-lg text-sm transition ${selected ? "bg-blue-700 font-semibold text-white" : selectable ? "bg-slate-100 text-slate-900 hover:bg-blue-100" : "text-slate-300"}`}>
                      {day.day}
                    </button>
                  );
                })}
              </div>
              {calendarSlotsLoading && <p className="mt-3 text-sm text-slate-600">Se verifică intervalele libere…</p>}
              {!calendarSlotsLoading && calendarSlots.length === 0 && <p className="mt-3 text-sm text-slate-600">Nu există intervale libere pentru acest tip de programare în perioada configurată.</p>}
              <p className="mt-3 text-sm text-slate-600">Data selectată: <span className="font-medium">{selectedDate ? new Intl.DateTimeFormat("ro-RO", { dateStyle: "long", timeZone: "Europe/Bucharest" }).format(new Date(`${selectedDate}T12:00:00Z`)) : "niciuna"}</span></p>
            </section>
            <label className="text-sm font-medium">
              Interval disponibil
              <select
                value={selectedSlot}
                onChange={event => setSelectedSlot(event.target.value)}
                className="mt-2 w-full rounded-lg border border-slate-300 p-3"
                disabled={slotsLoading || slots.length === 0}
              >
                <option value="">{slotsLoading ? "Se încarcă…" : "Alege un interval"}</option>
                {slots.map(slot => <option key={slot.id} value={slot.id}>{formatDate(slot.startsAt)} – {new Date(slot.endsAt).toLocaleTimeString("ro-RO", { hour: "2-digit", minute: "2-digit", timeZone: "Europe/Bucharest" })}</option>)}
              </select>
              {!slotsLoading && slots.length === 0 && (
                <span className="mt-2 block text-sm font-normal text-slate-600">
                  Nu există intervale disponibile în perioada configurată. Se pot
                  solicita doar intervale din următoarele {types.find(
                    type => type.code === selectedCode,
                  )?.maxDaysAhead ?? 0} zile.
                </span>
              )}
            </label>
            <label className="text-sm font-medium md:col-span-2">
              Observații (opțional)
              <textarea value={notes} onChange={event => setNotes(event.target.value)} maxLength={1000} rows={3} className="mt-2 w-full rounded-lg border border-slate-300 p-3" />
            </label>
            <button type="button" onClick={() => void book()} disabled={busy || !selectedSlot} className="w-fit rounded-lg bg-blue-700 px-4 py-2 font-medium text-white hover:bg-blue-800 disabled:opacity-50">
              {busy ? "Se procesează…" : "Solicită programarea"}
            </button>
          </div>
        )}
      </section>

      <section>
        <h2 className="text-xl font-semibold">Programările mele</h2>
        {appointments.length === 0 ? (
          <p className="mt-4 rounded-xl border border-slate-200 bg-white p-6 text-slate-600">Nu ai programări.</p>
        ) : (
          <div className="mt-4 grid gap-4">
            {appointments.map(appointment => (
              <article key={appointment.id} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <h3 className="font-semibold">{appointment.appointmentTypeName}</h3>
                    <p className="mt-1 text-sm text-slate-600">{formatDate(appointment.startsAt)}</p>
                    <p className="mt-1 text-xs text-slate-500">Cod: {appointment.referenceCode}</p>
                  </div>
                  <span className="rounded-full bg-slate-100 px-3 py-1 text-sm">{statusLabel(appointment.status)}</span>
                </div>
                {appointment.decisionNote && <p className="mt-3 text-sm text-slate-600">Motiv: {appointment.decisionNote}</p>}
                {(appointment.status === "Requested" || appointment.status === "Confirmed") && (
                  <button type="button" disabled={busy} onClick={() => void cancel(appointment.id)} className="mt-4 rounded-lg border border-red-300 px-3 py-2 text-sm text-red-700 hover:bg-red-50 disabled:opacity-50">Anulează</button>
                )}
              </article>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
