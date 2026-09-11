import { EditorialMotion } from "@/components/editorial-motion";
import { PublicImmersiveShell } from "@/components/public-immersive-shell";
import { SentryTenantProvider } from "@/components/sentry-tenant-provider";
import { Button } from "@/components/ui/button";

export const revalidate = 60;

export default async function PublicHomePage() {
  const jsonLd = {
    "@context": "https://schema.org",
    "@type": "LocalBusiness",
    name: "Prata",
    url: "https://piloto.prata.app",
  };

  return (
    <SentryTenantProvider tenantSlug="piloto">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }}
      />
      <EditorialMotion />
      <main className="relative min-h-screen overflow-hidden bg-[radial-gradient(ellipse_at_top,_#f7f3eb_0%,_#e8e2d6_45%,_#d4cfc4_100%)] text-stone-900">
        <PublicImmersiveShell />
        <div
          data-parallax
          className="pointer-events-none absolute inset-x-0 top-0 h-[70vh] bg-[url('/hero-grain.svg')] bg-cover opacity-40"
          aria-hidden
        />
        <section className="relative mx-auto flex min-h-screen max-w-4xl flex-col justify-end gap-6 px-6 pb-24 pt-32">
          <p
            data-display
            data-reveal
            className="text-6xl tracking-tight md:text-7xl"
          >
            Prata
          </p>
          <h1 data-reveal className="max-w-xl text-xl text-stone-700 md:text-2xl">
            Portfolio editorial do estudio — tipografia self-hosted, motion leve, DOM primeiro.
          </h1>
          <p data-reveal className="max-w-lg text-stone-600">
            Colecoes publicadas com ISR; SEO, JSON-LD e OG preparados para o fotografo piloto.
          </p>
          <div data-reveal>
            <Button type="button">Ver colecoes</Button>
          </div>
        </section>
      </main>
    </SentryTenantProvider>
  );
}
