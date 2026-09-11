import * as Sentry from "@sentry/nextjs";

const dsn = process.env.NEXT_PUBLIC_SENTRY_DSN || process.env.SENTRY_DSN;

Sentry.init({
  dsn: dsn && dsn !== "CHANGE_ME" ? dsn : undefined,
  enabled: Boolean(dsn && dsn !== "CHANGE_ME"),
  tracesSampleRate: 0.1,
  enableLogs: true,
});
