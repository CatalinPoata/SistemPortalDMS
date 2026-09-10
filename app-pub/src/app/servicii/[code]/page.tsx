import PortalHeader from "@/components/portal-header";
import PublicServicePage from "@/components/public-service-page";

export default async function ServicePage(
  props: PageProps<"/servicii/[code]">,
) {
  const { code } = await props.params;

  return (
    <div className="min-h-screen bg-slate-50 text-slate-950">
      <PortalHeader />
      <main className="mx-auto max-w-4xl px-4 py-10 sm:px-6">
        <PublicServicePage code={code} />
      </main>
    </div>
  );
}
