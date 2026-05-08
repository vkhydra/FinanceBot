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

export async function generateTelegramLinkAction() {
  const session = await requireSession();

  let link;
  try {
    link = await generateTelegramLink(session.token);
  } catch (error) {
    const message =
      error instanceof FinanceBotApiError
        ? error.message
        : "Nao foi possivel gerar um codigo de vinculacao.";
    redirect(buildRedirect("/telegram", { error: message }));
  }

  redirect(
    buildRedirect("/telegram", {
      success: "Codigo de vinculacao gerado.",
      code: link.codigo,
      expires: link.expiraEmUtc,
    }),
  );
}

export async function unlinkTelegramAction() {
  const session = await requireSession();

  let result;
  try {
    result = await unlinkTelegram(session.token);
  } catch (error) {
    const message =
      error instanceof FinanceBotApiError
        ? error.message
        : "Nao foi possivel remover o vinculo com o Telegram.";
    redirect(buildRedirect("/telegram", { error: message }));
  }

  revalidatePath("/telegram");
  redirect(
    buildRedirect("/telegram", {
      success: result.mensagem,
    }),
  );
}
