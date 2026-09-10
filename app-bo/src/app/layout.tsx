import type { Metadata } from "next";
import { BoNavigation } from "@/components/bo-navigation";
import "./globals.css";

export const metadata: Metadata = {
  title: "Administrare — Portal de servicii",
  description: "Administrarea portalului de servicii publice.",
};
export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="ro" className="min-h-full bg-white antialiased">
      <body className="flex min-h-screen flex-col bg-white text-slate-950">
        <BoNavigation />
        <div className="min-h-[calc(100vh-65px)] flex-1 bg-white">{children}</div>
      </body>
    </html>
  );
}
