import Link from "next/link";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

type AuthFormCardProps = {
  title: string;
  description: string;
  actionLabel: string;
  action: (formData: FormData) => void | Promise<void>;
  footerLabel: string;
  footerHref: string;
  footerAction: string;
};

export function AuthFormCard({
  title,
  description,
  actionLabel,
  action,
  footerLabel,
  footerHref,
  footerAction,
}: AuthFormCardProps) {
  return (
    <Card className="w-full shadow-sm">
      <CardHeader className="space-y-2">
        <CardTitle className="text-2xl">{title}</CardTitle>
        <CardDescription>{description}</CardDescription>
      </CardHeader>
      <CardContent>
        <form action={action} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="email">E-mail</Label>
            <Input id="email" name="email" type="email" placeholder="voce@exemplo.com" required />
          </div>
          <div className="space-y-2">
            <Label htmlFor="senha">Senha</Label>
            <Input id="senha" name="senha" type="password" minLength={6} required />
          </div>
          <Button type="submit" className="w-full">
            {actionLabel}
          </Button>
        </form>
        <p className="mt-4 text-sm text-muted-foreground">
          {footerLabel}{" "}
          <Link href={footerHref} className="font-medium text-foreground underline underline-offset-4">
            {footerAction}
          </Link>
        </p>
      </CardContent>
    </Card>
  );
}
