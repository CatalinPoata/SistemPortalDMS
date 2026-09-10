import PortalHeader from "@/components/portal-header";
import ArticlesCatalog from "@/components/articles-catalog";
import HomePublicRegistries from "@/components/home-public-registries";
import HomeSurveys from "@/components/home-surveys";
import ServicesCatalog from "@/components/services-catalog";

export default function Home() {
  return (
    <div className="min-h-screen bg-slate-50 text-slate-950">
      <PortalHeader />

      <main>
        <section className="border-b border-slate-200 bg-gradient-to-br from-blue-950 via-blue-800 to-indigo-700 text-white">
          <div className="mx-auto max-w-6xl px-4 py-16 sm:px-6 sm:py-24">
            <p className="text-sm font-semibold uppercase tracking-[0.2em] text-blue-100">
              Administrație publică online
            </p>

            <h1 className="mt-4 max-w-3xl text-4xl font-bold tracking-tight sm:text-5xl">
              Depune cereri fără drumuri inutile
            </h1>

            <p className="mt-5 max-w-2xl text-lg leading-8 text-blue-100">
              Alege serviciul, completează formularul și urmărește rezolvarea din contul tău.
            </p>
          </div>
        </section>

        <section
          id="servicii"
          aria-labelledby="services-title"
          className="mx-auto max-w-6xl px-4 py-12 sm:px-6"
        >
          <div className="mb-8">
            <p className="text-sm font-semibold uppercase tracking-wider text-blue-700">
              Catalog public
            </p>
            <h2 id="services-title" className="mt-2 text-3xl font-bold">
              Servicii disponibile
            </h2>
          </div>

          <ServicesCatalog />
        </section>

        <section aria-labelledby="surveys-title" className="border-t border-slate-200 bg-white">
          <div className="mx-auto max-w-6xl px-4 py-12 sm:px-6">
            <div className="mb-8 flex flex-wrap items-end justify-between gap-4">
              <div>
                <p className="text-sm font-semibold uppercase tracking-wider text-blue-700">Consultare publică</p>
                <h2 id="surveys-title" className="mt-2 text-3xl font-bold">Chestionare active</h2>
              </div>
              <a href="/chestionare" className="text-sm font-medium text-blue-700 underline">Vezi toate chestionarele</a>
            </div>
            <HomeSurveys />
          </div>
        </section>

        <section
          id="articole"
          aria-labelledby="articles-title"
          className="border-t border-slate-200 bg-white"
        >
          <div className="mx-auto max-w-6xl px-4 py-12 sm:px-6">
            <div className="mb-8">
              <p className="text-sm font-semibold uppercase tracking-wider text-blue-700">
                Informații publice
              </p>
              <h2 id="articles-title" className="mt-2 text-3xl font-bold">
                Articole și anunțuri
              </h2>
            </div>

            <ArticlesCatalog />
          </div>
        </section>

        <section aria-labelledby="registries-title" className="border-t border-slate-200 bg-slate-100">
          <div className="mx-auto max-w-6xl px-4 py-12 sm:px-6">
            <div className="mb-8 flex flex-wrap items-end justify-between gap-4">
              <div>
                <p className="text-sm font-semibold uppercase tracking-wider text-blue-700">Transparență</p>
                <h2 id="registries-title" className="mt-2 text-3xl font-bold">Registre publice</h2>
              </div>
              <a href="/registre" className="text-sm font-medium text-blue-700 underline">Consultă toate registrele</a>
            </div>
            <HomePublicRegistries />
          </div>
        </section>
      </main>
    </div>
  );
}
