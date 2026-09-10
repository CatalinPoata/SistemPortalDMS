import "client-only";

import { authenticatedRequest } from "@/lib/auth-store";
import { request } from "@/lib/http";

export type AppointmentStatus =
  | "Requested"
  | "Confirmed"
  | "Rejected"
  | "Cancelled"
  | "Completed"
  | "NoShow";

export type AppointmentType = {
  id: string;
  code: string;
  name: string;
  description: string | null;
  location: string | null;
  durationMinutes: number;
  requiresConfirmation: boolean;
  maxDaysAhead: number;
  isActive: boolean;
};

export type AppointmentSlot = {
  id: string;
  appointmentTypeId: string;
  startsAt: string;
  endsAt: string;
  capacity: number;
  bookedCount: number;
  isBlocked: boolean;
};

export type Appointment = {
  id: string;
  slotId: string;
  appointmentTypeCode: string;
  appointmentTypeName: string;
  startsAt: string;
  endsAt: string;
  status: AppointmentStatus;
  notes: string | null;
  decisionNote: string | null;
  referenceCode: string;
};

type Paged<T> = {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
};

export function getAppointmentTypes() {
  return request<Paged<AppointmentType>>(
    "/api/public/appointment-types?page=1&pageSize=100",
  );
}

export function getAppointmentSlots(code: string, date?: string) {
  const query = new URLSearchParams({ page: "1", pageSize: "100" });
  if (date) query.set("date", date);

  return request<Paged<AppointmentSlot>>(
    `/api/public/appointment-types/${encodeURIComponent(code)}/slots?${query}`,
  );
}

export function createAppointment(
  code: string,
  slotId: string,
  notes: string,
) {
  return authenticatedRequest<Appointment>(
    `/api/public/appointment-types/${encodeURIComponent(code)}/appointments`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ slotId, notes: notes.trim() || null }),
    },
  );
}

export function getMyAppointments() {
  return authenticatedRequest<Appointment[]>("/api/appointments/mine");
}

export function cancelAppointment(id: string) {
  return authenticatedRequest<Appointment>(
    `/api/appointments/${encodeURIComponent(id)}/cancel`,
    { method: "POST" },
  );
}
