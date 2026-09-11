import { ImageResponse } from "next/og";

export const runtime = "nodejs";

/** OG image polido (@vercel/og via next/og) — E1 §3.6. */
export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const title = searchParams.get("title") ?? "Prata";
  const subtitle = searchParams.get("subtitle") ?? "Portfolio editorial";

  return new ImageResponse(
    (
      <div
        style={{
          height: "100%",
          width: "100%",
          display: "flex",
          flexDirection: "column",
          justifyContent: "flex-end",
          background: "linear-gradient(145deg, #1c1917 0%, #44403c 55%, #78716c 100%)",
          color: "#fafaf9",
          padding: 72,
        }}
      >
        <div style={{ fontSize: 28, letterSpacing: 6, textTransform: "uppercase", opacity: 0.7 }}>
          Prata
        </div>
        <div style={{ fontSize: 72, fontFamily: "Georgia, serif", marginTop: 16, lineHeight: 1.1 }}>
          {title}
        </div>
        <div style={{ fontSize: 28, marginTop: 20, opacity: 0.85 }}>{subtitle}</div>
      </div>
    ),
    { width: 1200, height: 630 },
  );
}
