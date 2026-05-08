import Link from "next/link";

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
import {
  FinanceBotApiError,
  getBillingStatus,
  getMonthlyReport,
  getUltimosMovimentos,
  getResumo,
} from "@/lib/financebot-api";
import { getQueryMessage } from "@/lib/action-state";
import { requireSession } from "@/lib/session";
import { formatCurrency, formatDateTime } from "@/lib/utils/format";

const textareaClassName =
  "flex min-h-20 w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-xs outline-none transition-[color,box-shadow] placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-ring/50 focus-visible:ring-[3px] dark:bg-input/30";

type DashboardPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default async function DashboardPage({
  searchParams,
}: DashboardPageProps) {
  const session = await requireSession();
  const params = await searchParams;
  const error = getQueryMessage(params.error);
  const success = getQueryMessage(params.success);

  const [resumo, movimentos, billing] = await Promise.all([
    getResumo(session.token),
    getUltimosMovimentos(session.token),
    getBillingStatus(session.token),
  ]);
  const canAccessMonthlyReport = billing.planoEfetivo === "Premium";
  const showUpgradeJourney =
    billing.podeSolicitarUpgrade || billing.upgradePendente || billing.trialAtivo;
  let monthlyReport = null;
  let monthlyReportError: string | null = null;

  if (canAccessMonthlyReport) {
    try {
      monthlyReport = await getMonthlyReport(session.token);
    } catch (error) {
      monthlyReportError =
        error instanceof FinanceBotApiError
          ? error.message
          : "Nao foi possivel carregar o relatorio mensal.";
    }
  }

  return (
    <div className="space-y-6">
      {error ? (
        <FlashMessage title="Algo deu errado" message={error} variant="destructive" />
      ) : null}
      {success ? (
        <FlashMessage title="Tudo certo" message={success} />
      ) : null}

      {showUpgradeJourney ? (
        <Card className="border-primary/20 bg-primary/5">
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
              <p>{billing.mensagemUpgrade ?? "O Premium libera o relatorio mensal e remove o limite mensal de lancamentos."}</p>
              <p>
                Voce pode acompanhar esse fluxo na pagina de plano ou usar o comando{" "}
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

      <section className="grid gap-4 md:grid-cols-3">
        <MetricCard title="Entradas do dia" value={formatCurrency(resumo.ganhos)} />
        <MetricCard title="Saidas do dia" value={formatCurrency(resumo.gastos)} />
        <MetricCard title="Saldo do dia" value={formatCurrency(resumo.saldo)} />
      </section>

      <section className="grid gap-6 lg:grid-cols-[2fr_1fr]">
        <div className="grid gap-6">
          <Card>
            <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
              <div className="space-y-1">
                <CardTitle>Ultimos movimentos</CardTitle>
                <CardDescription>
                  Visao rapida do que voce registrou recentemente.
                </CardDescription>
              </div>
              <Link href="/lancamentos" className={buttonVariants({ variant: "outline" })}>
                Ver todos os lancamentos
              </Link>
            </CardHeader>
            <CardContent className="space-y-3">
              {movimentos.length === 0 ? (
                <p className="text-sm text-muted-foreground">
                  Ainda nao ha movimentos registrados.
                </p>
              ) : (
                movimentos.map((movimento, index) => (
                  <div
                    key={`${movimento.tipo}-${movimento.data}-${index}`}
                    className="flex items-center justify-between rounded-lg border px-4 py-3"
                  >
                    <div>
                      <p className="font-medium">{movimento.descricao}</p>
                      <p className="text-sm text-muted-foreground">
                        {movimento.tipo}
                        {movimento.categoria ? ` • ${movimento.categoria}` : ""}
                        {` • ${movimento.origem}`}
                        {" • "}
                        {formatDateTime(movimento.data)}
                      </p>
                      {movimento.observacao ? (
                        <p className="text-sm text-muted-foreground">{movimento.observacao}</p>
                      ) : null}
                    </div>
                    <span className="font-semibold">
                      {formatCurrency(movimento.valor)}
                    </span>
                  </div>
                ))
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
              <div className="space-y-1">
                <CardTitle>Relatorio mensal</CardTitle>
                <CardDescription>
                  Consolidado do mes atual com saldo e categorias de gasto.
                </CardDescription>
              </div>
              <Badge variant={canAccessMonthlyReport ? "default" : "secondary"}>
                {canAccessMonthlyReport ? "Premium/trial" : "Upgrade"}
              </Badge>
            </CardHeader>
            <CardContent className="space-y-4">
              {monthlyReport ? (
                <>
                  <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                    <MetricCard title="Entradas do mes" value={formatCurrency(monthlyReport.totalReceitas)} />
                    <MetricCard title="Saidas do mes" value={formatCurrency(monthlyReport.totalGastos)} />
                    <MetricCard title="Saldo do mes" value={formatCurrency(monthlyReport.saldo)} />
                    <MetricCard title="Lancamentos" value={String(monthlyReport.totalLancamentos)} />
                  </div>

                  <div className="rounded-lg border px-4 py-3 text-sm text-muted-foreground">
                    Referencia {String(monthlyReport.mes).padStart(2, "0")}/{monthlyReport.ano}
                  </div>

                  <div className="space-y-3">
                    <p className="text-sm font-medium">Top categorias de gasto</p>
                    {monthlyReport.topCategoriasGasto.length === 0 ? (
                      <p className="text-sm text-muted-foreground">
                        Ainda nao ha gastos categorizados neste mes.
                      </p>
                    ) : (
                      monthlyReport.topCategoriasGasto.map((categoria) => (
                        <div
                          key={categoria.categoria}
                          className="flex items-center justify-between rounded-lg border px-4 py-3"
                        >
                          <div>
                            <p className="font-medium">{categoria.categoria}</p>
                            <p className="text-sm text-muted-foreground">
                              {categoria.quantidade} lancamento(s)
                            </p>
                          </div>
                          <span className="font-semibold">
                            {formatCurrency(categoria.totalGasto)}
                          </span>
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
                <div className="rounded-lg border border-dashed p-4 text-sm text-muted-foreground">
                  <p>{billing.mensagemUpgrade ?? "Faça upgrade para liberar o relatorio mensal."}</p>
                  <p className="mt-2">
                    Enquanto o checkout nao entra, o trial e o Premium seguem sendo o caminho para
                    liberar essa visao consolidada.
                  </p>
                </div>
              )}
            </CardContent>
          </Card>

          <div className="grid gap-6 md:grid-cols-2">
            <Card>
              <CardHeader>
                <CardTitle>Novo gasto</CardTitle>
                <CardDescription>Registre uma saida manualmente.</CardDescription>
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

            <Card>
              <CardHeader>
                <CardTitle>Nova receita</CardTitle>
                <CardDescription>Registre uma entrada manualmente.</CardDescription>
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
                  <label className="flex items-center gap-3 rounded-lg border px-3 py-2 text-sm">
                    <Checkbox name="ehFixo" />
                    Receita recorrente
                  </label>
                  <Button type="submit" className="w-full">
                    Salvar receita
                  </Button>
                </form>
              </CardContent>
            </Card>
          </div>
        </div>

        <Card className="h-fit">
          <CardHeader>
            <CardTitle>Plano e quota</CardTitle>
            <CardDescription>
              Estado atual do seu acesso no FinanceBot.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4 text-sm">
            <div className="flex items-center justify-between">
              <span className="text-muted-foreground">Plano efetivo</span>
              <Badge variant={billing.planoEfetivo === "Premium" ? "default" : "secondary"}>
                {billing.planoEfetivo}
              </Badge>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-muted-foreground">Assinatura</span>
              <span>{billing.statusAssinatura}</span>
            </div>
            {billing.upgradeSolicitadoEmUtc ? (
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Upgrade solicitado em</span>
                <span>{formatDateTime(billing.upgradeSolicitadoEmUtc)}</span>
              </div>
            ) : null}
            <div className="flex items-center justify-between">
              <span className="text-muted-foreground">Resumo</span>
              <span className="max-w-[14rem] text-right">{billing.mensagemStatus}</span>
            </div>
            {billing.trialAteUtc ? (
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Trial ate</span>
                <span>{formatDateTime(billing.trialAteUtc)}</span>
              </div>
            ) : null}
            <div className="flex items-center justify-between">
              <span className="text-muted-foreground">Lancamentos no mes</span>
              <span>
                {billing.lancamentosNoMesAtual}
                {billing.limiteLancamentosNoMesAtual
                  ? ` / ${billing.limiteLancamentosNoMesAtual}`
                  : " / ilimitado"}
              </span>
            </div>
            {billing.motivoBloqueio ? (
              <FlashMessage
                title="Atencao"
                message={billing.motivoBloqueio}
                variant="destructive"
              />
            ) : (
              <p className="text-muted-foreground">
                Voce ainda pode registrar lancamentos normalmente.
              </p>
            )}
            {billing.mensagemUpgrade ? (
              <div className="rounded-lg border border-dashed p-3 text-muted-foreground">
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
              <div className="rounded-lg border border-dashed p-3 text-muted-foreground">
                Seu pedido de upgrade ja esta registrado e agora segue nesse fluxo ate a ativacao do Premium.
              </div>
            ) : null}
            <Link href="/plano" className={`${buttonVariants({ variant: "outline" })} w-full`}>
              Abrir pagina de plano
            </Link>
          </CardContent>
        </Card>
      </section>
    </div>
  );
}
