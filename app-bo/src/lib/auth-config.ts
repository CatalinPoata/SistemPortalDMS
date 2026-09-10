export type PortalRole = "Citizen" | "Admin";

type AuthAppConfig = {
  title: string;
  sectionLabel: string;
  loginDescription: string;
  profilePath: string;
  allowedRoles: readonly PortalRole[];
  forbiddenMessage: string;
  lockName: string;
  channelName: string;
};

export const roleLabels: Record<PortalRole, string> = {
  Citizen: "Cetățean",
  Admin: "Administrator",
};

export const authConfig: AuthAppConfig = {
  title: "Administrare",
  sectionLabel: "Portal de servicii",
  loginDescription: "Acces pentru administratorii portalului.",
  profilePath: "/api/auth/admin-me",
  allowedRoles: ["Admin"],
  forbiddenMessage:
    "Accesul în backoffice este permis doar administratorilor.",
  lockName: "portal-backoffice-auth-session",
  channelName: "portal-backoffice-auth-events",
};
