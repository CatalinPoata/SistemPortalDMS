import { authenticatedRequest } from "@/lib/auth-store";

export type Notification = {
  id: string;
  subject: string;
  body: string;
  linkUrl: string;
  isRead: boolean;
  createdAt: string;
};

type NotificationList = {
  items: Notification[];
  page: number;
  pageSize: number;
  total: number;
};

export function getNotifications() {
  return authenticatedRequest<NotificationList>("/api/notifications");
}

export function markNotificationRead(id: string) {
  return authenticatedRequest<void>(`/api/notifications/${id}/read`, {
    method: "POST",
  });
}

export function markAllNotificationsRead() {
  return authenticatedRequest<void>("/api/notifications/read-all", {
    method: "POST",
  });
}
