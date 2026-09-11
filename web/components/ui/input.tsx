import * as React from "react";
import { cn } from "@/lib/utils";

export function Input({ className, ...props }: React.InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      className={cn(
        "flex h-10 w-full rounded-md border border-stone-300 bg-white px-3 text-sm outline-none ring-stone-400 focus:ring-2",
        className,
      )}
      {...props}
    />
  );
}
