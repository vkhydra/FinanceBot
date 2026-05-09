"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { buildRedirect } from "@/lib/action-state";
import {
  deleteGasto,
  deleteReceita,
  FinanceBotApiError,
  updateGasto,
  updateReceita,
} from "@/lib/financebot-api";
import { requireSession } from "@/lib/session";

function parseMoney(value: FormDataEntryValue | null) {
  return Number.parseFloat(String(value ?? "").replace(",", "."));
}

function resolveReturnTo(formData: FormData) {
  const requestedTarget = String(formData.get("returnTo") ?? "/lancamentos");
  return requestedTarget.startsWith("/lancamentos") ? requestedTarget : "/lancamentos";
}

function revalidateLaunchSurfaces() {
  revalidatePath("/dashboard");
  revalidatePath("/lancamentos");
  revalidatePath("/plano");
}

export async function updateGastoAction(formData: FormData) {
  const session = await requireSession();
  const returnTo = resolveReturnTo(formData);
  const gastoId = String(formData.get("gastoId") ?? "");
  const descricao = String(formData.get("descricao") ?? "").trim();
  const valor = parseMoney(formData.get("valor"));
  const data = String(formData.get("data") ?? "");
  const categoria = String(formData.get("categoria") ?? "").trim();
  const observacao = String(formData.get("observacao") ?? "").trim();
  const ehFixo = formData.get("ehFixo") === "on";
  const ehEssencial = formData.get("ehEssencial") === "on";

  if (!gastoId || !descricao || !categoria || Number.isNaN(valor) || valor <= 0 || !data) {
    redirect(
      buildRedirect(returnTo, {
        error: "Preencha descricao, valor, data e categoria para atualizar o gasto.",
      }),
    );
  }

  try {
    await updateGasto(session.token, gastoId, {
      descricao,
      valor,
      data,
      categoria,
      observacao: observacao || undefined,
      ehFixo,
      ehEssencial,
    });
  } catch (error) {
    const message =
      error instanceof FinanceBotApiError
        ? error.message
        : "Nao foi possivel atualizar o gasto.";
    redirect(buildRedirect(returnTo, { error: message }));
  }

  revalidateLaunchSurfaces();
  redirect(
    buildRedirect(returnTo, {
      success: "Gasto atualizado com sucesso.",
    }),
  );
}

export async function updateReceitaAction(formData: FormData) {
  const session = await requireSession();
  const returnTo = resolveReturnTo(formData);
  const receitaId = String(formData.get("receitaId") ?? "");
  const descricao = String(formData.get("descricao") ?? "").trim();
  const valor = parseMoney(formData.get("valor"));
  const data = String(formData.get("data") ?? "");
  const ehFixo = formData.get("ehFixo") === "on";
  const observacao = String(formData.get("observacao") ?? "").trim();

  if (!receitaId || !descricao || Number.isNaN(valor) || valor <= 0 || !data) {
    redirect(
      buildRedirect(returnTo, {
        error: "Preencha descricao, valor e data para atualizar a receita.",
      }),
    );
  }

  try {
    await updateReceita(session.token, receitaId, {
      descricao,
      valor,
      data,
      ehFixo,
      observacao: observacao || undefined,
    });
  } catch (error) {
    const message =
      error instanceof FinanceBotApiError
        ? error.message
        : "Nao foi possivel atualizar a receita.";
    redirect(buildRedirect(returnTo, { error: message }));
  }

  revalidateLaunchSurfaces();
  redirect(
    buildRedirect(returnTo, {
      success: "Receita atualizada com sucesso.",
    }),
  );
}

export async function deleteGastoAction(formData: FormData) {
  const session = await requireSession();
  const returnTo = resolveReturnTo(formData);
  const gastoId = String(formData.get("gastoId") ?? "");

  if (!gastoId) {
    redirect(buildRedirect(returnTo, { error: "Gasto invalido para exclusao." }));
  }

  try {
    await deleteGasto(session.token, gastoId);
  } catch (error) {
    const message =
      error instanceof FinanceBotApiError
        ? error.message
        : "Nao foi possivel excluir o gasto.";
    redirect(buildRedirect(returnTo, { error: message }));
  }

  revalidateLaunchSurfaces();
  redirect(
    buildRedirect(returnTo, {
      success: "Gasto excluido com sucesso.",
    }),
  );
}

export async function deleteReceitaAction(formData: FormData) {
  const session = await requireSession();
  const returnTo = resolveReturnTo(formData);
  const receitaId = String(formData.get("receitaId") ?? "");

  if (!receitaId) {
    redirect(buildRedirect(returnTo, { error: "Receita invalida para exclusao." }));
  }

  try {
    await deleteReceita(session.token, receitaId);
  } catch (error) {
    const message =
      error instanceof FinanceBotApiError
        ? error.message
        : "Nao foi possivel excluir a receita.";
    redirect(buildRedirect(returnTo, { error: message }));
  }

  revalidateLaunchSurfaces();
  redirect(
    buildRedirect(returnTo, {
      success: "Receita excluida com sucesso.",
    }),
  );
}
