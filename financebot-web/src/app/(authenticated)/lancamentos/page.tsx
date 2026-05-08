import Link from "next/link";

import {
  deleteGastoAction,
  deleteReceitaAction,
  updateGastoAction,
  updateReceitaAction,
} from "@/actions/launches";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { MetricCard } from "@/components/app/metric-card";
import { getQueryMessage } from "@/lib/action-state";
import { listMovimentos } from "@/lib/financebot-api";
import { requireSession } from "@/lib/session";
import { formatCurrency, formatDate, formatDateTime } from "@/lib/utils/format";

type LaunchesPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function getSingleValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] ?? "" : value ?? "";
}

function formatDateInput(value: Date) {
  return value.toISOString().slice(0, 10);
}

const selectClassName =
  "h-8 w-full rounded-lg border border-input bg-transparent px-2.5 py-1 text-sm outline-none transition-colors focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 dark:bg-input/30";

export default async function LaunchesPage({ searchParams }: LaunchesPageProps) {
  const session = await requireSession();
  const params = await searchParams;
  const now = new Date();
  const firstDayOfMonth = new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), 1));

  const inicio = getSingleValue(params.inicio) || formatDateInput(firstDayOfMonth);
  const fim = getSingleValue(params.fim) || formatDateInput(now);
  const tipo = getSingleValue(params.tipo) || "Todos";
  const busca = getSingleValue(params.busca);
  const categoria = getSingleValue(params.categoria);
  const origem = getSingleValue(params.origem);
  const successMessage = getQueryMessage(params.success);
  const errorMessage = getQueryMessage(params.error);

  const movimentos = await listMovimentos(session.token, {
    inicio,
    fim,
    tipo: tipo === "Todos" ? undefined : tipo,
    busca: busca || undefined,
    categoria: categoria || undefined,
    origem: origem || undefined,
    limite: 150,
  });

  const categorias = [...new Set(movimentos.map((item) => item.categoria).filter((item): item is string => Boolean(item)))].sort(
    (left, right) => left.localeCompare(right),
  );
  const origens = [...new Set(movimentos.map((item) => item.origem).filter(Boolean))].sort((left, right) =>
    left.localeCompare(right),
  );

  if (categoria && !categorias.includes(categoria)) {
    categorias.push(categoria);
    categorias.sort((left, right) => left.localeCompare(right));
  }

  const totalEntradas = movimentos
    .filter((item) => item.tipo === "Receita")
    .reduce((sum, item) => sum + item.valor, 0);
  const totalSaidas = movimentos
    .filter((item) => item.tipo === "Gasto")
    .reduce((sum, item) => sum + item.valor, 0);
  const saldo = totalEntradas - totalSaidas;

  const groupedMovimentos = movimentos.reduce<Record<string, typeof movimentos>>((accumulator, movimento) => {
    const key = movimento.data.slice(0, 10);
    accumulator[key] ??= [];
    accumulator[key].push(movimento);
    return accumulator;
  }, {});

  const orderedDates = Object.keys(groupedMovimentos).sort((left, right) => right.localeCompare(left));
  const returnTo = buildCurrentLaunchesPath({ inicio, fim, tipo, busca, categoria, origem });

  return (
    <div className="space-y-6">
      <section className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div className="space-y-1">
          <h1 className="text-2xl font-semibold tracking-tight">Lancamentos</h1>
          <p className="text-sm text-muted-foreground">
            Sua area de trabalho para revisar gastos e receitas com mais contexto do que o dashboard.
          </p>
        </div>
        <Link href="/dashboard" className={buttonVariants({ variant: "outline" })}>
          Voltar ao dashboard
        </Link>
      </section>

      {successMessage ? (
        <div className="rounded-lg border border-emerald-500/30 bg-emerald-500/10 px-4 py-3 text-sm text-emerald-900 dark:text-emerald-100">
          {successMessage}
        </div>
      ) : null}

      {errorMessage ? (
        <div className="rounded-lg border border-destructive/30 bg-destructive/10 px-4 py-3 text-sm text-destructive">
          {errorMessage}
        </div>
      ) : null}

      <section className="grid gap-4 md:grid-cols-4">
        <MetricCard title="Lancamentos encontrados" value={String(movimentos.length)} />
        <MetricCard title="Entradas no filtro" value={formatCurrency(totalEntradas)} />
        <MetricCard title="Saidas no filtro" value={formatCurrency(totalSaidas)} />
        <MetricCard title="Saldo no filtro" value={formatCurrency(saldo)} />
      </section>

      <Card>
        <CardHeader>
          <CardTitle>Filtros</CardTitle>
          <CardDescription>
            Comece pelo periodo e refine por tipo, categoria, origem ou texto para organizar melhor o extrato.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form className="grid gap-4 md:grid-cols-2 xl:grid-cols-6">
            <div className="space-y-2">
              <Label htmlFor="inicio">Inicio</Label>
              <Input id="inicio" name="inicio" type="date" defaultValue={inicio} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="fim">Fim</Label>
              <Input id="fim" name="fim" type="date" defaultValue={fim} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="tipo">Tipo</Label>
              <select id="tipo" name="tipo" defaultValue={tipo} className={selectClassName}>
                <option value="Todos">Todos</option>
                <option value="Gasto">Gastos</option>
                <option value="Receita">Receitas</option>
              </select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="categoria">Categoria</Label>
              <select id="categoria" name="categoria" defaultValue={categoria} className={selectClassName}>
                <option value="">Todas</option>
                {categorias.map((item) => (
                  <option key={item} value={item}>
                    {item}
                  </option>
                ))}
              </select>
            </div>
            <div className="space-y-2">
              <Label htmlFor="busca">Buscar por descricao</Label>
              <Input id="busca" name="busca" placeholder="Ex.: mercado, uber, salario" defaultValue={busca} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="origem">Origem</Label>
              <select id="origem" name="origem" defaultValue={origem} className={selectClassName}>
                <option value="">Todas</option>
                {origens.map((item) => (
                  <option key={item} value={item}>
                    {item}
                  </option>
                ))}
              </select>
            </div>
            <div className="flex flex-col gap-3 md:col-span-2 xl:col-span-6 xl:flex-row">
              <button type="submit" className={buttonVariants({ variant: "default" })}>
                Aplicar filtros
              </button>
              <Link href="/lancamentos" className={buttonVariants({ variant: "outline" })}>
                Limpar filtros
              </Link>
            </div>
          </form>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Extrato organizado</CardTitle>
          <CardDescription>
            {movimentos.length === 0
              ? "Nenhum lancamento encontrado para os filtros atuais."
              : "Lista unificada de gastos e receitas agrupada por data, com origem e observacoes."}
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          {movimentos.length === 0 ? (
            <div className="rounded-lg border border-dashed p-6 text-sm text-muted-foreground">
              Ajuste o periodo, limpe a busca ou volte ao dashboard para registrar um novo lancamento.
            </div>
          ) : (
            orderedDates.map((dateKey) => (
                <div key={dateKey} className="space-y-3">
                  <div className="flex items-center justify-between border-b pb-2">
                    <div>
                      <p className="font-medium">{formatDate(dateKey)}</p>
                      <p className="text-sm text-muted-foreground">
                        {groupedMovimentos[dateKey].length} lancamento(s) • {formatDailySummary(groupedMovimentos[dateKey])}
                      </p>
                    </div>
                    <span className="text-sm text-muted-foreground">{formatCurrency(getDailySaldo(groupedMovimentos[dateKey]))}</span>
                  </div>
                <div className="space-y-3">
                  {groupedMovimentos[dateKey].map((movimento) => (
                    <div
                      key={movimento.id}
                      className="space-y-4 rounded-lg border px-4 py-3"
                    >
                      <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                        <div className="space-y-1">
                          <div className="flex flex-wrap items-center gap-2">
                            <Badge variant={movimento.tipo === "Receita" ? "default" : "secondary"}>
                              {movimento.tipo}
                            </Badge>
                            {movimento.categoria ? (
                              <Badge variant="outline">{movimento.categoria}</Badge>
                            ) : null}
                            <Badge variant="outline">{movimento.origem}</Badge>
                          </div>
                          <p className="font-medium">{movimento.descricao}</p>
                          <p className="text-sm text-muted-foreground">
                            Registrado em {formatDateTime(movimento.data)}
                          </p>
                          {movimento.observacao ? (
                            <p className="text-sm text-muted-foreground">{movimento.observacao}</p>
                          ) : null}
                        </div>
                        <div className="flex flex-col items-start gap-3 sm:items-end">
                          <span className="text-base font-semibold">{formatCurrency(movimento.valor)}</span>
                          <div className="flex flex-wrap gap-2">
                            <details className="group">
                              <summary className={buttonVariants({ variant: "outline" })}>Editar</summary>
                              <div className="mt-3 w-full min-w-0 rounded-lg border bg-muted/20 p-4 sm:min-w-[420px]">
                                {movimento.tipo === "Gasto" ? (
                                  <form action={updateGastoAction} className="grid gap-3">
                                    <input type="hidden" name="gastoId" value={movimento.id} />
                                    <input type="hidden" name="returnTo" value={returnTo} />
                                    <div className="grid gap-3 md:grid-cols-2">
                                      <div className="space-y-2 md:col-span-2">
                                        <Label htmlFor={`descricao-${movimento.id}`}>Descricao</Label>
                                        <Input
                                          id={`descricao-${movimento.id}`}
                                          name="descricao"
                                          defaultValue={movimento.descricao}
                                        />
                                      </div>
                                      <div className="space-y-2">
                                        <Label htmlFor={`valor-${movimento.id}`}>Valor</Label>
                                        <Input
                                          id={`valor-${movimento.id}`}
                                          name="valor"
                                          type="number"
                                          step="0.01"
                                          min="0.01"
                                          defaultValue={movimento.valor.toFixed(2)}
                                        />
                                      </div>
                                      <div className="space-y-2">
                                        <Label htmlFor={`data-${movimento.id}`}>Data</Label>
                                        <Input
                                          id={`data-${movimento.id}`}
                                          name="data"
                                          type="date"
                                          defaultValue={movimento.data.slice(0, 10)}
                                        />
                                      </div>
                                      <div className="space-y-2 md:col-span-2">
                                        <Label htmlFor={`categoria-${movimento.id}`}>Categoria</Label>
                                        <Input
                                          id={`categoria-${movimento.id}`}
                                          name="categoria"
                                          defaultValue={movimento.categoria ?? "Outros"}
                                        />
                                      </div>
                                      <div className="space-y-2 md:col-span-2">
                                        <Label htmlFor={`observacao-${movimento.id}`}>Observacao</Label>
                                        <textarea
                                          id={`observacao-${movimento.id}`}
                                          name="observacao"
                                          defaultValue={movimento.observacao ?? ""}
                                          className="flex min-h-20 w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-xs outline-none transition-[color,box-shadow] placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-ring/50 focus-visible:ring-[3px] dark:bg-input/30"
                                        />
                                      </div>
                                    </div>
                                    <div className="flex flex-wrap gap-2">
                                      <button type="submit" className={buttonVariants({ variant: "default" })}>
                                        Salvar gasto
                                      </button>
                                    </div>
                                  </form>
                                ) : (
                                  <form action={updateReceitaAction} className="grid gap-3">
                                    <input type="hidden" name="receitaId" value={movimento.id} />
                                    <input type="hidden" name="returnTo" value={returnTo} />
                                    <div className="grid gap-3 md:grid-cols-2">
                                      <div className="space-y-2 md:col-span-2">
                                        <Label htmlFor={`descricao-${movimento.id}`}>Descricao</Label>
                                        <Input
                                          id={`descricao-${movimento.id}`}
                                          name="descricao"
                                          defaultValue={movimento.descricao}
                                        />
                                      </div>
                                      <div className="space-y-2">
                                        <Label htmlFor={`valor-${movimento.id}`}>Valor</Label>
                                        <Input
                                          id={`valor-${movimento.id}`}
                                          name="valor"
                                          type="number"
                                          step="0.01"
                                          min="0.01"
                                          defaultValue={movimento.valor.toFixed(2)}
                                        />
                                      </div>
                                      <div className="space-y-2">
                                        <Label htmlFor={`data-${movimento.id}`}>Data</Label>
                                        <Input
                                          id={`data-${movimento.id}`}
                                          name="data"
                                          type="date"
                                          defaultValue={movimento.data.slice(0, 10)}
                                        />
                                      </div>
                                      <label className="flex items-center gap-2 text-sm text-muted-foreground md:col-span-2">
                                        <input
                                          type="checkbox"
                                          name="ehFixo"
                                          defaultChecked={movimento.ehFixo ?? false}
                                          className="h-4 w-4 rounded border border-input"
                                        />
                                        Receita fixa
                                      </label>
                                      <div className="space-y-2 md:col-span-2">
                                        <Label htmlFor={`observacao-${movimento.id}`}>Observacao</Label>
                                        <textarea
                                          id={`observacao-${movimento.id}`}
                                          name="observacao"
                                          defaultValue={movimento.observacao ?? ""}
                                          className="flex min-h-20 w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-xs outline-none transition-[color,box-shadow] placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-ring/50 focus-visible:ring-[3px] dark:bg-input/30"
                                        />
                                      </div>
                                    </div>
                                    <div className="flex flex-wrap gap-2">
                                      <button type="submit" className={buttonVariants({ variant: "default" })}>
                                        Salvar receita
                                      </button>
                                    </div>
                                  </form>
                                )}
                              </div>
                            </details>
                            <form action={movimento.tipo === "Gasto" ? deleteGastoAction : deleteReceitaAction}>
                              <input
                                type="hidden"
                                name={movimento.tipo === "Gasto" ? "gastoId" : "receitaId"}
                                value={movimento.id}
                              />
                              <input type="hidden" name="returnTo" value={returnTo} />
                              <button type="submit" className={buttonVariants({ variant: "destructive" })}>
                                Excluir
                              </button>
                            </form>
                          </div>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            ))
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function buildCurrentLaunchesPath(filters: {
  inicio: string;
  fim: string;
  tipo: string;
  busca: string;
  categoria: string;
  origem: string;
}) {
  const params = new URLSearchParams();
  params.set("inicio", filters.inicio);
  params.set("fim", filters.fim);

  if (filters.tipo && filters.tipo !== "Todos") {
    params.set("tipo", filters.tipo);
  }

  if (filters.busca) {
    params.set("busca", filters.busca);
  }

  if (filters.categoria) {
    params.set("categoria", filters.categoria);
  }

  if (filters.origem) {
    params.set("origem", filters.origem);
  }

  const query = params.toString();
  return query.length > 0 ? `/lancamentos?${query}` : "/lancamentos";
}

function getDailySaldo(movimentos: Awaited<ReturnType<typeof listMovimentos>>) {
  return movimentos.reduce((sum, movimento) => {
    return movimento.tipo === "Receita" ? sum + movimento.valor : sum - movimento.valor;
  }, 0);
}

function formatDailySummary(movimentos: Awaited<ReturnType<typeof listMovimentos>>) {
  const entradas = movimentos
    .filter((movimento) => movimento.tipo === "Receita")
    .reduce((sum, movimento) => sum + movimento.valor, 0);
  const saidas = movimentos
    .filter((movimento) => movimento.tipo === "Gasto")
    .reduce((sum, movimento) => sum + movimento.valor, 0);

  return `Entradas ${formatCurrency(entradas)} • Saidas ${formatCurrency(saidas)}`;
}
