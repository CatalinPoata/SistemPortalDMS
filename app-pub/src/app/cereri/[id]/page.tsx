import { connection } from "next/server";

import PortalHeader from "@/components/portal-header";
import { SubmissionDetail } from "@/components/submission-view";

export default async function SubmissionPage(
  props: PageProps<"/cereri/[id]">,
) {
  await connection();
  const { id } = await props.params;

  return (
    <div className="min-h-screen bg-slate-50 text-slate-950">
      <PortalHeader />
      <main className="mx-auto max-w-4xl px-4 py-10 sm:px-6">
        <SubmissionDetail id={id} />
      </main>
    </div>
  );
}
