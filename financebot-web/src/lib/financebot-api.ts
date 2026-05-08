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

export type MovimentoResponse = {
  tipo: string;
  descricao: string;
  valor: number;
  data: string;
  categoria?: string | null;
};

export type GastoResponse = {
  id: string;
  descricao: string;
  valor: number;
  data: string;
  categoria: string;
};

export type ReceitaResponse = {
  id: string;
  descricao: string;
  valor: number;
  data: string;
  ehFixo: boolean;
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

export async function getMovimentos(token: string) {
  return apiRequest<MovimentoResponse[]>("/api/movimentos/ultimos", {}, token);
}

export async function createGasto(
  token: string,
  payload: { descricao: string; valor: number },
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
  payload: { descricao: string; valor: number; ehFixo: boolean },
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
