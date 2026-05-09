"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { buildRedirect } from "@/lib/action-state";
import { FinanceBotApiError, updateMonthlyBudget } from "@/lib/financebot-api";
import { requireSession } from "@/lib/session";

function parseMoney(value: FormDataEntryValue | null) {
  return Number.parseFloat(String(value ?? "").replace(",", "."));
}

function resolveDashboardRedirectTarget(formData: FormData) {
  const requestedTarget = String(formData.get("redirectTo") ?? "/dashboard");
  return requestedTarget === "/dashboard" || requestedTarget.startsWith("/dashboard?")
    ? requestedTarget
    : "/dashboard";
}

export async function updateMonthlyBudgetAction(formData: FormData) {
  const session = await requireSession();
  const redirectTo = resolveDashboardRedirectTarget(formData);
  const limiteGastos = parseMoney(formData.get("limiteGastos"));
  const anoValue = String(formData.get("ano") ?? "").trim();
  const mesValue = String(formData.get("mes") ?? "").trim();
  const ano = anoValue ? Number.parseInt(anoValue, 10) : undefined;
  const mes = mesValue ? Number.parseInt(mesValue, 10) : undefined;

  if (Number.isNaN(limiteGastos) || limiteGastos <= 0) {
    redirect(
      buildRedirect(redirectTo, {
        error: "Informe um limite mensal valido para o orcamento.",
      }),
    );
  }

  try {
    await updateMonthlyBudget(session.token, {
      ano,
      mes,
      limiteGastos,
    });
  } catch (error) {
    const message =
      error instanceof FinanceBotApiError
        ? error.message
        : "Nao foi possivel atualizar o orcamento mensal.";
    redirect(buildRedirect(redirectTo, { error: message }));
  }

  revalidatePath("/dashboard");
  redirect(
    buildRedirect(redirectTo, {
      success: "Orcamento mensal atualizado com sucesso.",
    }),
  );
}
