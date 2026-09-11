"use client";

import * as React from "react";
import { cn } from "@/lib/utils";

type ButtonProps = React.ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: "primary" | "ghost";
};

/** Botao shadcn-style (components.json pronto para `npx shadcn@latest add`). */
export function Button({ className, variant = "primary", ...props }: ButtonProps) {
  return (
    <button
      className={cn(
        "inline-flex h-10 items-center justify-center rounded-md px-4 text-sm font-medium transition-colors disabled:opacity-50",
        variant === "primary"
          ? "bg-stone-900 text-stone-50 hover:bg-stone-800"
          : "bg-transparent text-stone-900 hover:bg-stone-100",
        className,
      )}
      {...props}
    />
  );
}
