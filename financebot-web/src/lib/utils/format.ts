const currencyFormatter = new Intl.NumberFormat("pt-BR", {
  style: "currency",
  currency: "BRL",
});

const dateTimeFormatter = new Intl.DateTimeFormat("pt-BR", {
  dateStyle: "short",
  timeStyle: "short",
});

const dateFormatter = new Intl.DateTimeFormat("pt-BR", {
  dateStyle: "medium",
});

const percentageFormatter = new Intl.NumberFormat("pt-BR", {
  style: "percent",
  maximumFractionDigits: 0,
});

export function formatCurrency(value: number) {
  return currencyFormatter.format(value);
}

export function formatDateTime(value: string | Date) {
  const date = typeof value === "string" ? new Date(value) : value;
  return dateTimeFormatter.format(date);
}

export function formatDate(value: string | Date) {
  const date = typeof value === "string" ? new Date(value) : value;
  return dateFormatter.format(date);
}

export function formatPercentage(value: number) {
  return percentageFormatter.format(value);
}

export function getMovementTraits(movimento: {
  tipo: string;
  categoria?: string | null;
  ehFixo?: boolean | null;
  ehEssencial?: boolean | null;
}) {
  const traits: string[] = [];

  if (movimento.categoria) {
    traits.push(movimento.categoria);
  }

  if (movimento.ehFixo) {
    traits.push(movimento.tipo === "Receita" ? "Fixa" : "Fixo");
  }

  if (movimento.ehEssencial) {
    traits.push("Essencial");
  }

  return traits;
}
