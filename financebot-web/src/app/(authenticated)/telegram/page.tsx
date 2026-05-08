import Link from "next/link";

import { requestUpgradeAction } from "@/actions/billing";
import { generateTelegramLinkAction, unlinkTelegramAction } from "@/actions/telegram";
import { FlashMessage } from "@/components/app/flash-message";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { getQueryMessage } from "@/lib/action-state";
import { getBillingStatus } from "@/lib/financebot-api";
import { requireSession } from "@/lib/session";
import { formatDateTime } from "@/lib/utils/format";

type TelegramPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default async function TelegramPage({
  searchParams,
}: TelegramPageProps) {
  const session = await requireSession();
  const params = await searchParams;
  const error = getQueryMessage(params.error);
  const success = getQueryMessage(params.success);
  const code = getQueryMessage(params.code);
  const expires = getQueryMessage(params.expires);
  const billing = await getBillingStatus(session.token);
  const showUpgradeCard =
    billing.podeSolicitarUpgrade || billing.upgradePendente || billing.trialAtivo;

  return (
    <div className="mx-auto grid w-full max-w-3xl gap-6">
      {error ? (
        <FlashMessage title="Nao foi possivel concluir a acao" message={error} variant="destructive" />
      ) : null}
      {success ? <FlashMessage title="Tudo certo" message={success} /> : null}

      <Card>
        <CardHeader>
          <CardTitle>Vincular Telegram</CardTitle>
          <CardDescription>
            Gere um codigo de 6 digitos para conectar sua conta Web ao bot do Telegram ou
            regenere um novo codigo quando precisar.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          <div className="flex flex-col gap-3 sm:flex-row">
            <form action={generateTelegramLinkAction}>
              <Button type="submit">Gerar codigo de vinculacao</Button>
            </form>
            <form action={unlinkTelegramAction}>
              <Button type="submit" variant="outline">
                Desvincular via Web
              </Button>
            </form>
          </div>

          {code ? (
            <>
              <Separator />
              <div className="rounded-xl border bg-muted/40 p-6">
                <p className="text-sm text-muted-foreground">Codigo atual</p>
                <p className="mt-2 text-4xl font-semibold tracking-[0.4em]">
                  {code}
                </p>
                {expires ? (
                  <p className="mt-3 text-sm text-muted-foreground">
                    Expira em {formatDateTime(expires)}.
                  </p>
                ) : null}
              </div>
            </>
          ) : null}

          <div className="space-y-2 text-sm text-muted-foreground">
            <p>Passos:</p>
            <ol className="list-decimal space-y-1 pl-5">
              <li>Abra o bot do FinanceBot no Telegram.</li>
              <li>Envie o comando <strong className="text-foreground">/vincular 123456</strong>.</li>
              <li>Substitua <strong className="text-foreground">123456</strong> pelo codigo gerado acima.</li>
            </ol>
          </div>

          <div className="rounded-lg border border-dashed p-4 text-sm text-muted-foreground">
            Se preferir, voce tambem pode remover o chat depois com{" "}
            <strong className="text-foreground">/desvincular</strong> no Telegram. A acao na Web
            usa o mesmo endpoint autenticado <strong className="text-foreground">POST /auth/desvincular</strong>{" "}
            e responde sem erro mesmo quando nao existe vinculo ativo.
          </div>
        </CardContent>
      </Card>

      {showUpgradeCard ? (
        <Card>
          <CardHeader className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
            <div className="space-y-1">
              <CardTitle>
                {billing.upgradePendente
                  ? "Upgrade ja solicitado"
                  : billing.trialAtivo
                    ? "Continue no Premium depois do trial"
                    : "Solicite o upgrade sem sair do ecossistema"}
              </CardTitle>
              <CardDescription>{billing.mensagemStatus}</CardDescription>
            </div>
            <Badge variant={billing.upgradePendente ? "secondary" : "default"}>
              {billing.upgradePendente ? "Pendente" : billing.trialAtivo ? "Trial ativo" : "Upgrade"}
            </Badge>
          </CardHeader>
          <CardContent className="space-y-4 text-sm text-muted-foreground">
            <p>
              O pedido de upgrade funciona tanto aqui na Web quanto direto no bot com{" "}
              <strong className="text-foreground">/upgrade</strong> ou{" "}
              <strong className="text-foreground">/assinar</strong>.
            </p>
            <p>
              O Premium mantém o relatorio mensal liberado e remove o limite mensal de lancamentos
              quando essa etapa comercial estiver concluida.
            </p>
            <div className="flex flex-col gap-3 sm:flex-row">
              {billing.podeSolicitarUpgrade ? (
                <form action={requestUpgradeAction}>
                  <input type="hidden" name="redirectTo" value="/telegram" />
                  <Button type="submit">Solicitar upgrade por aqui</Button>
                </form>
              ) : null}
              <Link href="/plano" className={buttonVariants({ variant: "outline" })}>
                Ver pagina de plano
              </Link>
            </div>
          </CardContent>
        </Card>
      ) : null}
    </div>
  );
}
