import Link from "next/link";

import { FaTelegramPlane } from "react-icons/fa";

import { requestUpgradeAction } from "@/actions/billing";
import { FlashMessage } from "@/components/app/flash-message";
import { MetricCard } from "@/components/app/metric-card";
import { PageSectionNav } from "@/components/app/page-section-nav";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { getQueryMessage } from "@/lib/action-state";
import { getBillingStatus } from "@/lib/financebot-api";
import { requireSession } from "@/lib/session";
import { formatDateTime } from "@/lib/utils/format";

type BillingPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

const billingSections = [
  { id: "status", label: "Status do plano", description: "Estado atual, quota e jornada comercial." },
  { id: "premium", label: "Premium e bot", description: "Valor entregue hoje e uso no Telegram." },
] as const;

type BillingSection = (typeof billingSections)[number]["id"];

function getJourneyCopy(billing: Awaited<ReturnType<typeof getBillingStatus>>) {
  if (billing.upgradePendente) {
    return {
      title: "Pedido de upgrade registrado",
      description:
        "Sua conta ja entrou no fluxo comercial do Premium. Agora o estado fica pendente ate a etapa de ativacao real.",
      badge: "Upgrade pendente",
      badgeVariant: "secondary" as const,
      steps: [
        "Seu pedido ja foi salvo e nao precisa ser reenviado.",
        "Voce continua acompanhando por aqui ou com /plano no Telegram.",
        "Quando a ativacao comercial entrar, esse fluxo podera ser concluido sem perder o contexto.",
      ],
    };
  }

  if (billing.trialAtivo) {
    return {
      title: "Seu trial Premium esta ativo",
      description:
        billing.diasRestantesTrial === 1
          ? "Voce ainda tem 1 dia de acesso Premium para experimentar o relatorio mensal e o fluxo sem limite."
          : `Voce ainda tem ${billing.diasRestantesTrial ?? 0} dias de acesso Premium para experimentar o produto completo.`,
      badge: "Trial ativo",
      badgeVariant: "default" as const,
      steps: [
        "Use o relatorio mensal enquanto o acesso Premium estiver liberado.",
        "Se fizer sentido continuar, registre o pedido de upgrade antes do fim do trial.",
        "O pedido tambem pode ser feito no bot com /upgrade ou /assinar.",
      ],
    };
  }

  if (billing.planoEfetivo === "Premium") {
    return {
      title: "Seu acesso Premium esta ativo",
      description:
        "Voce ja esta no estado Premium e pode seguir usando o relatorio mensal, o dashboard e o Telegram sem a urgencia comercial do trial.",
      badge: "Premium ativo",
      badgeVariant: "default" as const,
      steps: [
        "Acompanhe seus numeros pelo dashboard e pelo /relatorio no Telegram.",
        "Seu limite mensal de lancamentos permanece liberado.",
        "Use /plano no bot sempre que quiser revisar esse estado.",
      ],
    };
  }

  return {
    title: "Desbloqueie o Premium",
    description:
      "O Premium concentra o valor comercial mais forte do produto hoje: relatorio mensal liberado e continuidade sem o teto mensal do plano Free.",
    badge: "Plano Free",
    badgeVariant: "outline" as const,
    steps: [
      "Registre seu pedido de upgrade agora para entrar no fluxo comercial.",
      "Mantenha o relatorio mensal liberado quando o acesso Premium estiver ativo.",
      "Use Web ou Telegram para acompanhar esse pedido sem depender de um unico canal.",
    ],
  };
}

export default async function BillingPage({ searchParams }: BillingPageProps) {
  const session = await requireSession();
  const params = await searchParams;
  const error = getQueryMessage(params.error);
  const success = getQueryMessage(params.success);
  const activeSection = resolveBillingSection(getSingleValue(params.secao));
  const billing = await getBillingStatus(session.token);
  const journey = getJourneyCopy(billing);

  const currentStateLabel = billing.upgradePendente
    ? "Pendente"
    : billing.trialAtivo
      ? "Trial"
      : billing.planoEfetivo;

  return (
    <div className="space-y-6">
      {error ? (
        <FlashMessage title="Algo deu errado" message={error} variant="destructive" />
      ) : null}
      {success ? <FlashMessage title="Tudo certo" message={success} /> : null}

      <PageSectionNav
        items={billingSections.map((section) => ({
          href: buildBillingPath(section.id),
          label: section.label,
          description: section.description,
          active: section.id === activeSection,
        }))}
      />

      {activeSection === "status" ? (
        <section className="grid gap-6 lg:grid-cols-[1.6fr_1fr]">
        <Card className="border-primary/20 bg-primary/5">
          <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div className="space-y-1">
              <CardTitle>{journey.title}</CardTitle>
              <CardDescription>{journey.description}</CardDescription>
            </div>
            <Badge variant={journey.badgeVariant}>{journey.badge}</Badge>
          </CardHeader>
          <CardContent className="space-y-5">
            <div className="grid gap-4 sm:grid-cols-3">
              <MetricCard title="Plano efetivo" value={billing.planoEfetivo} />
              <MetricCard title="Assinatura" value={billing.statusAssinatura} />
              <MetricCard title="Estado atual" value={currentStateLabel} />
            </div>

            <div className="space-y-3">
              <p className="text-sm font-medium">Proximos passos</p>
              <ol className="list-decimal space-y-2 pl-5 text-sm text-muted-foreground">
                {journey.steps.map((step) => (
                  <li key={step}>{step}</li>
                ))}
              </ol>
            </div>

              <div className="flex flex-col gap-3 sm:flex-row">
                {billing.podeSolicitarUpgrade ? (
                  <form action={requestUpgradeAction}>
                    <input type="hidden" name="redirectTo" value={buildBillingPath("status")} />
                    <Button type="submit">Solicitar upgrade para Premium</Button>
                  </form>
                ) : null}
              <Link href="/dashboard" className={buttonVariants({ variant: "outline" })}>
                Abrir dashboard
              </Link>
              <Link href="/telegram" className={`${buttonVariants({ variant: "outline" })} app-telegram-button`}>
                <FaTelegramPlane className="size-4" />
                Abrir Telegram
              </Link>
            </div>

            <div className="rounded-lg border border-dashed p-4 text-sm text-muted-foreground">
              Transparencia do fluxo: por enquanto, o pedido de upgrade fica registrado e o estado
              comercial aparece em todos os canais. A ativacao real segue reservada para a fase de
              checkout.
            </div>
          </CardContent>
        </Card>

        <Card className="h-fit">
          <CardHeader>
            <CardTitle>Resumo do plano</CardTitle>
            <CardDescription>Leitura rapida do que ja esta ativo na sua conta.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4 text-sm">
            <div className="flex items-center justify-between">
              <span className="text-muted-foreground">Plano cadastrado</span>
              <span>{billing.planoAtual}</span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-muted-foreground">Plano efetivo</span>
              <span>{billing.planoEfetivo}</span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-muted-foreground">Lançamentos no mes</span>
              <span>
                {billing.lancamentosNoMesAtual}
                {billing.limiteLancamentosNoMesAtual
                  ? ` / ${billing.limiteLancamentosNoMesAtual}`
                  : " / ilimitado"}
              </span>
            </div>
            {billing.trialAteUtc ? (
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Trial ate</span>
                <span>{formatDateTime(billing.trialAteUtc)}</span>
              </div>
            ) : null}
            {billing.upgradeSolicitadoEmUtc ? (
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Upgrade solicitado em</span>
                <span>{formatDateTime(billing.upgradeSolicitadoEmUtc)}</span>
              </div>
            ) : null}
            <div className="rounded-lg border border-dashed p-3 text-muted-foreground">
              {billing.mensagemStatus}
            </div>
            {billing.mensagemUpgrade ? (
              <div className="rounded-lg border border-dashed p-3 text-muted-foreground">
                {billing.mensagemUpgrade}
              </div>
            ) : null}
          </CardContent>
        </Card>
        </section>
      ) : null}

      {activeSection === "premium" ? (
        <section className="grid gap-6 lg:grid-cols-[1.4fr_1fr]">
        <Card>
          <CardHeader>
            <CardTitle>O que o Premium entrega hoje</CardTitle>
            <CardDescription>
              A proposta comercial fica centrada no que ja existe de verdade no produto.
            </CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-2">
            <div className="rounded-lg border p-4">
              <p className="font-medium">Relatorio mensal liberado</p>
              <p className="mt-2 text-sm text-muted-foreground">
                Acompanhe saldo, entradas, saidas e top categorias sem sair do dashboard ou do
                comando /relatorio no Telegram.
              </p>
            </div>
            <div className="rounded-lg border p-4">
              <p className="font-medium">Lancamentos sem teto mensal</p>
              <p className="mt-2 text-sm text-muted-foreground">
                O plano Premium remove o limite mensal que hoje protege o plano Free.
              </p>
            </div>
            <div className="rounded-lg border p-4">
              <p className="font-medium">Continuidade apos o trial</p>
              <p className="mt-2 text-sm text-muted-foreground">
                O pedido de upgrade evita perder o contexto comercial quando voce decidir seguir no
                Premium.
              </p>
            </div>
            <div className="rounded-lg border p-4">
              <p className="font-medium">Mesmo fluxo em Web e Telegram</p>
              <p className="mt-2 text-sm text-muted-foreground">
                O pedido pode ser acompanhado pelo painel ou pelos comandos /plano e /upgrade no
                bot.
              </p>
            </div>
          </CardContent>
        </Card>

        <Card className="h-fit">
          <CardHeader>
            <CardTitle>Comandos uteis no Telegram</CardTitle>
            <CardDescription>Use o bot como extensao natural do painel Web.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-3 text-sm text-muted-foreground">
            <div className="rounded-lg border px-4 py-3">
              <p className="font-medium text-foreground">/plano</p>
              <p className="mt-1">Revisa quota, trial, estado do upgrade e mensagens comerciais.</p>
            </div>
            <div className="rounded-lg border px-4 py-3">
              <p className="font-medium text-foreground">/upgrade</p>
              <p className="mt-1">Registra o pedido de upgrade sem sair do Telegram.</p>
            </div>
            <div className="rounded-lg border px-4 py-3">
              <p className="font-medium text-foreground">/relatorio</p>
              <p className="mt-1">Entrega o consolidado mensal quando o acesso Premium estiver ativo.</p>
            </div>
          </CardContent>
        </Card>
        </section>
      ) : null}
    </div>
  );
}

function getSingleValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] ?? "" : value ?? "";
}

function resolveBillingSection(value: string): BillingSection {
  return billingSections.some((section) => section.id === value)
    ? (value as BillingSection)
    : "status";
}

function buildBillingPath(section: BillingSection) {
  if (section === "status") {
    return "/plano";
  }

  const params = new URLSearchParams();
  params.set("secao", section);
  return `/plano?${params.toString()}`;
}
