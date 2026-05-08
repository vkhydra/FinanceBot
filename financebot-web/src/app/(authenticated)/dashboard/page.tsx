import { createGastoAction, createReceitaAction } from "@/actions/finance";
import { FlashMessage } from "@/components/app/flash-message";
import { MetricCard } from "@/components/app/metric-card";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { getBillingStatus, getMovimentos, getResumo } from "@/lib/financebot-api";
import { getQueryMessage } from "@/lib/action-state";
import { requireSession } from "@/lib/session";
import { formatCurrency, formatDateTime } from "@/lib/utils/format";

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
    getMovimentos(session.token),
    getBillingStatus(session.token),
  ]);

  return (
    <div className="space-y-6">
      {error ? (
        <FlashMessage title="Algo deu errado" message={error} variant="destructive" />
      ) : null}
      {success ? (
        <FlashMessage title="Tudo certo" message={success} />
      ) : null}

      <section className="grid gap-4 md:grid-cols-3">
        <MetricCard title="Entradas do dia" value={formatCurrency(resumo.ganhos)} />
        <MetricCard title="Saidas do dia" value={formatCurrency(resumo.gastos)} />
        <MetricCard title="Saldo do dia" value={formatCurrency(resumo.saldo)} />
      </section>

      <section className="grid gap-6 lg:grid-cols-[2fr_1fr]">
        <div className="grid gap-6">
          <Card>
            <CardHeader>
              <CardTitle>Ultimos movimentos</CardTitle>
              <CardDescription>
                Visao rapida do que voce registrou recentemente.
              </CardDescription>
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
                        {movimento.tipo} • {formatDateTime(movimento.data)}
                      </p>
                    </div>
                    <span className="font-semibold">
                      {formatCurrency(movimento.valor)}
                    </span>
                  </div>
                ))
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
          </CardContent>
        </Card>
      </section>
    </div>
  );
}
