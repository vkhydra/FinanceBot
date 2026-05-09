"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";

import {
  FinanceBotApiError,
  generateTelegramLink,
  unlinkTelegram,
} from "@/lib/financebot-api";
import { buildRedirect } from "@/lib/action-state";
import { requireSession } from "@/lib/session";

function resolveTelegramRedirectTarget(formData: FormData) {
  const requestedTarget = String(formData.get("redirectTo") ?? "/telegram");
  return requestedTarget === "/telegram" || requestedTarget.startsWith("/telegram?")
    ? requestedTarget
    : "/telegram";
}

export async function generateTelegramLinkAction(formData: FormData) {
  const session = await requireSession();
  const redirectTo = resolveTelegramRedirectTarget(formData);

  let link;
  try {
    link = await generateTelegramLink(session.token);
  } catch (error) {
    const message =
      error instanceof FinanceBotApiError
        ? error.message
        : "Nao foi possivel gerar um codigo de vinculacao.";
    redirect(buildRedirect(redirectTo, { error: message }));
  }

  redirect(
    buildRedirect(redirectTo, {
      success: "Codigo de vinculacao gerado.",
      code: link.codigo,
      expires: link.expiraEmUtc,
    }),
  );
}

export async function unlinkTelegramAction(formData: FormData) {
  const session = await requireSession();
  const redirectTo = resolveTelegramRedirectTarget(formData);

  let result;
  try {
    result = await unlinkTelegram(session.token);
  } catch (error) {
    const message =
      error instanceof FinanceBotApiError
        ? error.message
        : "Nao foi possivel remover o vinculo com o Telegram.";
    redirect(buildRedirect(redirectTo, { error: message }));
  }

  revalidatePath("/telegram");
  redirect(
    buildRedirect(redirectTo, {
      success: result.mensagem,
    }),
  );
}
