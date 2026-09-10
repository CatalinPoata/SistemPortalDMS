import "client-only";

import { apiRequest } from "@/lib/auth-store";

export type PortalUserRole = "Citizen" | "Admin";

export type PortalUser = {
  id: string;
  email: string;
  fullName: string;
  role: PortalUserRole;
  emailConfirmed: boolean;
  isActive: boolean;
  createdAt: string;
  updatedAt: string | null;
};

export type PagedUsers = {
  items: PortalUser[];
  page: number;
  pageSize: number;
  total: number;
};

export function getUsers(page: number, search: string) {
  const query = new URLSearchParams({
    page: String(page),
    pageSize: "25",
  });

  if (search.trim()) query.set("search", search.trim());

  return apiRequest<PagedUsers>(`/api/users?${query.toString()}`);
}

export function setUserActive(id: string, isActive: boolean) {
  return apiRequest<PortalUser>(`/api/users/${encodeURIComponent(id)}/active`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ isActive }),
  });
}

export function setUserRole(id: string, role: PortalUserRole) {
  return apiRequest<PortalUser>(`/api/users/${encodeURIComponent(id)}/role`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ role }),
  });
}
