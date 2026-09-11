"use client";

import { useEffect } from "react";
import * as Sentry from "@sentry/nextjs";
import { setSentryTenantSlug, getSentryTenantTags } from "@/lib/sentry-tags";

/** Liga tenant_slug nas tags Sentry e observa LCP (Web Vitals) — E1 §3.2. */
export function SentryTenantProvider({
  tenantSlug,
  children,
}: {
  tenantSlug?: string | null;
  children: React.ReactNode;
}) {
  useEffect(() => {
    setSentryTenantSlug(tenantSlug);
    const tags = getSentryTenantTags(tenantSlug);
    Sentry.setTag("tenant_slug", tags.tenant_slug);

    let observer: PerformanceObserver | undefined;
    try {
      observer = new PerformanceObserver((list) => {
        for (const entry of list.getEntries()) {
          if (entry.entryType === "largest-contentful-paint") {
            Sentry.setMeasurement("LCP", entry.startTime, "millisecond");
            Sentry.addBreadcrumb({
              category: "web-vital",
              message: "LCP",
              data: { value: entry.startTime, tenant_slug: tags.tenant_slug },
            });
          }
        }
      });
      observer.observe({ type: "largest-contentful-paint", buffered: true });
    } catch {
      // browsers antigos
    }

    return () => observer?.disconnect();
  }, [tenantSlug]);

  return <>{children}</>;
}
