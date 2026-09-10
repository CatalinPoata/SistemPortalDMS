import PortalHeader from "@/components/portal-header";
import PublicArticlePage from "@/components/public-article-page";

export default async function ArticlePage(
  props: PageProps<"/articole/[slug]">,
) {
  const { slug } = await props.params;

  return (
    <div className="min-h-screen bg-slate-50 text-slate-950">
      <PortalHeader />
      <main className="mx-auto max-w-4xl px-4 py-10 sm:px-6">
        <PublicArticlePage slug={slug} />
      </main>
    </div>
  );
}
