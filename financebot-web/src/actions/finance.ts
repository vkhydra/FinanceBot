"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  createGasto,
  createReceita,
  FinanceBotApiError,
} from "@/lib/financebot-api";
import { buildRedirect } from "@/lib/action-state";
import { requireSession } from "@/lib/session";

function parseMoney(value: FormDataEntryValue | null) {
  return Number.parseFloat(String(value ?? "").replace(",", "."));
}

function resolveLaunchCreationRedirectTarget(formData: FormData) {
  const requestedTarget = String(formData.get("redirectTo") ?? "/dashboard");
  return requestedTarget === "/dashboard" ||
    requestedTarget.startsWith("/dashboard?") ||
    requestedTarget === "/lancamentos" ||
    requestedTarget.startsWith("/lancamentos?")
    ? requestedTarget
    : "/dashboard";
}

export async function createGastoAction(formData: FormData) {
  const session = await requireSession();
  const redirectTo = resolveLaunchCreationRedirectTarget(formData);
  const descricao = String(formData.get("descricao") ?? "").trim();
  const valor = parseMoney(formData.get("valor"));
  const observacao = String(formData.get("observacao") ?? "").trim();
  const ehFixo = formData.get("ehFixo") === "on";
  const ehEssencial = formData.get("ehEssencial") === "on";

  if (!descricao || Number.isNaN(valor) || valor <= 0) {
    redirect(
      buildRedirect(redirectTo, {
        error: "Informe uma descricao e um valor valido para o gasto.",
      }),
    );
  }

  try {
    await createGasto(session.token, {
      descricao,
      valor,
      observacao: observacao || undefined,
      ehFixo,
      ehEssencial,
    });
  } catch (error) {
    const message =
      error instanceof FinanceBotApiError
        ? error.message
        : "Nao foi possivel registrar o gasto.";
    redirect(buildRedirect(redirectTo, { error: message }));
  }

  revalidatePath("/dashboard");
  revalidatePath("/lancamentos");
  redirect(
    buildRedirect(redirectTo, {
      success: "Gasto registrado com sucesso.",
    }),
  );
}

export async function createReceitaAction(formData: FormData) {
  const session = await requireSession();
  const redirectTo = resolveLaunchCreationRedirectTarget(formData);
  const descricao = String(formData.get("descricao") ?? "").trim();
  const valor = parseMoney(formData.get("valor"));
  const ehFixo = formData.get("ehFixo") === "on";
  const observacao = String(formData.get("observacao") ?? "").trim();

  if (!descricao || Number.isNaN(valor) || valor <= 0) {
    redirect(
      buildRedirect(redirectTo, {
        error: "Informe uma descricao e um valor valido para a receita.",
      }),
    );
  }

  try {
    await createReceita(session.token, { descricao, valor, ehFixo, observacao: observacao || undefined });
  } catch (error) {
    const message =
      error instanceof FinanceBotApiError
        ? error.message
        : "Nao foi possivel registrar a receita.";
    redirect(buildRedirect(redirectTo, { error: message }));
  }

  revalidatePath("/dashboard");
  revalidatePath("/lancamentos");
  redirect(
    buildRedirect(redirectTo, {
      success: "Receita registrada com sucesso.",
    }),
  );
}
