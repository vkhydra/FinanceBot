import Link from "next/link";

import { logoutAction } from "@/actions/auth";
import { Button } from "@/components/ui/button";
import { Separator } from "@/components/ui/separator";

type ShellHeaderProps = {
  email: string;
};

export function ShellHeader({ email }: ShellHeaderProps) {
  return (
    <header className="border-b bg-background/95 backdrop-blur">
      <div className="mx-auto flex w-full max-w-6xl items-center justify-between gap-6 px-6 py-4">
        <div>
          <Link href="/dashboard" className="text-lg font-semibold tracking-tight">
            FinanceBot Web
          </Link>
          <p className="text-sm text-muted-foreground">
            Sessao ativa como {email}
          </p>
        </div>
        <div className="flex items-center gap-4">
          <nav className="hidden items-center gap-4 text-sm text-muted-foreground md:flex">
            <Link href="/dashboard" className="transition-colors hover:text-foreground">
              Dashboard
            </Link>
            <Link href="/lancamentos" className="transition-colors hover:text-foreground">
              Lancamentos
            </Link>
            <Link href="/plano" className="transition-colors hover:text-foreground">
              Plano
            </Link>
            <Link href="/telegram" className="transition-colors hover:text-foreground">
              Telegram
            </Link>
          </nav>
          <Separator orientation="vertical" className="hidden h-8 md:block" />
          <form action={logoutAction}>
            <Button type="submit" variant="outline">
              Sair
            </Button>
          </form>
        </div>
      </div>
    </header>
  );
}
