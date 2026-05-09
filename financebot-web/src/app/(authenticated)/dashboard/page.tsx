import Link from "next/link";
import type { ReactNode } from "react";

import {
  ArrowRightLeft,
  ChartSpline,
} from "lucide-react";
import { FaTelegramPlane } from "react-icons/fa";

import { updateMonthlyBudgetAction } from "@/actions/budget";
import { requestUpgradeAction } from "@/actions/billing";
import { FlashMessage } from "@/components/app/flash-message";
import { LaunchEntryModal } from "@/components/app/launch-entry-modal";
import { MetricCard } from "@/components/app/metric-card";
import { PageSectionNav } from "@/components/app/page-section-nav";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getQueryMessage } from "@/lib/action-state";
import {
  FinanceBotApiError,
  getBillingStatus,
  getMonthlyBudget,
  getMonthlyReport,
  getResumo,
  getUltimosMovimentos,
  listMovimentos,
} from "@/lib/financebot-api";
import { requireSession } from "@/lib/session";
import { formatCurrency, formatDate, formatDateTime, formatPercentage, getMovementTraits } from "@/lib/utils/format";

type DashboardPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

type MovimentoItem = Awaited<ReturnType<typeof listMovimentos>>[number];
type MonthlyBudgetItem = Awaited<ReturnType<typeof getMonthlyBudget>>;
const dashboardSections = [
  { id: "visao-geral", label: "Visao geral", description: "Saldo, indicadores e upgrade." },
  { id: "orcamento", label: "Orcamento", description: "Limite mensal e ritmo de gastos." },
  { id: "analises", label: "Analises", description: "Panorama do mes e relatorio." },
  { id: "atividade", label: "Atividade", description: "Movimentos recentes e plano." },
] as const;

type DashboardSection = (typeof dashboardSections)[number]["id"];

export default async function DashboardPage({ searchParams }: DashboardPageProps) {
  const session = await requireSession();
  const params = await searchParams;
  const error = getQueryMessage(params.error);
  const success = getQueryMessage(params.success);
  const activeSection = resolveDashboardSection(getSingleValue(params.secao));
  const now = new Date();
  const firstDayOfMonth = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1));
  const previousMonthReference = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth() - 1, 1));
  const monthStart = firstDayOfMonth.toISOString().slice(0, 10);
  const monthEnd = now.toISOString().slice(0, 10);

  const [
    resumo,
    movimentosRecentes,
    billing,
    movimentosMes,
    monthlyBudget,
    previousMonthBudget,
  ] = await Promise.all([
    getResumo(session.token),
    getUltimosMovimentos(session.token),
    getBillingStatus(session.token),
    listMovimentos(session.token, {
      inicio: monthStart,
      fim: monthEnd,
      limite: 200,
    }),
    getMonthlyBudget(session.token),
    getMonthlyBudget(session.token, {
      ano: previousMonthReference.getUTCFullYear(),
      mes: previousMonthReference.getUTCMonth() + 1,
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
  const currentSectionPath = buildDashboardPath(activeSection);
  const budgetInsights = buildBudgetInsights(monthlyBudget, previousMonthBudget);
  const budgetSuggestions = buildBudgetSuggestions(monthlyBudget);

  return (
    <div className="space-y-8">
      {error ? (
        <FlashMessage title="Algo deu errado" message={error} variant="destructive" />
      ) : null}
      {success ? <FlashMessage title="Tudo certo" message={success} /> : null}

      <section className="app-panel p-5 sm:p-6">
        <div className="grid gap-5 xl:grid-cols-[minmax(0,1.05fr)_minmax(0,0.95fr)] xl:items-start">
          <div className="space-y-4">
            <p className="text-xs font-medium uppercase tracking-[0.24em] text-muted-foreground">Dashboard</p>
            <h1 className="text-2xl font-semibold tracking-tight sm:text-3xl">Resumo financeiro</h1>
            <p className="max-w-2xl text-sm text-muted-foreground">
              Veja o saldo do dia, acompanhe o mes atual e use os lancamentos como area principal de operacao.
            </p>
            <div className="flex flex-wrap gap-2">
              <span className="app-pill">
                {topCategoria ? `Maior categoria: ${topCategoria.label}` : "Sem gasto relevante no mes"}
              </span>
              <span className="app-pill">
                {formatDate(firstDayOfMonth)} a {formatDate(now)}
              </span>
            </div>
            <div className="grid grid-cols-2 gap-2 sm:flex sm:flex-row">
              <LaunchEntryModal
                redirectTo={currentSectionPath}
                className="col-span-2 h-11 w-full justify-center rounded-2xl sm:col-span-1 sm:w-auto"
              />
              <Link
                href="/lancamentos"
                className={`${buttonVariants({ variant: "outline" })} h-11 w-full justify-center rounded-2xl sm:w-auto`}
              >
                <ArrowRightLeft className="size-4" />
                Abrir extrato
              </Link>
              <Link
                href="/telegram"
                className={`${buttonVariants({ variant: "outline" })} app-telegram-button h-11 w-full justify-center rounded-2xl sm:w-auto`}
              >
                <FaTelegramPlane className="size-4" />
                Ver Telegram
              </Link>
            </div>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <DashboardKeyStat label="Saldo do dia" value={formatCurrency(resumo.saldo)} hint="Hoje" />
            <DashboardKeyStat label="Saldo do mes" value={formatCurrency(saldoMes)} hint="Mes atual" />
            <DashboardKeyStat label="Dias com registro" value={String(diasComMovimento)} hint="No mes" />
            <DashboardKeyStat label="Uso via Telegram" value={formatPercentage(telegramShare)} hint="Dos registros" />
          </div>
        </div>
      </section>

      <PageSectionNav
        items={dashboardSections.map((section) => ({
          href: buildDashboardPath(section.id),
          label: section.label,
          description: section.description,
          active: section.id === activeSection,
        }))}
      />

      {activeSection === "visao-geral" ? (
        <>
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
                      <input type="hidden" name="redirectTo" value={buildDashboardPath("visao-geral")} />
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
        </>
      ) : null}

      {activeSection === "orcamento" ? (
        <Card className="app-panel">
          <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div className="space-y-1">
              <CardTitle>Orcamento do mes</CardTitle>
              <CardDescription>
                Defina um teto de gastos, acompanhe o ritmo atual e use esse espaco para tomar decisao com mais calma.
              </CardDescription>
            </div>
            <Badge variant={monthlyBudget.possuiOrcamentoDefinido ? "default" : "secondary"}>
              {monthlyBudget.possuiOrcamentoDefinido ? "Orcamento ativo" : "Nao definido"}
            </Badge>
          </CardHeader>
          <CardContent className="grid gap-6 xl:grid-cols-[minmax(0,1.25fr)_minmax(21rem,0.85fr)]">
            <div className="min-w-0 space-y-4">
              <div className="rounded-2xl border border-amber-500/35 bg-amber-500/10 p-4 sm:p-5">
                <p className="text-xs font-medium uppercase tracking-[0.24em] text-amber-900/80 dark:text-amber-200/80">
                  Leitura rapida
                </p>
                <p className="mt-3 text-sm leading-6 text-amber-950/90 dark:text-amber-50/90">
                  {buildBudgetSummary(monthlyBudget, budgetInsights)}
                </p>
                {monthlyBudget.possuiOrcamentoDefinido ? (
                  <div className="mt-4 space-y-2">
                    <div className="flex items-center justify-between text-sm text-amber-900/75 dark:text-amber-200/80">
                      <span>Consumo do limite</span>
                      <span>{formatPercentage(monthlyBudget.percentualConsumido ?? 0)}</span>
                    </div>
                    <ProgressTrack value={monthlyBudget.percentualConsumido ?? 0} max={1} tone="neutral" />
                  </div>
                ) : null}
              </div>

              <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                <div className="app-data-row p-4">
                  <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Limite</p>
                  <p className="mt-2 font-semibold tracking-tight">
                    {monthlyBudget.limiteGastos !== null ? formatCurrency(monthlyBudget.limiteGastos) : "Nao definido"}
                  </p>
                </div>
                <div className="app-data-row p-4">
                  <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Gastos</p>
                  <p className="mt-2 font-semibold tracking-tight">{formatCurrency(monthlyBudget.totalGastos)}</p>
                </div>
                <div className="app-data-row p-4">
                  <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Restante</p>
                  <p className="mt-2 font-semibold tracking-tight">
                    {monthlyBudget.restante !== null ? formatCurrency(monthlyBudget.restante) : "Defina um limite"}
                  </p>
                </div>
                <div className="app-data-row p-4">
                  <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Projecao</p>
                  <p className="mt-2 font-semibold tracking-tight">{formatCurrency(monthlyBudget.projecaoFechamento)}</p>
                </div>
              </div>
            </div>

            <form action={updateMonthlyBudgetAction} className="min-w-0 rounded-2xl border border-border/60 bg-background/60 p-4">
              <input type="hidden" name="redirectTo" value={currentSectionPath} />
              <input type="hidden" name="ano" value={monthlyBudget.ano} />
              <input type="hidden" name="mes" value={monthlyBudget.mes} />
              <div className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="monthly-budget-limit">Limite de gastos</Label>
                  <Input
                    id="monthly-budget-limit"
                    name="limiteGastos"
                    placeholder="Ex.: 2500,00"
                    defaultValue={monthlyBudget.limiteGastos?.toFixed(2) ?? ""}
                  />
                </div>
                <p className="text-sm text-muted-foreground">
                  Restam {monthlyBudget.diasRestantes} dia(s) no mes atual. Use esse teto para saber
                  quanto ainda pode gastar com seguranca.
                </p>
                <Button type="submit" className="w-full">
                  Salvar orcamento
                </Button>
              </div>
            </form>

            <div className="min-w-0 xl:col-span-2">
              <div className="overflow-hidden rounded-2xl border border-border/60 bg-background/60 p-4">
                <div className="space-y-1">
                  <p className="text-sm font-medium">Sugestoes de limite</p>
                  <p className="text-sm text-muted-foreground">
                    Base atual: {budgetSuggestions.sourceLabel}.{" "}
                    {budgetSuggestions.items.length > 0
                      ? "A sugestao equilibrada e a mais segura para comecar."
                      : "Assim que houver mais historico, eu sugiro limites aqui automaticamente."}
                  </p>
                </div>
                <div className="max-w-full overflow-x-auto pb-2">
                  {budgetSuggestions.items.length === 0 ? (
                    <div className="rounded-2xl border border-dashed border-border/70 bg-background/70 px-4 py-5 text-sm text-muted-foreground">
                      Ainda nao existe base suficiente para recomendar um limite mensal com seguranca.
                    </div>
                  ) : (
                    <div className="grid min-w-full snap-x snap-mandatory grid-flow-col auto-cols-[85%] gap-3 sm:auto-cols-[16rem] xl:min-w-0 xl:grid-cols-3 xl:grid-flow-row xl:auto-cols-auto">
                      {budgetSuggestions.items.map((suggestion) => (
                        <BudgetSuggestionRow
                          key={suggestion.id}
                          suggestion={suggestion}
                          redirectTo={currentSectionPath}
                          ano={monthlyBudget.ano}
                          mes={monthlyBudget.mes}
                        />
                      ))}
                    </div>
                  )}
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      ) : null}

      {activeSection === "analises" ? (
        <section className="grid gap-6 xl:grid-cols-[1.3fr_1fr]">
          <Card className="app-panel">
            <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
              <div className="space-y-1">
                <CardTitle>Leitura do mes</CardTitle>
                <CardDescription>Menos ruido e foco no que mais pesa no periodo.</CardDescription>
              </div>
              <span className="app-pill">
                <ChartSpline className="size-3.5" />
                {formatDate(firstDayOfMonth)} a {formatDate(now)}
              </span>
            </CardHeader>
            <CardContent className="space-y-6">
              <div className="grid gap-6 lg:grid-cols-2">
                <div className="space-y-3">
                  <div className="space-y-1">
                    <p className="text-sm font-medium">Categorias que mais pesam</p>
                    <p className="text-sm text-muted-foreground">So o essencial para entender para onde o dinheiro vai.</p>
                  </div>
                  {categorias.length === 0 ? (
                    <EmptyCopy text="Ainda nao ha gasto suficiente para montar essa leitura." />
                  ) : (
                    <div className="space-y-2">
                      {categorias.slice(0, 4).map((categoria) => (
                        <CompactInsightRow
                          key={categoria.label}
                          label={categoria.label}
                          value={formatCurrency(categoria.total)}
                          detail={formatPercentage(categoria.percentage)}
                        />
                      ))}
                    </div>
                  )}
                </div>

                <div className="space-y-3">
                  <div className="space-y-1">
                    <p className="text-sm font-medium">Ritmo recente</p>
                    <p className="text-sm text-muted-foreground">Os ultimos dias sem grafico pesado.</p>
                  </div>
                  {dailySeries.length === 0 ? (
                    <EmptyCopy text="Assim que houver movimentacao, o ritmo recente aparece aqui." />
                  ) : (
                    <div className="space-y-2">
                      {dailySeries
                        .slice(-4)
                        .reverse()
                        .map((day) => (
                          <CompactInsightRow
                            key={day.dateKey}
                            label={formatDate(day.dateKey)}
                            value={formatCurrency(day.saldo)}
                            detail={`Entradas ${formatCurrency(day.entradas)} • Saidas ${formatCurrency(day.saidas)}`}
                          />
                        ))}
                    </div>
                  )}
                </div>
              </div>

              <div className="space-y-3">
                <div className="space-y-1">
                  <p className="text-sm font-medium">Origem dos lancamentos</p>
                  <p className="text-sm text-muted-foreground">Distribuicao simples entre Web e Telegram.</p>
                </div>
                {originBreakdown.length === 0 ? (
                  <EmptyCopy text="A origem aparece assim que houver registros no mes." />
                ) : (
                  <div className="flex flex-wrap gap-2">
                    {originBreakdown.map((origem) => (
                      <OriginBreakdownPill
                        key={origem.label}
                        label={origem.label}
                        count={origem.count}
                        percentage={origem.percentage}
                      />
                    ))}
                  </div>
                )}
              </div>
            </CardContent>
          </Card>

          <Card className="app-panel">
            <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
              <div className="space-y-1">
                <CardTitle>Relatorio mensal</CardTitle>
                <CardDescription>Consolidado do mes em formato mais enxuto.</CardDescription>
              </div>
              <Badge variant={canAccessMonthlyReport ? "default" : "secondary"}>
                {canAccessMonthlyReport ? "Premium/trial" : "Upgrade"}
              </Badge>
            </CardHeader>
            <CardContent className="space-y-4">
              {monthlyReport ? (
                <>
                  <div className="grid grid-cols-2 gap-3">
                    <DashboardKeyStat label="Entradas" value={formatCurrency(monthlyReport.totalReceitas)} />
                    <DashboardKeyStat label="Saidas" value={formatCurrency(monthlyReport.totalGastos)} />
                    <DashboardKeyStat label="Saldo" value={formatCurrency(monthlyReport.saldo)} />
                    <DashboardKeyStat label="Lancamentos" value={String(monthlyReport.totalLancamentos)} />
                  </div>

                  <div className="app-pill w-fit">
                    Referencia {String(monthlyReport.mes).padStart(2, "0")}/{monthlyReport.ano}
                  </div>

                  <div className="space-y-3">
                    <p className="text-sm font-medium">Top categorias de gasto</p>
                    {monthlyReport.topCategoriasGasto.length === 0 ? (
                      <EmptyCopy text="Ainda nao ha gastos categorizados neste mes." />
                    ) : (
                      <div className="space-y-2">
                        {monthlyReport.topCategoriasGasto.slice(0, 3).map((categoria) => (
                          <CompactInsightRow
                            key={categoria.categoria}
                            label={categoria.categoria}
                            value={formatCurrency(categoria.totalGasto)}
                            detail={`${categoria.quantidade} lancamento(s)`}
                          />
                        ))}
                      </div>
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
      ) : null}

      {activeSection === "atividade" ? (
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
                      {getMovementTraits(movimento).map((trait) => (
                        <Badge key={`${movimento.id}-${trait}`} variant="outline">
                          {trait}
                        </Badge>
                      ))}
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
                <input type="hidden" name="redirectTo" value={buildDashboardPath("atividade")} />
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
      ) : null}

    </div>
  );
}

function getSingleValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] ?? "" : value ?? "";
}

function resolveDashboardSection(value: string): DashboardSection {
  return dashboardSections.some((section) => section.id === value)
    ? (value as DashboardSection)
    : "visao-geral";
}

function buildDashboardPath(section: DashboardSection) {
  if (section === "visao-geral") {
    return "/dashboard";
  }

  const params = new URLSearchParams();
  params.set("secao", section);
  return `/dashboard?${params.toString()}`;
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

function DashboardKeyStat({
  label,
  value,
  hint,
}: {
  label: string;
  value: string;
  hint?: string;
}) {
  return (
    <div className="app-data-row p-3.5 sm:p-4">
      <p className="text-[11px] uppercase tracking-[0.24em] text-muted-foreground">{label}</p>
      <p className="mt-2 text-base font-semibold tracking-tight sm:text-lg">{value}</p>
      {hint ? (
        <p className="mt-1 text-xs text-muted-foreground">{hint}</p>
      ) : null}
    </div>
  );
}

function CompactInsightRow({
  label,
  value,
  detail,
}: {
  label: string;
  value: string;
  detail: string;
}) {
  return (
    <div className="app-data-row p-3.5">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 space-y-1">
          <p className="font-medium">{label}</p>
          <p className="text-sm text-muted-foreground">{detail}</p>
        </div>
        <span className="shrink-0 text-sm font-semibold">{value}</span>
      </div>
    </div>
  );
}

function OriginBreakdownPill({
  label,
  count,
  percentage,
}: {
  label: string;
  count: number;
  percentage: number;
}) {
  return (
    <span className="app-pill">
      <strong className="text-foreground">{label}</strong>
      <span>{count} registro(s)</span>
      <span>{formatPercentage(percentage)}</span>
    </span>
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

function BudgetSuggestionRow({
  suggestion,
  redirectTo,
  ano,
  mes,
}: {
  suggestion: {
    id: string;
    label: string;
    amount: number;
    description: string;
    recommended?: boolean;
  };
  redirectTo: string;
  ano: number;
  mes: number;
}) {
  return (
    <div className="min-w-0 snap-start rounded-2xl border border-border/60 p-3">
      <div className="flex items-start justify-between gap-3">
        <div className="space-y-1">
          <div className="flex flex-wrap items-center gap-2">
            <p className="text-sm font-medium">{suggestion.label}</p>
            {suggestion.recommended ? <Badge variant="secondary">Recomendado</Badge> : null}
          </div>
          <p className="text-sm text-muted-foreground">{suggestion.description}</p>
        </div>
        <p className="text-right font-semibold tracking-tight">{formatCurrency(suggestion.amount)}</p>
      </div>
      <form action={updateMonthlyBudgetAction} className="mt-3">
        <input type="hidden" name="redirectTo" value={redirectTo} />
        <input type="hidden" name="ano" value={ano} />
        <input type="hidden" name="mes" value={mes} />
        <input type="hidden" name="limiteGastos" value={suggestion.amount.toFixed(2)} />
        <Button type="submit" variant={suggestion.recommended ? "default" : "outline"} size="sm" className="w-full">
          Usar esse limite
        </Button>
      </form>
    </div>
  );
}

function buildBudgetSummary(budget: MonthlyBudgetItem, insights: ReturnType<typeof buildBudgetInsights>) {
  if (!budget.possuiOrcamentoDefinido) {
    return `Voce ainda nao definiu um limite mensal. Seus gastos fixos ja somam ${formatCurrency(budget.gastoFixo)}, os essenciais ${formatCurrency(budget.gastoEssencial)} e a projecao atual aponta ${formatCurrency(budget.projecaoFechamento)} para o fechamento.`;
  }

  if (budget.estourado) {
    return `Voce ja ultrapassou o orcamento em ${formatCurrency(Math.abs(budget.restante ?? 0))}. Vale revisar os gastos nao essenciais antes de seguir aumentando o mes.`;
  }

  if (budget.estouroProjetado) {
    return `Mantido o ritmo atual, o mes deve fechar em ${formatCurrency(budget.projecaoFechamento)}, acima do limite definido. Para voltar ao plano, tente segurar o restante em algo perto de ${insights.dailyAllowanceLabel} por dia.`;
  }

  const paceText = insights.hasDailyAllowance
    ? ` Isso deixa algo perto de ${insights.dailyAllowanceLabel} por dia ate o fechamento.`
    : "";

  return `Voce consumiu ${formatPercentage(budget.percentualConsumido ?? 0)} do orcamento e ainda tem ${formatCurrency(budget.restante ?? 0)} disponiveis.${paceText}`;
}

function buildBudgetInsights(current: MonthlyBudgetItem, previous: MonthlyBudgetItem) {
  const previousReferenceLabel = formatBudgetReference(previous.ano, previous.mes);
  const hasPreviousBase =
    previous.totalGastos > 0 || previous.totalReceitas > 0 || previous.possuiOrcamentoDefinido;

  let previousMonthLabel = "Sem base anterior";
  if (hasPreviousBase) {
    const difference = current.totalGastos - previous.totalGastos;
    previousMonthLabel =
      difference === 0
        ? "Mesmo nivel"
        : difference > 0
          ? `${formatCurrency(difference)} acima`
          : `${formatCurrency(Math.abs(difference))} abaixo`;
  }

  let dailyAllowanceLabel = "Defina um limite";
  let hasDailyAllowance = false;
  if (current.possuiOrcamentoDefinido) {
    if (current.estourado || (current.restante ?? 0) <= 0) {
      dailyAllowanceLabel = "Sem folga diaria";
    } else if (current.diasRestantes <= 0) {
      dailyAllowanceLabel = "Mes no fim";
    } else {
      dailyAllowanceLabel = formatCurrency((current.restante ?? 0) / current.diasRestantes);
      hasDailyAllowance = true;
    }
  }

  let projectedMarginLabel = "Defina um limite";
  if (current.possuiOrcamentoDefinido && current.diferencaProjetada !== null) {
    projectedMarginLabel =
      current.diferencaProjetada >= 0
        ? `${formatCurrency(current.diferencaProjetada)} de folga`
        : `${formatCurrency(Math.abs(current.diferencaProjetada))} acima`;
  }

  return {
    dailyAllowanceLabel,
    hasDailyAllowance,
    previousReferenceLabel,
    previousMonthLabel,
    projectedMarginLabel,
  };
}

function buildBudgetSuggestions(current: MonthlyBudgetItem) {
  return {
    sourceLabel:
      current.mesesBaseSugestao > 0
        ? `media dos ultimos ${current.mesesBaseSugestao} mes(es) fechados`
        : "projecao do mes atual por falta de historico",
    items: [
      {
        id: "safe",
        label: "Sugestao segura",
        amount: current.sugestaoLimiteSeguro ?? 0,
        description: "Foca no seu piso mais comprometido para reduzir o risco de estourar o mes.",
      },
      {
        id: "balanced",
        label: "Sugestao equilibrada",
        amount: current.sugestaoLimiteEquilibrado ?? 0,
        description: "Considera o basico do mes e uma parte do gasto variavel para um limite mais realista.",
        recommended: true,
      },
      {
        id: "flexible",
        label: "Sugestao flexivel",
        amount: current.sugestaoLimiteFlexivel ?? 0,
        description: "Abre mais espaco para variacoes, mas ainda respeita uma trava ligada a sua receita media.",
      },
    ].filter((item) => item.amount > 0),
  };
}

function formatBudgetReference(year: number, month: number) {
  return `${String(month).padStart(2, "0")}/${year}`;
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
