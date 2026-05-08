import Link from "next/link";
import type { ReactNode } from "react";

import { Filter, LayoutList } from "lucide-react";

import {
  deleteGastoAction,
  deleteReceitaAction,
  updateGastoAction,
  updateReceitaAction,
} from "@/actions/launches";
import { MetricCard } from "@/components/app/metric-card";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getQueryMessage } from "@/lib/action-state";
import { listMovimentos } from "@/lib/financebot-api";
import { requireSession } from "@/lib/session";
import { formatCurrency, formatDate, formatDateTime, formatPercentage } from "@/lib/utils/format";

type LaunchesPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

type MovimentoItem = Awaited<ReturnType<typeof listMovimentos>>[number];

function getSingleValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] ?? "" : value ?? "";
}

function formatDateInput(value: Date) {
  return value.toISOString().slice(0, 10);
}

const selectClassName =
  "h-10 w-full rounded-2xl border border-border/70 bg-background/75 px-3 py-2 text-sm outline-none transition-colors focus-visible:border-primary/40 focus-visible:ring-4 focus-visible:ring-primary/10 dark:bg-input/25";

const textareaClassName =
  "flex min-h-24 w-full rounded-2xl border border-border/70 bg-background/75 px-3 py-2 text-sm outline-none transition-colors placeholder:text-muted-foreground focus-visible:border-primary/40 focus-visible:ring-4 focus-visible:ring-primary/10 dark:bg-input/25";
const ledgerActionButtonClassName = "w-full justify-center";

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
    limite: 200,
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

  if (origem && !origens.includes(origem)) {
    origens.push(origem);
    origens.sort((left, right) => left.localeCompare(right));
  }

  const totalEntradas = movimentos
    .filter((item) => item.tipo === "Receita")
    .reduce((sum, item) => sum + item.valor, 0);
  const totalSaidas = movimentos
    .filter((item) => item.tipo === "Gasto")
    .reduce((sum, item) => sum + item.valor, 0);
  const saldo = totalEntradas - totalSaidas;
  const categoriasGasto = buildCategoryBreakdown(movimentos.filter((item) => item.tipo === "Gasto"));
  const originBreakdown = buildOriginBreakdown(movimentos);
  const returnTo = buildCurrentLaunchesPath({ inicio, fim, tipo, busca, categoria, origem });

  return (
    <div className="space-y-8">
      <section className="app-panel flex flex-col gap-5 p-5 sm:p-6">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div className="space-y-2">
            <p className="text-xs font-medium uppercase tracking-[0.24em] text-muted-foreground">Lancamentos</p>
            <h1 className="text-2xl font-semibold tracking-tight sm:text-3xl">Extrato</h1>
            <p className="max-w-2xl text-sm text-muted-foreground">
              Filtre, revise e ajuste seus lancamentos sem excesso visual.
            </p>
          </div>
          <div className="flex flex-col gap-2 sm:flex-row">
            <Link href="/dashboard" className={buttonVariants({ variant: "outline" })}>
              Voltar ao dashboard
            </Link>
          </div>
        </div>
        <div className="grid gap-3 sm:grid-cols-3">
          <div className="app-data-row p-4">
            <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Registros</p>
            <p className="mt-2 text-xl font-semibold tracking-tight">{movimentos.length}</p>
          </div>
          <div className="app-data-row p-4">
            <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Saldo do filtro</p>
            <p className="mt-2 text-xl font-semibold tracking-tight">{formatCurrency(saldo)}</p>
          </div>
          <div className="app-data-row p-4">
            <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Origem principal</p>
            <p className="mt-2 text-base font-semibold tracking-tight">{originBreakdown[0]?.label ?? "Sem dados"}</p>
          </div>
        </div>
      </section>

      {successMessage ? (
        <FlashBanner tone="success" message={successMessage} />
      ) : null}

      {errorMessage ? (
        <FlashBanner tone="error" message={errorMessage} />
      ) : null}

      <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
        <MetricCard title="Lancamentos encontrados" value={String(movimentos.length)} description="Volume total no filtro." />
        <MetricCard title="Entradas no filtro" value={formatCurrency(totalEntradas)} description="Receitas do periodo." />
        <MetricCard title="Saidas no filtro" value={formatCurrency(totalSaidas)} description="Gastos do periodo." />
        <MetricCard title="Saldo no filtro" value={formatCurrency(saldo)} description="Resultado liquido do recorte." />
      </section>

      <section className="grid gap-6 xl:grid-cols-[1.3fr_0.9fr]">
        <Card className="app-panel">
          <CardHeader>
            <CardTitle>Filtros</CardTitle>
            <CardDescription>
              Refine o extrato por periodo, tipo, categoria, origem ou texto sem perder contexto.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
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
              <div className="space-y-2">
                <Label htmlFor="busca">Buscar por descricao</Label>
                <Input id="busca" name="busca" placeholder="Ex.: mercado, uber, salario" defaultValue={busca} />
              </div>
              <div className="flex flex-col gap-3 md:col-span-2 xl:col-span-3 xl:flex-row">
                <button type="submit" className={buttonVariants({ variant: "default", size: "lg" })}>
                  <Filter className="mr-2 size-4" />
                  Aplicar filtros
                </button>
                <Link href="/lancamentos" className={buttonVariants({ variant: "outline", size: "lg" })}>
                  Limpar filtros
                </Link>
              </div>
            </form>
          </CardContent>
        </Card>

        <Card className="app-panel">
          <CardHeader>
            <CardTitle>Distribuicao do periodo</CardTitle>
            <CardDescription>Leitura compacta de categorias e origem do filtro atual.</CardDescription>
          </CardHeader>
          <CardContent className="space-y-6">
            <div className="space-y-3">
              <p className="text-sm font-medium">Categorias de gasto</p>
              {categoriasGasto.length === 0 ? (
                <EmptyCopy text="Quando houver gastos no filtro, as categorias aparecem aqui." />
              ) : (
                categoriasGasto.map((categoriaItem) => (
                  <BarRow
                    key={categoriaItem.label}
                    label={categoriaItem.label}
                    value={formatCurrency(categoriaItem.total)}
                    percentage={categoriaItem.percentage}
                  />
                ))
              )}
            </div>

            <div className="space-y-3">
              <p className="text-sm font-medium">Origem dos registros</p>
              {originBreakdown.length === 0 ? (
                <EmptyCopy text="A origem Web/Telegram aparece assim que houver dados no periodo." />
              ) : (
                originBreakdown.map((originItem) => (
                  <BarRow
                    key={originItem.label}
                    label={originItem.label}
                    value={`${originItem.count} registro(s)`}
                    percentage={originItem.percentage}
                  />
                ))
              )}
            </div>
          </CardContent>
        </Card>
      </section>

      <Card className="app-panel">
        <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div className="space-y-1">
            <CardTitle>Extrato em lista moderna</CardTitle>
            <CardDescription>
              Uma visualizacao mais legivel no desktop e no mobile, com acoes por item sem sacrificar contexto.
            </CardDescription>
          </div>
          <span className="app-pill">
            <LayoutList className="size-3.5" />
            {movimentos.length} registro(s)
          </span>
        </CardHeader>
        <CardContent className="space-y-4">
          {movimentos.length === 0 ? (
            <EmptyCopy text="Nenhum lancamento encontrado para os filtros atuais." />
          ) : (
            <>
              <div className="hidden overflow-hidden rounded-2xl border border-border/60 lg:block">
                <table className="w-full table-fixed border-collapse">
                  <colgroup>
                    <col className="w-[16%]" />
                    <col className="w-[33%]" />
                    <col className="w-[18%]" />
                    <col className="w-[11%]" />
                    <col className="w-[12%]" />
                    <col className="w-[10%]" />
                  </colgroup>
                  <thead className="bg-muted/30 text-left text-xs font-medium uppercase tracking-[0.24em] text-muted-foreground">
                    <tr>
                      <th className="px-4 py-3">Data</th>
                      <th className="px-4 py-3">Descricao</th>
                      <th className="px-4 py-3">Tipo</th>
                      <th className="px-4 py-3">Origem</th>
                      <th className="px-4 py-3 text-right">Valor</th>
                      <th className="px-4 py-3 text-right">Acoes</th>
                    </tr>
                  </thead>
                  <tbody>
                    {movimentos.map((movimento) => (
                      <LedgerDesktopRow key={movimento.id} movimento={movimento} returnTo={returnTo} />
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="space-y-3 lg:hidden">
                {movimentos.map((movimento) => (
                  <LedgerMobileItem key={movimento.id} movimento={movimento} returnTo={returnTo} />
                ))}
              </div>
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}

function LedgerDesktopRow({
  movimento,
  returnTo,
}: {
  movimento: MovimentoItem;
  returnTo: string;
}) {
  return (
    <tr className="border-t border-border/60 bg-card/65 align-top">
      <td className="px-4 py-4">
        <div className="flex flex-col gap-1">
          <span className="font-medium">{formatDate(movimento.data)}</span>
          <span className="text-sm text-muted-foreground">{formatDateTime(movimento.data)}</span>
        </div>
      </td>
      <td className="px-4 py-4">
        <div className="space-y-1.5">
          <p className="font-medium">{movimento.descricao}</p>
          {movimento.observacao ? (
            <p className="text-sm text-muted-foreground">{movimento.observacao}</p>
          ) : (
            <p className="text-sm text-muted-foreground">Sem observacao adicional.</p>
          )}
        </div>
      </td>
      <td className="px-4 py-4">
        <div className="flex flex-col items-start gap-2">
          <Badge variant={movimento.tipo === "Receita" ? "default" : "secondary"}>
            {movimento.tipo}
          </Badge>
          <span className="text-sm text-muted-foreground">
            {movimento.categoria ?? (movimento.ehFixo ? "Receita fixa" : "Sem categoria")}
          </span>
        </div>
      </td>
      <td className="px-4 py-4">
        <span className="app-pill w-fit">{movimento.origem}</span>
      </td>
      <td className="px-4 py-4 text-right">
        <span className="text-lg font-semibold tracking-tight">{formatCurrency(movimento.valor)}</span>
      </td>
      <td className="px-4 py-4">
        <div className="ml-auto flex w-full max-w-[6.75rem] flex-col items-stretch gap-2">
          <LedgerActions movimento={movimento} returnTo={returnTo} />
        </div>
      </td>
    </tr>
  );
}

function LedgerMobileItem({
  movimento,
  returnTo,
}: {
  movimento: MovimentoItem;
  returnTo: string;
}) {
  return (
    <div className="app-data-row overflow-hidden p-4">
      <div className="grid gap-4 lg:grid-cols-[1.1fr_2.4fr_1.3fr_1fr_1fr_auto] lg:items-start">
        <LedgerCell label="Data">
          <span className="font-medium">{formatDate(movimento.data)}</span>
          <span className="text-sm text-muted-foreground">{formatDateTime(movimento.data)}</span>
        </LedgerCell>

        <LedgerCell label="Descricao">
          <div className="space-y-1.5">
            <p className="font-medium">{movimento.descricao}</p>
            {movimento.observacao ? (
              <p className="text-sm text-muted-foreground">{movimento.observacao}</p>
            ) : (
              <p className="text-sm text-muted-foreground">Sem observacao adicional.</p>
            )}
          </div>
        </LedgerCell>

        <LedgerCell label="Tipo">
          <div className="flex flex-col items-start gap-2">
            <Badge variant={movimento.tipo === "Receita" ? "default" : "secondary"}>
              {movimento.tipo}
            </Badge>
            <span className="text-sm text-muted-foreground">
              {movimento.categoria ?? (movimento.ehFixo ? "Receita fixa" : "Sem categoria")}
            </span>
          </div>
        </LedgerCell>

        <LedgerCell label="Origem">
          <span className="app-pill w-fit">{movimento.origem}</span>
        </LedgerCell>

        <LedgerCell label="Valor">
          <span className="text-lg font-semibold tracking-tight">{formatCurrency(movimento.valor)}</span>
        </LedgerCell>

        <div className="space-y-3 lg:text-right">
          <p className="text-xs font-medium uppercase tracking-[0.24em] text-muted-foreground lg:hidden">
            Acoes
          </p>
          <div className="grid grid-cols-2 gap-2">
            <LedgerActions movimento={movimento} returnTo={returnTo} />
          </div>
        </div>
      </div>
    </div>
  );
}

function LedgerActions({
  movimento,
  returnTo,
}: {
  movimento: MovimentoItem;
  returnTo: string;
}) {
  return (
    <>
      <details className="group w-full">
        <summary
          className={`${buttonVariants({ variant: "outline" })} ${ledgerActionButtonClassName} list-none [&::-webkit-details-marker]:hidden`}
        >
          Editar
        </summary>
        <div className="mt-3 rounded-2xl border border-border/60 bg-background/65 p-4 text-left shadow-xl shadow-black/5 sm:min-w-[28rem]">
          {movimento.tipo === "Gasto" ? (
            <form action={updateGastoAction} className="grid gap-3">
              <input type="hidden" name="gastoId" value={movimento.id} />
              <input type="hidden" name="returnTo" value={returnTo} />
              <div className="grid gap-3 md:grid-cols-2">
                <Field label="Descricao" htmlFor={`descricao-${movimento.id}`}>
                  <Input
                    id={`descricao-${movimento.id}`}
                    name="descricao"
                    defaultValue={movimento.descricao}
                  />
                </Field>
                <Field label="Valor" htmlFor={`valor-${movimento.id}`}>
                  <Input
                    id={`valor-${movimento.id}`}
                    name="valor"
                    type="number"
                    step="0.01"
                    min="0.01"
                    defaultValue={movimento.valor.toFixed(2)}
                  />
                </Field>
                <Field label="Data" htmlFor={`data-${movimento.id}`}>
                  <Input
                    id={`data-${movimento.id}`}
                    name="data"
                    type="date"
                    defaultValue={movimento.data.slice(0, 10)}
                  />
                </Field>
                <Field label="Categoria" htmlFor={`categoria-${movimento.id}`}>
                  <Input
                    id={`categoria-${movimento.id}`}
                    name="categoria"
                    defaultValue={movimento.categoria ?? "Outros"}
                  />
                </Field>
                <Field
                  label="Observacao"
                  htmlFor={`observacao-${movimento.id}`}
                  className="md:col-span-2"
                >
                  <textarea
                    id={`observacao-${movimento.id}`}
                    name="observacao"
                    defaultValue={movimento.observacao ?? ""}
                    className={textareaClassName}
                  />
                </Field>
              </div>
              <button type="submit" className={buttonVariants({ variant: "default" })}>
                Salvar gasto
              </button>
            </form>
          ) : (
            <form action={updateReceitaAction} className="grid gap-3">
              <input type="hidden" name="receitaId" value={movimento.id} />
              <input type="hidden" name="returnTo" value={returnTo} />
              <div className="grid gap-3 md:grid-cols-2">
                <Field label="Descricao" htmlFor={`descricao-${movimento.id}`}>
                  <Input
                    id={`descricao-${movimento.id}`}
                    name="descricao"
                    defaultValue={movimento.descricao}
                  />
                </Field>
                <Field label="Valor" htmlFor={`valor-${movimento.id}`}>
                  <Input
                    id={`valor-${movimento.id}`}
                    name="valor"
                    type="number"
                    step="0.01"
                    min="0.01"
                    defaultValue={movimento.valor.toFixed(2)}
                  />
                </Field>
                <Field label="Data" htmlFor={`data-${movimento.id}`}>
                  <Input
                    id={`data-${movimento.id}`}
                    name="data"
                    type="date"
                    defaultValue={movimento.data.slice(0, 10)}
                  />
                </Field>
                <label className="flex items-center gap-3 rounded-2xl border border-border/60 bg-background/60 px-4 py-3 text-sm text-muted-foreground">
                  <input
                    type="checkbox"
                    name="ehFixo"
                    defaultChecked={movimento.ehFixo ?? false}
                    className="h-4 w-4 rounded border border-input"
                  />
                  Receita fixa
                </label>
                <Field
                  label="Observacao"
                  htmlFor={`observacao-${movimento.id}`}
                  className="md:col-span-2"
                >
                  <textarea
                    id={`observacao-${movimento.id}`}
                    name="observacao"
                    defaultValue={movimento.observacao ?? ""}
                    className={textareaClassName}
                  />
                </Field>
              </div>
              <button type="submit" className={buttonVariants({ variant: "default" })}>
                Salvar receita
              </button>
            </form>
          )}
        </div>
      </details>
      <form action={movimento.tipo === "Gasto" ? deleteGastoAction : deleteReceitaAction} className="w-full">
        <input
          type="hidden"
          name={movimento.tipo === "Gasto" ? "gastoId" : "receitaId"}
          value={movimento.id}
        />
        <input type="hidden" name="returnTo" value={returnTo} />
        <button
          type="submit"
          className={`${buttonVariants({ variant: "destructive" })} ${ledgerActionButtonClassName}`}
        >
          Excluir
        </button>
      </form>
    </>
  );
}

function LedgerCell({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <div className="space-y-1">
      <p className="text-xs font-medium uppercase tracking-[0.24em] text-muted-foreground lg:hidden">
        {label}
      </p>
      <div className="flex flex-col gap-1">{children}</div>
    </div>
  );
}

function Field({
  label,
  htmlFor,
  children,
  className = "",
}: {
  label: string;
  htmlFor: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <div className={`space-y-2 ${className}`}>
      <Label htmlFor={htmlFor}>{label}</Label>
      {children}
    </div>
  );
}

function FlashBanner({
  message,
  tone,
}: {
  message: string;
  tone: "success" | "error";
}) {
  return (
    <div
      className={
        tone === "success"
          ? "rounded-2xl border border-emerald-500/30 bg-emerald-500/10 px-4 py-3 text-sm text-emerald-900 dark:text-emerald-100"
          : "rounded-2xl border border-destructive/30 bg-destructive/10 px-4 py-3 text-sm text-destructive"
      }
    >
      {message}
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
      <div className="mt-3 h-2 overflow-hidden rounded-full bg-muted/80">
        <div
          className="h-full rounded-full bg-gradient-to-r from-primary/65 to-primary"
          style={{ width: `${Math.max(6, Math.round(percentage * 100))}%` }}
        />
      </div>
      <p className="mt-2 text-xs uppercase tracking-[0.24em] text-muted-foreground">
        {formatPercentage(percentage)}
      </p>
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

function buildCategoryBreakdown(movimentos: MovimentoItem[]) {
  const total = movimentos.reduce((sum, movimento) => sum + movimento.valor, 0);
  const grouped = new Map<string, number>();

  for (const movimento of movimentos) {
    const label = movimento.categoria ?? "Outros";
    grouped.set(label, (grouped.get(label) ?? 0) + movimento.valor);
  }

  return [...grouped.entries()]
    .map(([label, categoryTotal]) => ({
      label,
      total: categoryTotal,
      percentage: total > 0 ? categoryTotal / total : 0,
    }))
    .sort((left, right) => right.total - left.total)
    .slice(0, 4);
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
