import "server-only";

const DEFAULT_API_BASE_URL = "http://127.0.0.1:5057";

export type AuthResponse = {
  usuarioId: string;
  email: string;
  token: string;
  expiraEmUtc: string;
};

export type BillingStatusResponse = {
  usuarioId: string;
  planoAtual: string;
  planoEfetivo: string;
  statusAssinatura: string;
  premiumAteUtc: string | null;
  trialAteUtc: string | null;
  upgradeSolicitadoEmUtc: string | null;
  lancamentosNoMesAtual: number;
  limiteLancamentosNoMesAtual: number | null;
  podeRegistrarLancamento: boolean;
  upgradePendente: boolean;
  podeSolicitarUpgrade: boolean;
  motivoBloqueio: string | null;
  trialAtivo: boolean;
  diasRestantesTrial: number | null;
  mensagemStatus: string;
  mensagemUpgrade: string | null;
};

export type ResumoResponse = {
  data: string;
  ganhos: number;
  gastos: number;
  saldo: number;
};

export type MonthlyBudgetResponse = {
  ano: number;
  mes: number;
  limiteGastos: number | null;
  totalGastos: number;
  totalReceitas: number;
  gastoFixo: number;
  gastoEssencial: number;
  gastoNaoEssencial: number;
  restante: number | null;
  percentualConsumido: number | null;
  projecaoFechamento: number;
  diferencaProjetada: number | null;
  diasNoMes: number;
  diasDecorridos: number;
  diasRestantes: number;
  possuiOrcamentoDefinido: boolean;
  estourado: boolean;
  estouroProjetado: boolean;
  sugestaoLimiteSeguro: number | null;
  sugestaoLimiteEquilibrado: number | null;
  sugestaoLimiteFlexivel: number | null;
  mesesBaseSugestao: number;
};

export type MovimentoResponse = {
  id: string;
  tipo: string;
  descricao: string;
  valor: number;
  data: string;
  categoria?: string | null;
  ehFixo?: boolean | null;
  ehEssencial?: boolean | null;
  origem: string;
  observacao?: string | null;
};

export type GastoResponse = {
  id: string;
  descricao: string;
  valor: number;
  data: string;
  categoria: string;
  ehFixo: boolean;
  ehEssencial: boolean;
  origem: string;
  observacao?: string | null;
};

export type ReceitaResponse = {
  id: string;
  descricao: string;
  valor: number;
  data: string;
  ehFixo: boolean;
  origem: string;
  observacao?: string | null;
};

export type CodigoVinculoResponse = {
  codigo: string;
  expiraEmUtc: string;
};

export type DesvinculoTelegramResponse = {
  sucesso: boolean;
  mensagem: string;
};

export type CategoriaResumoResponse = {
  categoria: string;
  totalGasto: number;
  quantidade: number;
};

export type MonthlyReportResponse = {
  ano: number;
  mes: number;
  totalReceitas: number;
  totalGastos: number;
  saldo: number;
  totalLancamentos: number;
  topCategoriasGasto: CategoriaResumoResponse[];
};

export type UpgradeRequestResponse = {
  sucesso: boolean;
  mensagem: string;
  solicitadoEmUtc: string | null;
  upgradePendente: boolean;
};

export class FinanceBotApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

function getApiBaseUrl() {
  return process.env.FINANCEBOT_API_BASE_URL ?? DEFAULT_API_BASE_URL;
}

async function parseErrorMessage(response: Response) {
  try {
    const payload = (await response.json()) as { message?: string };
    if (typeof payload.message === "string" && payload.message.length > 0) {
      return payload.message;
    }
  } catch {
    // noop
  }

  return `A API retornou erro ${response.status}.`;
}

async function apiRequest<T>(
  path: string,
  init: RequestInit = {},
  token?: string,
): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");

  if (init.body && !headers.has("Content-Type")) {
    headers.set("Content-Type", "application/json");
  }

  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  const response = await fetch(`${getApiBaseUrl()}${path}`, {
    ...init,
    headers,
    cache: "no-store",
  });

  if (!response.ok) {
    throw new FinanceBotApiError(
      response.status,
      await parseErrorMessage(response),
    );
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

export async function register(payload: {
  email: string;
  senha: string;
}) {
  return apiRequest<AuthResponse>("/auth/register", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export async function login(payload: { email: string; senha: string }) {
  return apiRequest<AuthResponse>("/auth/login", {
    method: "POST",
    body: JSON.stringify(payload),
  });
}

export async function getBillingStatus(token: string) {
  return apiRequest<BillingStatusResponse>("/api/billing/status", {}, token);
}

export async function requestUpgrade(token: string) {
  return apiRequest<UpgradeRequestResponse>(
    "/api/billing/solicitar-upgrade",
    { method: "POST" },
    token,
  );
}

export async function getResumo(token: string) {
  return apiRequest<ResumoResponse>("/api/resumo", {}, token);
}

export async function getMonthlyBudget(
  token: string,
  filters: { ano?: number; mes?: number } = {},
) {
  const params = new URLSearchParams();

  if (typeof filters.ano === "number") {
    params.set("ano", String(filters.ano));
  }

  if (typeof filters.mes === "number") {
    params.set("mes", String(filters.mes));
  }

  const query = params.toString();
  const path = query.length > 0 ? `/api/orcamentos/mensal?${query}` : "/api/orcamentos/mensal";
  return apiRequest<MonthlyBudgetResponse>(path, {}, token);
}

export async function getUltimosMovimentos(token: string) {
  return apiRequest<MovimentoResponse[]>("/api/movimentos/ultimos", {}, token);
}

export async function listMovimentos(
  token: string,
  filters: {
    inicio?: string;
    fim?: string;
    tipo?: string;
    busca?: string;
    categoria?: string;
    origem?: string;
    limite?: number;
  } = {},
) {
  const params = new URLSearchParams();

  if (filters.inicio) {
    params.set("inicio", filters.inicio);
  }

  if (filters.fim) {
    params.set("fim", filters.fim);
  }

  if (filters.tipo) {
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

  if (typeof filters.limite === "number") {
    params.set("limite", String(filters.limite));
  }

  const query = params.toString();
  const path = query.length > 0 ? `/api/movimentos?${query}` : "/api/movimentos";
  return apiRequest<MovimentoResponse[]>(path, {}, token);
}

export async function createGasto(
  token: string,
  payload: {
    descricao: string;
    valor: number;
    observacao?: string;
    ehFixo?: boolean;
    ehEssencial?: boolean;
  },
) {
  return apiRequest<GastoResponse>(
    "/api/gastos",
    {
      method: "POST",
      body: JSON.stringify(payload),
    },
    token,
  );
}

export async function createReceita(
  token: string,
  payload: { descricao: string; valor: number; ehFixo: boolean; observacao?: string },
) {
  return apiRequest<ReceitaResponse>(
    "/api/receitas",
    {
      method: "POST",
      body: JSON.stringify(payload),
    },
    token,
  );
}

export async function updateMonthlyBudget(
  token: string,
  payload: { ano?: number; mes?: number; limiteGastos: number },
) {
  return apiRequest<MonthlyBudgetResponse>(
    "/api/orcamentos/mensal",
    {
      method: "PUT",
      body: JSON.stringify(payload),
    },
    token,
  );
}

export async function updateGasto(
  token: string,
  gastoId: string,
  payload: {
    descricao: string;
    valor: number;
    data: string;
    categoria: string;
    observacao?: string;
    ehFixo?: boolean;
    ehEssencial?: boolean;
  },
) {
  return apiRequest<GastoResponse>(
    `/api/gastos/${gastoId}`,
    {
      method: "PUT",
      body: JSON.stringify(payload),
    },
    token,
  );
}

export async function updateReceita(
  token: string,
  receitaId: string,
  payload: { descricao: string; valor: number; data: string; ehFixo: boolean; observacao?: string },
) {
  return apiRequest<ReceitaResponse>(
    `/api/receitas/${receitaId}`,
    {
      method: "PUT",
      body: JSON.stringify(payload),
    },
    token,
  );
}

export async function deleteGasto(token: string, gastoId: string) {
  await apiRequest<unknown>(
    `/api/gastos/${gastoId}`,
    {
      method: "DELETE",
    },
    token,
  );
}

export async function deleteReceita(token: string, receitaId: string) {
  await apiRequest<unknown>(
    `/api/receitas/${receitaId}`,
    {
      method: "DELETE",
    },
    token,
  );
}

export async function generateTelegramLink(token: string) {
  return apiRequest<CodigoVinculoResponse>(
    "/auth/gerar-vinculo",
    { method: "POST" },
    token,
  );
}

export async function unlinkTelegram(token: string) {
  return apiRequest<DesvinculoTelegramResponse>(
    "/auth/desvincular",
    { method: "POST" },
    token,
  );
}

export async function getMonthlyReport(token: string) {
  return apiRequest<MonthlyReportResponse>("/api/relatorios/mensal", {}, token);
}
