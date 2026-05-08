import Link from "next/link";

import { ThemeToggle } from "@/components/app/theme-toggle";

export default function PublicLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <div className="relative flex min-h-screen items-center justify-center bg-muted/40 px-4 py-10">
      <div className="absolute inset-x-0 top-0">
        <div className="mx-auto flex w-full max-w-6xl items-center justify-between px-4 py-5 sm:px-6">
          <Link href="/" className="text-sm font-semibold tracking-[0.2em] text-muted-foreground uppercase">
            FinanceBot
          </Link>
          <ThemeToggle />
        </div>
      </div>

      <div className="w-full max-w-md pt-14">{children}</div>
    </div>
  );
}
