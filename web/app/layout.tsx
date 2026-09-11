import type { Metadata } from "next";
import { typePairClassNames } from "@/lib/fonts";
import "./globals.css";

export const metadata: Metadata = {
  metadataBase: new URL(process.env.NEXT_PUBLIC_SITE_URL ?? "https://prata.app"),
  title: "Prata",
  description: "Gestao e entrega fotografica multi-tenant",
  openGraph: {
    title: "Prata",
    description: "Portfolio editorial do estudio",
    images: [{ url: "/api/og?title=Prata" }],
  },
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  const typePair = typePairClassNames("alto-contraste");

  return (
    <html lang="pt-BR" className={`${typePair} h-full antialiased`}>
      <body className="min-h-full flex flex-col bg-stone-50 text-stone-900 font-[family-name:var(--font-body)]">
        {children}
      </body>
    </html>
  );
}
