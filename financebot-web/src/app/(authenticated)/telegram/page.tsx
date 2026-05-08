import { generateTelegramLinkAction } from "@/actions/telegram";
import { FlashMessage } from "@/components/app/flash-message";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { getQueryMessage } from "@/lib/action-state";
import { formatDateTime } from "@/lib/utils/format";

type TelegramPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

export default async function TelegramPage({
  searchParams,
}: TelegramPageProps) {
  const params = await searchParams;
  const error = getQueryMessage(params.error);
  const success = getQueryMessage(params.success);
  const code = getQueryMessage(params.code);
  const expires = getQueryMessage(params.expires);

  return (
    <div className="mx-auto grid w-full max-w-3xl gap-6">
      {error ? (
        <FlashMessage title="Nao foi possivel gerar o codigo" message={error} variant="destructive" />
      ) : null}
      {success ? <FlashMessage title="Tudo certo" message={success} /> : null}

      <Card>
        <CardHeader>
          <CardTitle>Vincular Telegram</CardTitle>
          <CardDescription>
            Gere um codigo de 6 digitos para conectar sua conta Web ao bot do Telegram.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          <form action={generateTelegramLinkAction}>
            <Button type="submit">Gerar codigo de vinculacao</Button>
          </form>

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
            O fluxo de desvinculacao ainda sera adicionado em uma iteracao seguinte.
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
