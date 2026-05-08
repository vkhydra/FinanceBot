import type { Metadata } from "next";
import Link from "next/link";
import { redirect } from "next/navigation";
import { ArrowRight, Bot, ChartColumnIncreasing, ShieldCheck, WalletCards } from "lucide-react";

import { ThemeToggle } from "@/components/app/theme-toggle";
import { buttonVariants } from "@/components/ui/button";
import { getSession } from "@/lib/session";

export const metadata: Metadata = {
  title: "FinanceBot | Controle financeiro no Telegram e na Web",
  description:
    "Acompanhe gastos e receitas na Web e no Telegram com uma interface mais direta e objetiva.",
};

const featureCards = [
  {
    icon: WalletCards,
    title: "Registre em segundos",
    description:
      "Lance gastos e receitas pela Web ou com mensagens naturais no Telegram, sem fluxo burocratico.",
  },
  {
    icon: ChartColumnIncreasing,
    title: "Leia seu dinheiro melhor",
      description: "Veja resumo do dia, extrato e leitura do seu mes sem depender de planilhas.",
  },
  {
    icon: ShieldCheck,
    title: "Tudo por usuario",
    description:
      "Conta autenticada, vinculo por codigo no Telegram e isolamento dos dados para cada pessoa.",
  },
];

const onboardingSteps = [
  "Crie sua conta e entre pela Web.",
  "Conecte o Telegram com um codigo de vinculo.",
  "Acompanhe o resumo e opere o extrato sem depender de planilhas.",
];

export default async function HomePage() {
  const session = await getSession();
  if (session) {
    redirect("/dashboard");
  }

  return (
    <main className="app-shell">
      <div className="mx-auto flex min-h-screen w-full max-w-6xl flex-col px-4 py-6 sm:px-6 lg:px-8">
        <header className="flex items-center justify-between gap-4 py-2">
          <div className="flex items-center gap-3">
            <div className="flex size-10 items-center justify-center rounded-xl bg-primary/12 text-primary">
              <Bot className="size-5" />
            </div>
            <div>
              <p className="text-sm font-semibold tracking-[0.2em] text-muted-foreground uppercase">
                FinanceBot
              </p>
              <p className="text-sm text-muted-foreground">
                Controle financeiro pessoal na Web e no Telegram
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2">
            <ThemeToggle />
            <Link href="/login" className={buttonVariants({ variant: "outline" })}>
              Entrar
            </Link>
            <Link href="/register" className="hidden sm:inline-flex sm:text-sm sm:font-medium sm:text-muted-foreground">
              Criar conta
            </Link>
          </div>
        </header>

        <section className="app-panel mt-8 grid gap-6 px-6 py-6 sm:px-8 lg:grid-cols-[1.1fr_0.9fr] lg:items-start">
          <div className="space-y-4">
            <p className="text-xs font-medium uppercase tracking-[0.24em] text-muted-foreground">
              Controle financeiro pessoal
            </p>
            <h1 className="max-w-3xl text-3xl font-semibold tracking-tight sm:text-4xl">
              Registre e acompanhe seu dinheiro sem excesso de tela.
            </h1>
            <p className="max-w-2xl text-sm leading-6 text-muted-foreground sm:text-base">
              Use a Web para operar o extrato e o Telegram para registrar rapido no dia a dia.
            </p>
            <div className="flex flex-col gap-3 sm:flex-row">
              <Link href="/register" className={buttonVariants({ variant: "default", size: "lg" })}>
                Criar conta
                <ArrowRight className="size-4" />
              </Link>
              <Link href="/login" className={buttonVariants({ variant: "outline", size: "lg" })}>
                Entrar
              </Link>
            </div>
          </div>

          <div className="grid gap-3">
            {featureCards.map(({ icon: Icon, title, description }) => (
              <div key={title} className="app-data-row flex items-start gap-3 p-4">
                <div className="flex size-9 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary">
                  <Icon className="size-4" />
                </div>
                <div className="space-y-1">
                  <p className="font-medium">{title}</p>
                  <p className="text-sm text-muted-foreground">{description}</p>
                </div>
              </div>
            ))}
          </div>
        </section>

        <section className="mt-6 grid gap-4 lg:grid-cols-[0.9fr_1.1fr]">
          <div className="app-panel p-5">
            <p className="text-sm font-semibold">Como comecar</p>
            <div className="mt-4 space-y-3">
              {onboardingSteps.map((step, index) => (
                <div key={step} className="flex items-start gap-3">
                  <span className="mt-0.5 flex size-6 shrink-0 items-center justify-center rounded-full bg-primary/12 text-xs font-semibold text-primary">
                    {index + 1}
                  </span>
                  <p className="text-sm text-muted-foreground">{step}</p>
                </div>
              ))}
            </div>
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            <div className="app-panel p-5">
              <p className="text-sm font-semibold">Dashboard</p>
              <p className="mt-2 text-sm text-muted-foreground">Resumo do dia e leitura do mes.</p>
            </div>
            <div className="app-panel p-5">
              <p className="text-sm font-semibold">Lancamentos</p>
              <p className="mt-2 text-sm text-muted-foreground">Extrato com filtros e acoes por item.</p>
            </div>
            <div className="app-panel p-5">
              <p className="text-sm font-semibold">Telegram</p>
              <p className="mt-2 text-sm text-muted-foreground">Registro rapido e consulta objetiva.</p>
            </div>
          </div>
        </section>
      </div>
    </main>
  );
}
