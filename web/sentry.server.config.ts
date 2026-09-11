import * as Sentry from "@sentry/nextjs";

const dsn = process.env.SENTRY_DSN || process.env.NEXT_PUBLIC_SENTRY_DSN;

Sentry.init({
  dsn: dsn && dsn !== "CHANGE_ME" ? dsn : undefined,
  enabled: Boolean(dsn && dsn !== "CHANGE_ME"),
  tracesSampleRate: 0.1,
});
