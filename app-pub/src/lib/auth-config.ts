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
  title: "Contul meu",
  sectionLabel: "Portal de servicii",
  loginDescription: "Autentifică-te în contul tău.",
  profilePath: "/api/auth/me",
  allowedRoles: ["Citizen"],
  forbiddenMessage: "Emailul sau parola sunt incorecte.",
  lockName: "portal-public-auth-session",
  channelName: "portal-public-auth-events",
};
