import { revalidateTag } from "next/cache";
import { NextRequest, NextResponse } from "next/server";

/** ISR por tag (RN-VIT-004) — chamado pelo outbox apos ColecaoPublicada. */
export async function POST(request: NextRequest) {
  const secret = process.env.REVALIDATE_SECRET ?? "CHANGE_ME";
  const presented = request.headers.get("X-Prata-Revalidate");
  if (!presented || presented !== secret || secret === "CHANGE_ME") {
    return NextResponse.json({ ok: false }, { status: 401 });
  }

  const body = (await request.json().catch(() => ({}))) as {
    slug?: string;
    tags?: string[];
  };
  const tags = body.tags ?? (body.slug ? [`portfolio:${body.slug}`] : ["portfolio"]);
  for (const tag of tags) {
    revalidateTag(tag, "max");
  }
  return NextResponse.json({ revalidated: true, tags });
}
