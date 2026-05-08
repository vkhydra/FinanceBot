import Link from "next/link";

import { ChartSpline, Menu, MessageCircleMore, ShieldCheck, WalletCards } from "lucide-react";

import { logoutAction } from "@/actions/auth";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";
import { ThemeToggle } from "@/components/app/theme-toggle";
import { buttonVariants } from "@/components/ui/button";

type ShellHeaderProps = {
  email: string;
};

export function ShellHeader({ email }: ShellHeaderProps) {
  const links = [
    { href: "/dashboard", label: "Dashboard", icon: ChartSpline },
    { href: "/lancamentos", label: "Lancamentos", icon: WalletCards },
    { href: "/plano", label: "Plano", icon: ShieldCheck },
    { href: "/telegram", label: "Telegram", icon: MessageCircleMore },
  ];

  return (
    <header className="sticky top-0 z-40 border-b border-border/70 bg-background/90 backdrop-blur">
      <div className="mx-auto flex w-full max-w-7xl items-center justify-between gap-3 px-4 py-3 sm:px-6 lg:px-8">
        <div className="min-w-0">
          <Link href="/dashboard" className="inline-flex items-center gap-2 text-base font-semibold tracking-tight">
            <span className="flex size-8 items-center justify-center rounded-xl bg-primary/12 text-primary">
              <WalletCards className="size-4" />
            </span>
            <span className="truncate">FinanceBot</span>
          </Link>
          <p className="mt-1 truncate text-sm text-muted-foreground">{email}</p>
        </div>
        <div className="flex items-center gap-2">
          <nav className="hidden items-center gap-5 text-sm text-muted-foreground lg:flex">
            {links.map(({ href, label, icon: Icon }) => (
              <Link
                key={href}
                href={href}
                className="inline-flex items-center gap-2 transition-colors hover:text-foreground"
              >
                <Icon className="size-4" />
                {label}
              </Link>
            ))}
          </nav>
          <ThemeToggle />
          <Separator orientation="vertical" className="hidden h-8 lg:block" />
          <form action={logoutAction}>
            <Button type="submit" variant="outline" className="hidden border-border/70 bg-background/75 sm:inline-flex">
              Sair
            </Button>
          </form>
          <details className="relative lg:hidden">
            <summary
              className={`${buttonVariants({ variant: "outline", size: "icon" })} list-none border-border/70 bg-background/75 [&::-webkit-details-marker]:hidden`}
            >
              <Menu className="size-4" />
            </summary>
            <div className="absolute right-0 top-12 z-50 w-72 rounded-2xl border border-border/70 bg-popover p-3 shadow-xl">
              <div className="rounded-2xl border border-border/60 bg-card/80 px-4 py-3">
                <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Sessao ativa</p>
                <p className="mt-1 truncate font-medium">{email}</p>
              </div>
              <nav className="mt-3 grid gap-2">
                {links.map(({ href, label, icon: Icon }) => (
                  <Link
                    key={href}
                    href={href}
                    className="inline-flex items-center gap-3 rounded-2xl border border-transparent px-3 py-3 text-sm text-muted-foreground transition-colors hover:border-border/60 hover:bg-muted/60 hover:text-foreground"
                  >
                    <Icon className="size-4" />
                    {label}
                  </Link>
                ))}
              </nav>
              <form action={logoutAction} className="mt-3">
                <Button type="submit" variant="outline" className="w-full border-border/70 bg-background/75">
                  Sair
                </Button>
              </form>
            </div>
          </details>
        </div>
      </div>
    </header>
  );
}
