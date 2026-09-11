/** Avaliacao de VisibleWhen no client (espelha o servidor). */
export function evaluateVisibleWhen(
  expression: string | null | undefined,
  answers: Record<string, Record<string, unknown>>
): boolean {
  if (!expression?.trim()) return true;
  const expr = expression.trim();
  const codes = extractCodes(expr);
  if (codes.length === 0) return true;
  if (!codes.every((c) => c in answers)) return false;

  if (codes.length === 1 && expr.toUpperCase() === codes[0]) return true;

  const parts = expr.split(/\s+/);
  if (parts.length >= 3 && parts[0].toLowerCase().includes(".value")) {
    const code = parts[0].split(".")[0]!.toUpperCase();
    const left = Number(answers[code]?.value ?? answers[code]?.number);
    const right = Number(parts[parts.length - 1]);
    const op = parts.slice(1, -1).join(" ") || parts[1];
    if (Number.isNaN(left) || Number.isNaN(right)) return false;
    switch (op) {
      case ">=":
        return left >= right;
      case "<=":
        return left <= right;
      case ">":
        return left > right;
      case "<":
        return left < right;
      case "==":
      case "=":
        return left === right;
      case "!=":
        return left !== right;
      default:
        return true;
    }
  }

  if (parts.length >= 3 && parts[0].toLowerCase().includes(".option")) {
    const code = parts[0].split(".")[0]!.toUpperCase();
    const actual = String(answers[code]?.option ?? answers[code]?.value ?? "");
    const expected = parts[parts.length - 1]!.replace(/['"]/g, "");
    const op = parts[1];
    return op === "==" || op === "="
      ? actual.toUpperCase() === expected.toUpperCase()
      : actual.toUpperCase() !== expected.toUpperCase();
  }

  return true;
}

function extractCodes(expression: string): string[] {
  const codes: string[] = [];
  for (const token of expression.split(/[\s.><=|&!()]+/)) {
    if (token.length >= 2 && /^[A-Za-z][A-Za-z0-9]*$/.test(token)) {
      const upper = token.toUpperCase();
      if (["VALUE", "NUMBER", "OPTION", "AND", "OR", "TRUE", "FALSE"].includes(upper)) continue;
      if (!codes.includes(upper)) codes.push(upper);
    }
  }
  return codes;
}
