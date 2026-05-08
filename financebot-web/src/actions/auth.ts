"use server";

import { redirect } from "next/navigation";

import {
  FinanceBotApiError,
  login,
  register,
} from "@/lib/financebot-api";
import { buildRedirect } from "@/lib/action-state";
import { clearSession, setSession } from "@/lib/session";

function parseCredentials(formData: FormData) {
  return {
    email: String(formData.get("email") ?? "").trim(),
    senha: String(formData.get("senha") ?? "").trim(),
  };
}

export async function loginAction(formData: FormData) {
  const payload = parseCredentials(formData);

  if (!payload.email || !payload.senha) {
    redirect(buildRedirect("/login", { error: "Informe e-mail e senha." }));
  }

  let auth;
  try {
    auth = await login(payload);
  } catch (error) {
    const message =
      error instanceof FinanceBotApiError
        ? error.message
        : "Nao foi possivel fazer login agora.";
    redirect(buildRedirect("/login", { error: message }));
  }

  await setSession(auth);
  redirect(buildRedirect("/dashboard", { success: "Login realizado com sucesso." }));
}

export async function registerAction(formData: FormData) {
  const payload = parseCredentials(formData);

  if (!payload.email || !payload.senha) {
    redirect(
      buildRedirect("/register", { error: "Informe e-mail e senha validos." }),
    );
  }

  let auth;
  try {
    auth = await register(payload);
  } catch (error) {
    const message =
      error instanceof FinanceBotApiError
        ? error.message
        : "Nao foi possivel criar a conta agora.";
    redirect(buildRedirect("/register", { error: message }));
  }

  await setSession(auth);
  redirect(
    buildRedirect("/dashboard", { success: "Conta criada com sucesso." }),
  );
}

export async function logoutAction() {
  await clearSession();
  redirect("/login");
}
