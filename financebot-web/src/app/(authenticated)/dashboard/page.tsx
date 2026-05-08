import Link from "next/link";
import type { ReactNode } from "react";

import {
  ChartSpline,
} from "lucide-react";

import { requestUpgradeAction } from "@/actions/billing";
import { createGastoAction, createReceitaAction } from "@/actions/finance";
import { FlashMessage } from "@/components/app/flash-message";
import { MetricCard } from "@/components/app/metric-card";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getQueryMessage } from "@/lib/action-state";
import {
  FinanceBotApiError,
  getBillingStatus,
  getMonthlyReport,
  getResumo,
  getUltimosMovimentos,
  listMovimentos,
} from "@/lib/financebot-api";
import { requireSession } from "@/lib/session";
import { formatCurrency, formatDate, formatDateTime, formatPercentage } from "@/lib/utils/format";

const textareaClassName =
  "flex min-h-24 w-full rounded-2xl border border-border/70 bg-background/70 px-3 py-2.5 text-sm shadow-sm outline-none transition-[color,box-shadow,border-color] placeholder:text-muted-foreground focus-visible:border-primary/40 focus-visible:ring-4 focus-visible:ring-primary/10 dark:bg-input/25";

type DashboardPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

type MovimentoItem = Awaited<ReturnType<typeof listMovimentos>>[number];

export default async function DashboardPage({ searchParams }: DashboardPageProps) {
  const session = await requireSession();
  const params = await searchParams;
  const error = getQueryMessage(params.error);
  const success = getQueryMessage(params.success);
  const now = new Date();
  const firstDayOfMonth = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1));
  const monthStart = firstDayOfMonth.toISOString().slice(0, 10);
  const monthEnd = now.toISOString().slice(0, 10);

  const [resumo, movimentosRecentes, billing, movimentosMes] = await Promise.all([
    getResumo(session.token),
    getUltimosMovimentos(session.token),
    getBillingStatus(session.token),
    listMovimentos(session.token, {
      inicio: monthStart,
      fim: monthEnd,
      limite: 200,
    }),
  ]);

  const canAccessMonthlyReport = billing.planoEfetivo === "Premium";
  const showUpgradeJourney =
    billing.podeSolicitarUpgrade || billing.upgradePendente || billing.trialAtivo;
  let monthlyReport = null;
  let monthlyReportError: string | null = null;

  if (canAccessMonthlyReport) {
    try {
      monthlyReport = await getMonthlyReport(session.token);
    } catch (caughtError) {
      monthlyReportError =
        caughtError instanceof FinanceBotApiError
          ? caughtError.message
          : "Nao foi possivel carregar o relatorio mensal.";
    }
  }

  const totalEntradasMes = movimentosMes
    .filter((movimento) => movimento.tipo === "Receita")
    .reduce((sum, movimento) => sum + movimento.valor, 0);
  const totalSaidasMes = movimentosMes
    .filter((movimento) => movimento.tipo === "Gasto")
    .reduce((sum, movimento) => sum + movimento.valor, 0);
  const saldoMes = totalEntradasMes - totalSaidasMes;
  const gastosDoMes = movimentosMes.filter((movimento) => movimento.tipo === "Gasto");
  const categorias = buildCategoryBreakdown(gastosDoMes);
  const dailySeries = buildDailySeries(movimentosMes).slice(-7);
  const originBreakdown = buildOriginBreakdown(movimentosMes);
  const topCategoria = categorias[0];
  const telegramShare = originBreakdown.find((item) => item.label === "Telegram")?.percentage ?? 0;
  const diasComMovimento = new Set(movimentosMes.map((movimento) => movimento.data.slice(0, 10))).size;

  return (
    <div className="space-y-8">
      {error ? (
        <FlashMessage title="Algo deu errado" message={error} variant="destructive" />
      ) : null}
      {success ? <FlashMessage title="Tudo certo" message={success} /> : null}

      <section className="app-panel flex flex-col gap-5 p-5 sm:p-6">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div className="space-y-2">
            <p className="text-xs font-medium uppercase tracking-[0.24em] text-muted-foreground">Dashboard</p>
            <h1 className="text-2xl font-semibold tracking-tight sm:text-3xl">Resumo financeiro</h1>
            <p className="max-w-2xl text-sm text-muted-foreground">
              Veja o saldo do dia, acompanhe o mes atual e use os lancamentos como area principal de operacao.
            </p>
          </div>
          <div className="flex flex-col gap-2 sm:flex-row">
            <Link href="/lancamentos" className={buttonVariants({ variant: "default" })}>
              Abrir lancamentos
            </Link>
            <Link href="/telegram" className={buttonVariants({ variant: "outline" })}>
              Ver Telegram
            </Link>
          </div>
        </div>
        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
          <div className="app-data-row p-4">
            <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Saldo do dia</p>
            <p className="mt-2 text-xl font-semibold tracking-tight">{formatCurrency(resumo.saldo)}</p>
          </div>
          <div className="app-data-row p-4">
            <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Saldo do mes</p>
            <p className="mt-2 text-xl font-semibold tracking-tight">{formatCurrency(saldoMes)}</p>
          </div>
          <div className="app-data-row p-4">
            <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Dias com registro</p>
            <p className="mt-2 text-xl font-semibold tracking-tight">{diasComMovimento}</p>
          </div>
          <div className="app-data-row p-4">
            <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Maior categoria</p>
            <p className="mt-2 text-base font-semibold tracking-tight">{topCategoria ? topCategoria.label : "Sem gastos"}</p>
          </div>
          <div className="app-data-row p-4">
            <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Uso via Telegram</p>
            <p className="mt-2 text-xl font-semibold tracking-tight">{formatPercentage(telegramShare)}</p>
          </div>
        </div>
      </section>

      {showUpgradeJourney ? (
        <Card className="app-panel border-primary/15 bg-primary/8">
          <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div className="space-y-1">
              <CardTitle>
                {billing.upgradePendente
                  ? "Seu pedido de upgrade ja esta em andamento"
                  : billing.trialAtivo
                    ? "Seu trial Premium esta ativo"
                    : "Leve sua conta para o Premium"}
              </CardTitle>
              <CardDescription>{billing.mensagemStatus}</CardDescription>
            </div>
            <Badge variant={billing.upgradePendente ? "secondary" : "default"}>
              {billing.upgradePendente ? "Upgrade pendente" : billing.trialAtivo ? "Trial ativo" : "Upgrade"}
            </Badge>
          </CardHeader>
          <CardContent className="flex flex-col gap-4 lg:flex-row lg:items-center lg:justify-between">
            <div className="space-y-2 text-sm text-muted-foreground">
              <p>
                {billing.mensagemUpgrade ??
                  "O Premium libera o relatorio mensal e remove o limite mensal de lancamentos."}
              </p>
              <p>
                Voce pode acompanhar esse fluxo pela pagina de plano ou com{" "}
                <strong className="text-foreground">/upgrade</strong> no Telegram.
              </p>
            </div>
            <div className="flex flex-col gap-3 sm:flex-row">
              {billing.podeSolicitarUpgrade ? (
                <form action={requestUpgradeAction}>
                  <input type="hidden" name="redirectTo" value="/dashboard" />
                  <Button type="submit">Solicitar upgrade</Button>
                </form>
              ) : null}
              <Link href="/plano" className={buttonVariants({ variant: "outline" })}>
                Ver plano completo
              </Link>
            </div>
          </CardContent>
        </Card>
      ) : null}

      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard title="Entradas do dia" value={formatCurrency(resumo.ganhos)} description="Tudo o que entrou hoje." />
        <MetricCard title="Saidas do dia" value={formatCurrency(resumo.gastos)} description="Saidas registradas no dia." />
        <MetricCard title="Entradas do mes" value={formatCurrency(totalEntradasMes)} description="Receitas do mes corrente." />
        <MetricCard title="Saidas do mes" value={formatCurrency(totalSaidasMes)} description="Gastos acumulados no mes." />
      </section>

      <section className="grid gap-6 xl:grid-cols-[1.3fr_1fr]">
        <Card className="app-panel">
          <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div className="space-y-1">
              <CardTitle>Panorama do mes</CardTitle>
              <CardDescription>Leitura compacta de categoria, origem e ritmo recente.</CardDescription>
            </div>
            <span className="app-pill">
              <ChartSpline className="size-3.5" />
              {formatDate(firstDayOfMonth)} a {formatDate(now)}
            </span>
          </CardHeader>
          <CardContent className="grid gap-6 lg:grid-cols-2">
            <div className="space-y-4">
              <div className="space-y-1">
                 <p className="text-sm font-medium">Categorias de gasto</p>
                 <p className="text-sm text-muted-foreground">Onde as saidas mais se concentram.</p>
              </div>
              <div className="space-y-3">
                {categorias.length === 0 ? (
                  <EmptyCopy text="Ainda nao ha gasto suficiente para montar a distribuicao por categoria." />
                ) : (
                  categorias.map((categoria) => (
                    <BarRow
                      key={categoria.label}
                      label={categoria.label}
                      value={formatCurrency(categoria.total)}
                      percentage={categoria.percentage}
                    />
                  ))
                )}
              </div>
            </div>

            <div className="space-y-4">
              <div className="space-y-1">
                 <p className="text-sm font-medium">Ultimos dias</p>
                 <p className="text-sm text-muted-foreground">Entradas e saidas recentes.</p>
              </div>
              <div className="space-y-3">
                {dailySeries.length === 0 ? (
                  <EmptyCopy text="Assim que houver movimentacao no periodo, o ritmo diario aparece aqui." />
                ) : (
                  dailySeries.map((day) => (
                    <div key={day.dateKey} className="app-data-row p-4">
                      <div className="flex items-center justify-between gap-3">
                        <div>
                          <p className="font-medium">{formatDate(day.dateKey)}</p>
                          <p className="text-sm text-muted-foreground">
                            Entradas {formatCurrency(day.entradas)} • Saidas {formatCurrency(day.saidas)}
                          </p>
                        </div>
                        <span className="text-sm font-medium">{formatCurrency(day.saldo)}</span>
                      </div>
                      <div className="mt-3 grid gap-2">
                        <ProgressTrack value={day.entradas} max={Math.max(day.entradas, day.saidas, 1)} tone="positive" />
                        <ProgressTrack value={day.saidas} max={Math.max(day.entradas, day.saidas, 1)} tone="negative" />
                      </div>
                    </div>
                  ))
                )}
              </div>
            </div>

            <div className="space-y-4 lg:col-span-2">
              <div className="space-y-1">
                 <p className="text-sm font-medium">Origem dos lancamentos</p>
                 <p className="text-sm text-muted-foreground">Web ou Telegram no mes atual.</p>
              </div>
              <div className="grid gap-3 md:grid-cols-2">
                {originBreakdown.map((origem) => (
                  <div key={origem.label} className="app-data-row p-4">
                    <div className="flex items-center justify-between gap-3">
                      <p className="font-medium">{origem.label}</p>
                      <span className="text-sm font-medium">{formatPercentage(origem.percentage)}</span>
                    </div>
                    <p className="mt-1 text-sm text-muted-foreground">{origem.count} registro(s) no mes atual</p>
                    <div className="mt-3">
                      <ProgressTrack value={origem.count} max={movimentosMes.length || 1} tone="neutral" />
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="app-panel">
          <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div className="space-y-1">
              <CardTitle>Relatorio mensal</CardTitle>
              <CardDescription>Resumo financeiro consolidado e leitura das categorias principais.</CardDescription>
            </div>
            <Badge variant={canAccessMonthlyReport ? "default" : "secondary"}>
              {canAccessMonthlyReport ? "Premium/trial" : "Upgrade"}
            </Badge>
          </CardHeader>
          <CardContent className="space-y-4">
            {monthlyReport ? (
              <>
                <div className="grid gap-3 sm:grid-cols-2">
                  <MetricCard title="Entradas do mes" value={formatCurrency(monthlyReport.totalReceitas)} />
                  <MetricCard title="Saidas do mes" value={formatCurrency(monthlyReport.totalGastos)} />
                  <MetricCard title="Saldo" value={formatCurrency(monthlyReport.saldo)} />
                  <MetricCard title="Lancamentos" value={String(monthlyReport.totalLancamentos)} />
                </div>

                <div className="app-data-row p-4 text-sm text-muted-foreground">
                  Referencia {String(monthlyReport.mes).padStart(2, "0")}/{monthlyReport.ano}
                </div>

                <div className="space-y-3">
                  <p className="text-sm font-medium">Top categorias de gasto</p>
                  {monthlyReport.topCategoriasGasto.length === 0 ? (
                    <EmptyCopy text="Ainda nao ha gastos categorizados neste mes." />
                  ) : (
                    monthlyReport.topCategoriasGasto.map((categoria) => (
                      <div key={categoria.categoria} className="app-data-row p-4">
                        <div className="flex items-center justify-between gap-3">
                          <div>
                            <p className="font-medium">{categoria.categoria}</p>
                            <p className="text-sm text-muted-foreground">
                              {categoria.quantidade} lancamento(s)
                            </p>
                          </div>
                          <span className="font-semibold">{formatCurrency(categoria.totalGasto)}</span>
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </>
            ) : canAccessMonthlyReport ? (
              <FlashMessage
                title="Nao foi possivel carregar o relatorio"
                message={monthlyReportError ?? "Tente novamente em instantes."}
                variant="destructive"
              />
            ) : (
              <div className="rounded-2xl border border-dashed border-border/70 p-4 text-sm text-muted-foreground">
                <p>{billing.mensagemUpgrade ?? "Faça upgrade para liberar o relatorio mensal."}</p>
                <p className="mt-2">
                  Enquanto o checkout nao entra, o trial e o Premium seguem sendo o caminho para
                  destravar essa camada de analise.
                </p>
              </div>
            )}
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-6 xl:grid-cols-[1.35fr_0.95fr]">
        <Card className="app-panel">
          <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div className="space-y-1">
              <CardTitle>Ultimos movimentos</CardTitle>
              <CardDescription>Uma leitura mais limpa do que aconteceu recentemente.</CardDescription>
            </div>
            <Link href="/lancamentos" className={buttonVariants({ variant: "outline" })}>
              Ver todos os lancamentos
            </Link>
          </CardHeader>
          <CardContent className="space-y-3">
            {movimentosRecentes.length === 0 ? (
              <EmptyCopy text="Ainda nao ha movimentos registrados." />
            ) : (
              movimentosRecentes.map((movimento, index) => (
                <div
                  key={`${movimento.tipo}-${movimento.data}-${index}`}
                  className="app-data-row flex flex-col gap-3 p-4 sm:flex-row sm:items-center sm:justify-between"
                >
                  <div className="min-w-0 space-y-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <Badge variant={movimento.tipo === "Receita" ? "default" : "secondary"}>
                        {movimento.tipo}
                      </Badge>
                      {movimento.categoria ? <Badge variant="outline">{movimento.categoria}</Badge> : null}
                      <span className="app-pill">{movimento.origem}</span>
                    </div>
                    <p className="truncate font-medium">{movimento.descricao}</p>
                    <p className="text-sm text-muted-foreground">{formatDateTime(movimento.data)}</p>
                    {movimento.observacao ? (
                      <p className="text-sm text-muted-foreground">{movimento.observacao}</p>
                    ) : null}
                  </div>
                  <span className="text-lg font-semibold tracking-tight">{formatCurrency(movimento.valor)}</span>
                </div>
              ))
            )}
          </CardContent>
        </Card>

        <Card className="app-panel h-fit">
          <CardHeader>
            <CardTitle>Plano e quota</CardTitle>
            <CardDescription>Estado atual do seu acesso e proximas alavancas comerciais.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4 text-sm">
            <InlineStat
              label="Plano efetivo"
              value={
                <Badge variant={billing.planoEfetivo === "Premium" ? "default" : "secondary"}>
                  {billing.planoEfetivo}
                </Badge>
              }
            />
            <InlineStat label="Assinatura" value={billing.statusAssinatura} />
            {billing.upgradeSolicitadoEmUtc ? (
              <InlineStat
                label="Upgrade solicitado em"
                value={formatDateTime(billing.upgradeSolicitadoEmUtc)}
              />
            ) : null}
            {billing.trialAteUtc ? (
              <InlineStat label="Trial ate" value={formatDateTime(billing.trialAteUtc)} />
            ) : null}
            <InlineStat
              label="Lancamentos no mes"
              value={`${billing.lancamentosNoMesAtual}${billing.limiteLancamentosNoMesAtual ? ` / ${billing.limiteLancamentosNoMesAtual}` : " / ilimitado"}`}
            />
            <div className="rounded-2xl border border-dashed border-border/70 p-4 text-muted-foreground">
              {billing.mensagemStatus}
            </div>
            {billing.motivoBloqueio ? (
              <FlashMessage title="Atencao" message={billing.motivoBloqueio} variant="destructive" />
            ) : (
              <p className="text-muted-foreground">Voce ainda pode registrar lancamentos normalmente.</p>
            )}
            {billing.mensagemUpgrade ? (
              <div className="rounded-2xl border border-dashed border-border/70 p-4 text-muted-foreground">
                {billing.mensagemUpgrade}
              </div>
            ) : null}
            {billing.podeSolicitarUpgrade ? (
              <form action={requestUpgradeAction}>
                <input type="hidden" name="redirectTo" value="/dashboard" />
                <Button type="submit" className="w-full">
                  Solicitar upgrade para Premium
                </Button>
              </form>
            ) : billing.upgradePendente ? (
              <div className="rounded-2xl border border-dashed border-border/70 p-4 text-muted-foreground">
                Seu pedido de upgrade ja esta registrado e segue ate a ativacao do Premium.
              </div>
            ) : null}
            <Link href="/plano" className={`${buttonVariants({ variant: "outline" })} w-full`}>
              Abrir pagina de plano
            </Link>
          </CardContent>
        </Card>
      </section>

      <section className="grid gap-6 xl:grid-cols-2">
        <Card className="app-panel">
          <CardHeader>
            <CardTitle>Novo gasto</CardTitle>
            <CardDescription>Registro rapido, com observacao opcional para dar mais contexto.</CardDescription>
          </CardHeader>
          <CardContent>
            <form action={createGastoAction} className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="gasto-descricao">Descricao</Label>
                <Input id="gasto-descricao" name="descricao" placeholder="Ex.: Mercado" required />
              </div>
              <div className="space-y-2">
                <Label htmlFor="gasto-valor">Valor</Label>
                <Input id="gasto-valor" name="valor" placeholder="Ex.: 45,90" required />
              </div>
              <div className="space-y-2">
                <Label htmlFor="gasto-observacao">Observacao</Label>
                <textarea
                  id="gasto-observacao"
                  name="observacao"
                  placeholder="Ex.: compra do fim de semana"
                  className={textareaClassName}
                />
              </div>
              <Button type="submit" className="w-full">
                Salvar gasto
              </Button>
            </form>
          </CardContent>
        </Card>

        <Card className="app-panel">
          <CardHeader>
            <CardTitle>Nova receita</CardTitle>
            <CardDescription>Entrada manual com uma leitura mais clara da recorrencia.</CardDescription>
          </CardHeader>
          <CardContent>
            <form action={createReceitaAction} className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="receita-descricao">Descricao</Label>
                <Input id="receita-descricao" name="descricao" placeholder="Ex.: Freelance" required />
              </div>
              <div className="space-y-2">
                <Label htmlFor="receita-valor">Valor</Label>
                <Input id="receita-valor" name="valor" placeholder="Ex.: 300,00" required />
              </div>
              <div className="space-y-2">
                <Label htmlFor="receita-observacao">Observacao</Label>
                <textarea
                  id="receita-observacao"
                  name="observacao"
                  placeholder="Ex.: pagamento do cliente X"
                  className={textareaClassName}
                />
              </div>
              <label className="flex items-center gap-3 rounded-2xl border border-border/70 bg-background/60 px-4 py-3 text-sm">
                <Checkbox name="ehFixo" />
                Receita recorrente
              </label>
              <Button type="submit" className="w-full">
                Salvar receita
              </Button>
            </form>
          </CardContent>
        </Card>
      </section>
    </div>
  );
}

function InlineStat({
  label,
  value,
}: {
  label: string;
  value: ReactNode;
}) {
  return (
    <div className="flex items-center justify-between gap-4">
      <span className="text-muted-foreground">{label}</span>
      <span className="max-w-[14rem] text-right">{value}</span>
    </div>
  );
}

function BarRow({
  label,
  value,
  percentage,
}: {
  label: string;
  value: string;
  percentage: number;
}) {
  return (
    <div className="app-data-row p-4">
      <div className="flex items-center justify-between gap-3">
        <p className="font-medium">{label}</p>
        <span className="text-sm text-muted-foreground">{value}</span>
      </div>
      <div className="mt-3">
        <ProgressTrack value={percentage} max={1} tone="neutral" />
      </div>
      <p className="mt-2 text-xs uppercase tracking-[0.24em] text-muted-foreground">
        {formatPercentage(percentage)}
      </p>
    </div>
  );
}

function ProgressTrack({
  value,
  max,
  tone,
}: {
  value: number;
  max: number;
  tone: "positive" | "negative" | "neutral";
}) {
  const percentage = max <= 0 ? 0 : Math.max(6, Math.round((value / max) * 100));
  const colorClass =
    tone === "positive"
      ? "from-emerald-400 to-emerald-500"
      : tone === "negative"
        ? "from-rose-400 to-rose-500"
        : "from-primary/70 to-primary";

  return (
    <div className="h-2 overflow-hidden rounded-full bg-muted/80">
      <div className={`h-full rounded-full bg-gradient-to-r ${colorClass}`} style={{ width: `${percentage}%` }} />
    </div>
  );
}

function EmptyCopy({ text }: { text: string }) {
  return (
    <div className="rounded-2xl border border-dashed border-border/70 p-4 text-sm text-muted-foreground">
      {text}
    </div>
  );
}

function buildCategoryBreakdown(movimentos: MovimentoItem[]) {
  const total = movimentos.reduce((sum, movimento) => sum + movimento.valor, 0);
  const grouped = new Map<string, number>();

  for (const movimento of movimentos) {
    const key = movimento.categoria ?? "Outros";
    grouped.set(key, (grouped.get(key) ?? 0) + movimento.valor);
  }

  return [...grouped.entries()]
    .map(([label, categoryTotal]) => ({
      label,
      total: categoryTotal,
      percentage: total > 0 ? categoryTotal / total : 0,
    }))
    .sort((left, right) => right.total - left.total)
    .slice(0, 5);
}

function buildDailySeries(movimentos: MovimentoItem[]) {
  const grouped = new Map<
    string,
    { dateKey: string; entradas: number; saidas: number; saldo: number }
  >();

  for (const movimento of movimentos) {
    const dateKey = movimento.data.slice(0, 10);
    const current = grouped.get(dateKey) ?? {
      dateKey,
      entradas: 0,
      saidas: 0,
      saldo: 0,
    };

    if (movimento.tipo === "Receita") {
      current.entradas += movimento.valor;
      current.saldo += movimento.valor;
    } else {
      current.saidas += movimento.valor;
      current.saldo -= movimento.valor;
    }

    grouped.set(dateKey, current);
  }

  return [...grouped.values()].sort((left, right) => left.dateKey.localeCompare(right.dateKey));
}

function buildOriginBreakdown(movimentos: MovimentoItem[]) {
  const total = movimentos.length || 1;
  const grouped = new Map<string, number>();

  for (const movimento of movimentos) {
    grouped.set(movimento.origem, (grouped.get(movimento.origem) ?? 0) + 1);
  }

  return [...grouped.entries()]
    .map(([label, count]) => ({
      label,
      count,
      percentage: count / total,
    }))
    .sort((left, right) => right.count - left.count);
}
