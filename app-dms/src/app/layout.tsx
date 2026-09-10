import type { Metadata } from "next";
import { DmsNavigation } from "@/components/dms-navigation";
import "./globals.css";

export const metadata: Metadata = {
  title: "DMS — Registratură",
  description: "Aplicația internă de registratură și documente.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="ro" className="min-h-full bg-white antialiased">
      <body className="flex min-h-screen flex-col bg-white text-slate-950">
        <DmsNavigation />
        <div className="min-h-[calc(100vh-65px)] flex-1 bg-white">{children}</div>
      </body>
    </html>
  );
}
