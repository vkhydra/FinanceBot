import "server-only";

import { cookies, headers } from "next/headers";
import { redirect } from "next/navigation";

import type { AuthResponse } from "@/lib/financebot-api";

const SESSION_COOKIE = "financebot.session";

export type AppSession = {
  usuarioId: string;
  email: string;
  token: string;
  expiraEmUtc: string;
};

function encodeSession(session: AppSession) {
  return Buffer.from(JSON.stringify(session), "utf8").toString("base64url");
}

function decodeSession(value: string): AppSession | null {
  try {
    return JSON.parse(
      Buffer.from(value, "base64url").toString("utf8"),
    ) as AppSession;
  } catch {
    return null;
  }
}

function parseBoolean(value: string) {
  const normalized = value.trim().toLowerCase();
  return normalized === "1" || normalized === "true" || normalized === "yes";
}

function isLocalHost(host: string | null) {
  if (!host) {
    return false;
  }

  const hostname = host.split(":")[0];
  return hostname === "localhost" || hostname === "127.0.0.1";
}

async function shouldUseSecureCookie() {
  const configured = process.env.FINANCEBOT_SESSION_COOKIE_SECURE;
  if (configured) {
    return parseBoolean(configured);
  }

  const requestHeaders = await headers();
  const forwardedProto = requestHeaders.get("x-forwarded-proto");
  if (forwardedProto) {
    return forwardedProto.split(",")[0]?.trim() === "https";
  }

  const host = requestHeaders.get("host");
  return process.env.NODE_ENV === "production" && !isLocalHost(host);
}

export async function getSession(): Promise<AppSession | null> {
  const store = await cookies();
  const raw = store.get(SESSION_COOKIE)?.value;

  if (!raw) {
    return null;
  }

  const session = decodeSession(raw);
  if (!session) {
    return null;
  }

  if (new Date(session.expiraEmUtc).getTime() <= Date.now()) {
    await clearSession();
    return null;
  }

  return session;
}

export async function requireSession() {
  const session = await getSession();
  if (!session) {
    redirect("/login");
  }

  return session;
}

export async function setSession(auth: AuthResponse) {
  const store = await cookies();
  const session: AppSession = {
    usuarioId: auth.usuarioId,
    email: auth.email,
    token: auth.token,
    expiraEmUtc: auth.expiraEmUtc,
  };

  store.set(SESSION_COOKIE, encodeSession(session), {
    httpOnly: true,
    sameSite: "lax",
    secure: await shouldUseSecureCookie(),
    path: "/",
    expires: new Date(auth.expiraEmUtc),
  });
}

export async function clearSession() {
  const store = await cookies();
  store.delete(SESSION_COOKIE);
}
