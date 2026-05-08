"use server";

import { redirect } from "next/navigation";

import {
  FinanceBotApiError,
  generateTelegramLink,
} from "@/lib/financebot-api";
import { buildRedirect } from "@/lib/action-state";
import { requireSession } from "@/lib/session";

export async function generateTelegramLinkAction() {
  const session = await requireSession();

  try {
    const link = await generateTelegramLink(session.token);
    redirect(
      buildRedirect("/telegram", {
        success: "Codigo de vinculacao gerado.",
        code: link.codigo,
        expires: link.expiraEmUtc,
      }),
    );
  } catch (error) {
    const message =
      error instanceof FinanceBotApiError
        ? error.message
        : "Nao foi possivel gerar um codigo de vinculacao.";
    redirect(buildRedirect("/telegram", { error: message }));
  }
}
