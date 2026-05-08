import { redirect } from "next/navigation";

import { registerAction } from "@/actions/auth";
import { AuthFormCard } from "@/components/app/auth-form-card";
import { FlashMessage } from "@/components/app/flash-message";
import { getQueryMessage } from "@/lib/action-state";
import { getSession } from "@/lib/session";

type RegisterPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default async function RegisterPage({
  searchParams,
}: RegisterPageProps) {
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
          title="Nao foi possivel criar a conta"
          message={error}
          variant="destructive"
        />
      ) : null}
      <AuthFormCard
        title="Criar conta"
        description="Cadastre-se para usar o FinanceBot Web e vincular seu Telegram."
        actionLabel="Criar conta"
        action={registerAction}
        footerLabel="Ja tem conta?"
        footerHref="/login"
        footerAction="Entrar"
      />
    </div>
  );
}
