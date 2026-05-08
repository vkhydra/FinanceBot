"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import { buildRedirect } from "@/lib/action-state";
import { FinanceBotApiError, requestUpgrade } from "@/lib/financebot-api";
import { requireSession } from "@/lib/session";

const allowedRedirectTargets = new Set(["/dashboard", "/plano", "/telegram"]);

function resolveRedirectTarget(formData: FormData) {
  const requestedTarget = String(formData.get("redirectTo") ?? "/dashboard");
  return allowedRedirectTargets.has(requestedTarget) ? requestedTarget : "/dashboard";
}

export async function requestUpgradeAction(formData: FormData) {
  const session = await requireSession();
  const redirectTo = resolveRedirectTarget(formData);

  let result;
  try {
    result = await requestUpgrade(session.token);
  } catch (error) {
    const message =
      error instanceof FinanceBotApiError
        ? error.message
        : "Nao foi possivel registrar o pedido de upgrade.";
    redirect(buildRedirect(redirectTo, { error: message }));
  }

  revalidatePath("/dashboard");
  revalidatePath("/plano");
  revalidatePath("/telegram");
  redirect(
    buildRedirect(redirectTo, {
      success: result.mensagem,
    }),
  );
}
