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

export async function createGastoAction(formData: FormData) {
  const session = await requireSession();
  const descricao = String(formData.get("descricao") ?? "").trim();
  const valor = parseMoney(formData.get("valor"));
  const observacao = String(formData.get("observacao") ?? "").trim();

  if (!descricao || Number.isNaN(valor) || valor <= 0) {
    redirect(
      buildRedirect("/dashboard", {
        error: "Informe uma descricao e um valor valido para o gasto.",
      }),
    );
  }

  try {
    await createGasto(session.token, { descricao, valor, observacao: observacao || undefined });
  } catch (error) {
    const message =
      error instanceof FinanceBotApiError
        ? error.message
        : "Nao foi possivel registrar o gasto.";
    redirect(buildRedirect("/dashboard", { error: message }));
  }

  revalidatePath("/dashboard");
  redirect(
    buildRedirect("/dashboard", {
      success: "Gasto registrado com sucesso.",
    }),
  );
}

export async function createReceitaAction(formData: FormData) {
  const session = await requireSession();
  const descricao = String(formData.get("descricao") ?? "").trim();
  const valor = parseMoney(formData.get("valor"));
  const ehFixo = formData.get("ehFixo") === "on";
  const observacao = String(formData.get("observacao") ?? "").trim();

  if (!descricao || Number.isNaN(valor) || valor <= 0) {
    redirect(
      buildRedirect("/dashboard", {
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
    redirect(buildRedirect("/dashboard", { error: message }));
  }

  revalidatePath("/dashboard");
  redirect(
    buildRedirect("/dashboard", {
      success: "Receita registrada com sucesso.",
    }),
  );
}
