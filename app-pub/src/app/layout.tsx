import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Portal de servicii",
  description: "Portalul de servicii publice.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="ro" className="min-h-full bg-white antialiased">
      <body className="flex min-h-screen flex-col bg-white">
        <div className="flex-1 bg-white">{children}</div>
      </body>
    </html>
  );
}
