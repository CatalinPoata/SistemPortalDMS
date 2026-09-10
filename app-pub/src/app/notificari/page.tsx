import NotificationsPage from "@/components/notifications-page";
import PortalHeader from "@/components/portal-header";

export default function NotificationsRoute() {
  return (
    <div className="min-h-screen bg-slate-50 text-slate-950">
      <PortalHeader />
      <main className="mx-auto max-w-4xl px-4 py-10 sm:px-6">
        <NotificationsPage />
      </main>
    </div>
  );
}
