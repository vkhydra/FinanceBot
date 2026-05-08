import { redirect } from "next/navigation";

import { loginAction } from "@/actions/auth";
import { AuthFormCard } from "@/components/app/auth-form-card";
import { FlashMessage } from "@/components/app/flash-message";
import { getQueryMessage } from "@/lib/action-state";
import { getSession } from "@/lib/session";

type LoginPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default async function LoginPage({ searchParams }: LoginPageProps) {
  const session = await getSession();
  if (session) {
    redirect("/dashboard");
  }

  const params = await searchParams;
  const error = getQueryMessage(params.error);

  return (
    <div className="space-y-4">
      {error ? (
        <FlashMessage
          title="Nao foi possivel entrar"
          message={error}
          variant="destructive"
        />
      ) : null}
      <AuthFormCard
        title="Entrar"
        description="Acesse sua area autenticada do FinanceBot."
        actionLabel="Entrar"
        action={loginAction}
        footerLabel="Ainda nao tem conta?"
        footerHref="/register"
        footerAction="Criar conta"
      />
    </div>
  );
}
