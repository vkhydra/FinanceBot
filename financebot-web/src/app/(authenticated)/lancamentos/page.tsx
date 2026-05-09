import Link from "next/link";

import { Filter, LayoutList } from "lucide-react";

import {
  deleteGastoAction,
  deleteReceitaAction,
} from "@/actions/launches";
import { LaunchEditModal } from "@/components/app/launch-edit-modal";
import { LaunchDateRangePicker } from "@/components/app/launch-date-range-picker";
import { LaunchEntryModal } from "@/components/app/launch-entry-modal";
import { MetricCard } from "@/components/app/metric-card";
import { PageSectionNav } from "@/components/app/page-section-nav";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { fieldSelectClassName, Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getQueryMessage } from "@/lib/action-state";
import { listMovimentos } from "@/lib/financebot-api";
import { requireSession } from "@/lib/session";
import { formatCurrency, formatDate, formatDateTime, formatPercentage, getMovementTraits } from "@/lib/utils/format";

type LaunchesPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

type MovimentoItem = Awaited<ReturnType<typeof listMovimentos>>[number];
const launchesSections = [
  { id: "extrato", label: "Extrato", description: "Tabela com filtros e acoes por item." },
  { id: "visao-geral", label: "Visao geral", description: "Resumo rapido do periodo filtrado." },
] as const;

type LaunchesSection = (typeof launchesSections)[number]["id"];

function getSingleValue(value: string | string[] | undefined) {
  return Array.isArray(value) ? value[0] ?? "" : value ?? "";
}

function formatDateInput(value: Date) {
  return value.toISOString().slice(0, 10);
}

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
  const activeSection = resolveLaunchesSection(getSingleValue(params.secao));
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
  const ledgerGroups = buildLedgerGroups(movimentos);
  const activeFilterChips = buildActiveFilterChips({ tipo, categoria, origem, busca });
  const returnTo = buildCurrentLaunchesPath({ inicio, fim, tipo, busca, categoria, origem, secao: "extrato" });
  const filterPresets = [
    {
      label: "Hoje",
      href: buildCurrentLaunchesPath({
        inicio: formatDateInput(now),
        fim: formatDateInput(now),
        tipo,
        busca,
        categoria,
        origem,
        secao: "extrato",
      }),
      active: inicio === formatDateInput(now) && fim === formatDateInput(now),
    },
    {
      label: "7 dias",
      href: buildCurrentLaunchesPath({
        inicio: formatDateInput(addUtcDays(now, -6)),
        fim: formatDateInput(now),
        tipo,
        busca,
        categoria,
        origem,
        secao: "extrato",
      }),
      active: inicio === formatDateInput(addUtcDays(now, -6)) && fim === formatDateInput(now),
    },
    {
      label: "30 dias",
      href: buildCurrentLaunchesPath({
        inicio: formatDateInput(addUtcDays(now, -29)),
        fim: formatDateInput(now),
        tipo,
        busca,
        categoria,
        origem,
        secao: "extrato",
      }),
      active: inicio === formatDateInput(addUtcDays(now, -29)) && fim === formatDateInput(now),
    },
    {
      label: "Mes atual",
      href: buildCurrentLaunchesPath({
        inicio: formatDateInput(firstDayOfMonth),
        fim: formatDateInput(now),
        tipo,
        busca,
        categoria,
        origem,
        secao: "extrato",
      }),
      active: inicio === formatDateInput(firstDayOfMonth) && fim === formatDateInput(now),
    },
  ];

  return (
    <div className="space-y-8">
      <section className="app-panel flex flex-col gap-5 p-5 sm:p-6">
        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
          <div className="space-y-2">
            <p className="text-xs font-medium uppercase tracking-[0.24em] text-muted-foreground">Lancamentos</p>
            <h1 className="text-2xl font-semibold tracking-tight sm:text-3xl">Extrato</h1>
            <p className="max-w-2xl text-sm text-muted-foreground">
              Veja, filtre e ajuste seus lancamentos sem excesso visual.
            </p>
          </div>
          <div className="grid grid-cols-2 gap-2 sm:flex sm:flex-row">
            <LaunchEntryModal redirectTo={returnTo} className="h-11 w-full justify-center rounded-2xl sm:w-auto" />
            <Link
              href="/dashboard"
              className={`${buttonVariants({ variant: "outline" })} h-11 w-full justify-center rounded-2xl sm:w-auto`}
            >
              Voltar ao dashboard
            </Link>
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3 md:grid-cols-3">
          <div className="app-data-row min-w-0 p-4">
            <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Registros</p>
            <p className="mt-2 break-words text-base font-semibold tracking-tight sm:text-xl">{movimentos.length}</p>
          </div>
          <div className="app-data-row min-w-0 p-4">
            <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Saldo</p>
            <p className="mt-2 break-words text-base font-semibold tracking-tight sm:text-xl">{formatCurrency(saldo)}</p>
          </div>
          <div className="app-data-row col-span-2 min-w-0 p-4 md:col-span-1">
            <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Origem</p>
            <p className="mt-2 break-words text-sm font-semibold tracking-tight sm:text-base">
              {originBreakdown[0]?.label ?? "Sem dados"}
            </p>
          </div>
        </div>
      </section>

      {successMessage ? (
        <FlashBanner tone="success" message={successMessage} />
      ) : null}

      {errorMessage ? (
        <FlashBanner tone="error" message={errorMessage} />
      ) : null}

      <PageSectionNav
        items={launchesSections.map((section) => ({
          href: buildCurrentLaunchesPath({ inicio, fim, tipo, busca, categoria, origem, secao: section.id }),
          label: section.label,
          description: section.description,
          active: section.id === activeSection,
        }))}
      />

      {activeSection === "visao-geral" ? (
        <>
          <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            <MetricCard title="Lancamentos encontrados" value={String(movimentos.length)} description="Volume total no filtro." />
            <MetricCard title="Entradas no filtro" value={formatCurrency(totalEntradas)} description="Receitas do periodo." />
            <MetricCard title="Saidas no filtro" value={formatCurrency(totalSaidas)} description="Gastos do periodo." />
            <MetricCard title="Saldo no filtro" value={formatCurrency(saldo)} description="Resultado liquido do recorte." />
          </section>

          <section className="w-full">
            <Card className="app-panel w-full">
              <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <CardTitle>Distribuicao do periodo</CardTitle>
                  <CardDescription>Leitura compacta de categorias e origem do filtro atual.</CardDescription>
                </div>
                <Link href={buildCurrentLaunchesPath({ inicio, fim, tipo, busca, categoria, origem, secao: "extrato" })} className={buttonVariants({ variant: "outline" })}>
                  Abrir extrato
                </Link>
              </CardHeader>
              <CardContent className="grid gap-6 lg:grid-cols-2">
                <div className="space-y-3">
                  <p className="text-sm font-medium">Categorias de gasto</p>
                  {categoriasGasto.length === 0 ? (
                    <EmptyCopy text="Quando houver gastos no filtro, as categorias aparecem aqui." />
                  ) : (
                    <div className="grid gap-2 sm:grid-cols-2">
                      {categoriasGasto.map((categoriaItem) => (
                        <BarRow
                          key={categoriaItem.label}
                          label={categoriaItem.label}
                          value={formatCurrency(categoriaItem.total)}
                          percentage={categoriaItem.percentage}
                          compact
                        />
                      ))}
                    </div>
                  )}
                </div>

                <div className="space-y-3">
                  <p className="text-sm font-medium">Origem dos registros</p>
                  {originBreakdown.length === 0 ? (
                    <EmptyCopy text="A origem Web/Telegram aparece assim que houver dados no periodo." />
                  ) : (
                    <div className="grid gap-2 sm:grid-cols-2">
                      {originBreakdown.map((originItem) => (
                        <BarRow
                          key={originItem.label}
                          label={originItem.label}
                          value={`${originItem.count} registro(s)`}
                          percentage={originItem.percentage}
                          compact
                        />
                      ))}
                    </div>
                  )}
                </div>
              </CardContent>
            </Card>
          </section>
        </>
      ) : null}

      {activeSection === "extrato" ? (
        <section className="space-y-6">
          <Card className="app-panel">
            <CardContent className="grid gap-4 p-5 md:grid-cols-[1.1fr_0.9fr]">
              <div className="min-w-0 space-y-3">
                <div className="space-y-1">
                  <p className="text-sm font-medium">Periodo atual</p>
                  <p className="text-sm text-muted-foreground">
                    De {formatDate(inicio)} ate {formatDate(fim)} com {movimentos.length} registro(s).
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  <span className="app-pill">Extrato aberto</span>
                  {activeFilterChips.length === 0 ? (
                    <span className="app-pill">Sem filtros</span>
                  ) : (
                    activeFilterChips.map((chip) => (
                      <span key={chip} className="app-pill">
                        {chip}
                      </span>
                    ))
                  )}
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
                <div className="app-data-row min-w-0 p-4">
                  <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Entrou</p>
                  <p className="mt-2 break-words text-sm font-semibold tracking-tight sm:text-base">{formatCurrency(totalEntradas)}</p>
                </div>
                <div className="app-data-row min-w-0 p-4">
                  <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Saiu</p>
                  <p className="mt-2 break-words text-sm font-semibold tracking-tight sm:text-base">{formatCurrency(totalSaidas)}</p>
                </div>
                <div className="app-data-row col-span-2 min-w-0 p-4 sm:col-span-1">
                  <p className="text-xs uppercase tracking-[0.24em] text-muted-foreground">Saldo</p>
                  <p className="mt-2 break-words text-sm font-semibold tracking-tight sm:text-base">{formatCurrency(saldo)}</p>
                </div>
              </div>
            </CardContent>
          </Card>

          <Card className="app-panel">
            <CardHeader>
              <CardTitle>Filtrar extrato</CardTitle>
              <CardDescription>
                Escolha periodo, tipo, categoria, origem ou busca.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="mb-4 flex flex-wrap gap-2">
                {filterPresets.map((preset) => (
                  <Link
                    key={preset.label}
                    href={preset.href}
                    className={buttonVariants({ variant: preset.active ? "default" : "outline", size: "sm" })}
                  >
                    {preset.label}
                  </Link>
                ))}
              </div>
              <form className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                <input type="hidden" name="secao" value="extrato" />
                <LaunchDateRangePicker initialStart={inicio} initialEnd={fim} />
                <div className="min-w-0 space-y-2">
                  <Label htmlFor="tipo">Tipo</Label>
                  <select id="tipo" name="tipo" defaultValue={tipo} className={fieldSelectClassName}>
                    <option value="Todos">Todos</option>
                    <option value="Gasto">Gastos</option>
                    <option value="Receita">Receitas</option>
                  </select>
                </div>
                <div className="min-w-0 space-y-2">
                  <Label htmlFor="categoria">Categoria</Label>
                  <select id="categoria" name="categoria" defaultValue={categoria} className={fieldSelectClassName}>
                    <option value="">Todas</option>
                    {categorias.map((item) => (
                      <option key={item} value={item}>
                        {item}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="min-w-0 space-y-2">
                  <Label htmlFor="origem">Origem</Label>
                  <select id="origem" name="origem" defaultValue={origem} className={fieldSelectClassName}>
                    <option value="">Todas</option>
                    {origens.map((item) => (
                      <option key={item} value={item}>
                        {item}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="min-w-0 space-y-2">
                  <Label htmlFor="busca">Buscar</Label>
                  <Input
                    id="busca"
                    name="busca"
                    placeholder="Ex.: mercado, uber, salario"
                    defaultValue={busca}
                  />
                </div>
                <div className="flex flex-col gap-3 md:col-span-2 xl:col-span-3 xl:flex-row">
                  <button type="submit" className={buttonVariants({ variant: "default", size: "lg" })}>
                    <Filter className="mr-2 size-4" />
                    Filtrar
                  </button>
                  <Link href="/lancamentos" className={buttonVariants({ variant: "outline", size: "lg" })}>
                    Limpar
                  </Link>
                </div>
              </form>
            </CardContent>
          </Card>

          <Card className="app-panel">
            <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
              <div className="space-y-1">
                <CardTitle>Extrato por dia</CardTitle>
                <CardDescription>
                  Lista compacta, com o mesmo raciocinio no desktop e no mobile.
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
                  <div className="space-y-4">
                    {ledgerGroups.map((group) => (
                      <section key={group.dateKey} className="space-y-3 rounded-2xl border border-border/60 bg-background/35 p-3 sm:p-4">
                        <div className="flex flex-col gap-3 rounded-2xl border border-border/60 bg-background/70 p-4 sm:flex-row sm:items-center sm:justify-between">
                          <div className="min-w-0 space-y-1">
                            <p className="text-sm font-medium">{formatDate(group.dateKey)}</p>
                            <p className="text-sm text-muted-foreground">
                              {group.items.length} registro(s) neste dia
                            </p>
                          </div>
                          <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 sm:gap-3">
                            <DayMetric label="Entrou" value={formatCurrency(group.entradas)} />
                            <DayMetric label="Saiu" value={formatCurrency(group.saidas)} />
                            <DayMetric label="Saldo" value={formatCurrency(group.saldo)} className="col-span-2 sm:col-span-1" />
                          </div>
                        </div>

                        <div className="space-y-3">
                          {group.items.map((movimento) => (
                            <LedgerEntryCard key={movimento.id} movimento={movimento} returnTo={returnTo} />
                          ))}
                        </div>
                      </section>
                    ))}
                  </div>
                </>
              )}
            </CardContent>
          </Card>
        </section>
      ) : null}
    </div>
  );
}

function LedgerEntryCard({
  movimento,
  returnTo,
}: {
  movimento: MovimentoItem;
  returnTo: string;
}) {
  const traits = getMovementTraits(movimento);

  return (
    <div className="app-data-row overflow-hidden p-4">
      <div className="grid gap-4 xl:grid-cols-[7rem_minmax(0,1fr)_9rem_9.5rem] xl:items-center">
        <div className="flex items-start justify-between gap-3 xl:block">
          <div className="space-y-1">
            <p className="font-medium">{formatDate(movimento.data)}</p>
            <p className="text-sm text-muted-foreground">{formatTime(movimento.data)}</p>
          </div>
          <span
            className={`text-lg font-semibold tracking-tight xl:hidden ${
              movimento.tipo === "Receita" ? "text-emerald-600 dark:text-emerald-400" : "text-rose-600 dark:text-rose-400"
            }`}
          >
            {formatCurrency(movimento.valor)}
          </span>
        </div>

        <div className="space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <Badge variant={movimento.tipo === "Receita" ? "default" : "secondary"}>
              {movimento.tipo}
            </Badge>
            <span className="app-pill w-fit">{movimento.origem}</span>
            {traits.map((trait) => (
              <Badge key={`${movimento.id}-${trait}`} variant="outline">
                {trait}
              </Badge>
            ))}
          </div>

          <div className="space-y-1.5">
            <p className="font-medium">{movimento.descricao}</p>
            {movimento.observacao ? <p className="text-sm text-muted-foreground">{movimento.observacao}</p> : null}
            <p className="text-sm text-muted-foreground">{formatDateTime(movimento.data)}</p>
          </div>
        </div>

        <div className="hidden xl:block text-right">
          <span
            className={`text-lg font-semibold tracking-tight ${
              movimento.tipo === "Receita" ? "text-emerald-600 dark:text-emerald-400" : "text-rose-600 dark:text-rose-400"
            }`}
          >
            {formatCurrency(movimento.valor)}
          </span>
          <p className="mt-1 text-sm text-muted-foreground">
            {movimento.tipo === "Receita" ? "Entrada no dia" : "Saida no dia"}
          </p>
        </div>

        <div className="grid grid-cols-2 gap-2 xl:grid-cols-1">
          <LedgerActions movimento={movimento} returnTo={returnTo} />
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
      <LaunchEditModal movimento={movimento} returnTo={returnTo} className={ledgerActionButtonClassName} />
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
  compact = false,
}: {
  label: string;
  value: string;
  percentage: number;
  compact?: boolean;
}) {
  return (
    <div className={`app-data-row ${compact ? "p-3" : "p-4"}`}>
      <div className="flex items-center justify-between gap-3">
        <p className={compact ? "text-sm font-medium" : "font-medium"}>{label}</p>
        <span className={compact ? "text-xs text-muted-foreground" : "text-sm text-muted-foreground"}>{value}</span>
      </div>
      <div className={`${compact ? "mt-2" : "mt-3"} h-2 overflow-hidden rounded-full bg-muted/80`}>
        <div
          className="h-full rounded-full bg-gradient-to-r from-primary/65 to-primary"
          style={{ width: `${Math.max(6, Math.round(percentage * 100))}%` }}
        />
      </div>
      <p className={compact ? "mt-1 text-[11px] text-muted-foreground" : "mt-2 text-xs uppercase tracking-[0.24em] text-muted-foreground"}>
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

function DayMetric({ label, value, className = "" }: { label: string; value: string; className?: string }) {
  return (
    <div className={`app-data-row min-w-0 p-2.5 sm:p-3 ${className}`}>
      <p className="truncate text-[11px] uppercase tracking-[0.24em] text-muted-foreground">{label}</p>
      <p className="mt-1 break-words text-sm font-semibold leading-tight tracking-tight sm:text-base">{value}</p>
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
  secao: LaunchesSection;
}) {
  const params = new URLSearchParams();
  params.set("inicio", filters.inicio);
  params.set("fim", filters.fim);
  params.set("secao", filters.secao);

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

function buildLedgerGroups(movimentos: MovimentoItem[]) {
  const grouped = new Map<
    string,
    { dateKey: string; items: MovimentoItem[]; entradas: number; saidas: number; saldo: number }
  >();

  for (const movimento of movimentos) {
    const dateKey = movimento.data.slice(0, 10);
    const current = grouped.get(dateKey) ?? {
      dateKey,
      items: [],
      entradas: 0,
      saidas: 0,
      saldo: 0,
    };

    current.items.push(movimento);

    if (movimento.tipo === "Receita") {
      current.entradas += movimento.valor;
      current.saldo += movimento.valor;
    } else {
      current.saidas += movimento.valor;
      current.saldo -= movimento.valor;
    }

    grouped.set(dateKey, current);
  }

  return [...grouped.values()]
    .map((group) => ({
      ...group,
      items: [...group.items].sort((left, right) => right.data.localeCompare(left.data)),
    }))
    .sort((left, right) => right.dateKey.localeCompare(left.dateKey));
}

function buildActiveFilterChips(filters: {
  tipo: string;
  categoria: string;
  origem: string;
  busca: string;
}) {
  const chips: string[] = [];

  if (filters.tipo && filters.tipo !== "Todos") {
    chips.push(`Tipo: ${filters.tipo}`);
  }

  if (filters.categoria) {
    chips.push(`Categoria: ${filters.categoria}`);
  }

  if (filters.origem) {
    chips.push(`Origem: ${filters.origem}`);
  }

  if (filters.busca) {
    chips.push(`Busca: ${filters.busca}`);
  }

  return chips;
}

function resolveLaunchesSection(value: string): LaunchesSection {
  return launchesSections.some((section) => section.id === value)
    ? (value as LaunchesSection)
    : "extrato";
}

function formatTime(value: string | Date) {
  const date = typeof value === "string" ? new Date(value) : value;
  return new Intl.DateTimeFormat("pt-BR", { timeStyle: "short" }).format(date);
}

function addUtcDays(value: Date, days: number) {
  const next = new Date(value);
  next.setUTCDate(next.getUTCDate() + days);
  return next;
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
