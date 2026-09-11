/** Tags Sentry com tenant_slug (RN / E1 §3.2). No-op ate @sentry/nextjs. */
let currentTenantSlug: string | null = null;

export function setSentryTenantSlug(tenantSlug: string | null | undefined) {
  currentTenantSlug = tenantSlug ?? null;
  // Quando Sentry estiver ligado: Sentry.setTag("tenant_slug", currentTenantSlug)
}

export function getSentryTenantTags(tenantSlug?: string | null) {
  return {
    tenant_slug: tenantSlug ?? currentTenantSlug ?? "unknown",
  };
}
