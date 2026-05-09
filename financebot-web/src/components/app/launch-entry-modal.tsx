"use client";

import { useState } from "react";

import { Plus, X } from "lucide-react";

import { createGastoAction, createReceitaAction } from "@/actions/finance";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { fieldOptionClassName, Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { cn } from "@/lib/utils";

type LaunchType = "gasto" | "receita";

type LaunchEntryModalProps = {
  redirectTo: string;
  triggerLabel?: string;
  triggerVariant?: "default" | "outline";
  initialType?: LaunchType;
  className?: string;
};

export function LaunchEntryModal({
  redirectTo,
  triggerLabel = "Novo lancamento",
  triggerVariant = "default",
  initialType = "gasto",
  className,
}: LaunchEntryModalProps) {
  const [open, setOpen] = useState(false);
  const [activeType, setActiveType] = useState<LaunchType>(initialType);

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger
        render={
          <Button type="button" variant={triggerVariant} className={className}>
            <Plus className="size-4" />
            {triggerLabel}
          </Button>
        }
      />

      <DialogContent className="app-panel">
        <div className="relative flex flex-col gap-3 pr-12 sm:flex-row sm:items-start sm:justify-between sm:pr-0">
          <DialogHeader className="pr-1">
            <p className="text-xs font-medium uppercase tracking-[0.24em] text-muted-foreground">
              Novo lancamento
            </p>
            <DialogTitle>Registrar pelo site</DialogTitle>
            <DialogDescription>
              Escolha entre gasto e receita sem sair da tela atual.
            </DialogDescription>
          </DialogHeader>
          <DialogClose
            render={
              <Button
                type="button"
                variant="outline"
                size="icon"
                className="absolute right-0 top-0 shrink-0 sm:static"
                aria-label="Fechar"
              >
                <X className="size-4" />
              </Button>
            }
          />
        </div>

        <div className="mt-4 grid gap-2 sm:mt-5 sm:grid-cols-2">
          <button
            type="button"
            onClick={() => setActiveType("gasto")}
            className={cn(
              "rounded-2xl border px-4 py-3 text-left transition-colors",
              activeType === "gasto"
                ? "border-primary/35 bg-primary text-primary-foreground"
                : "border-border/60 bg-background/60 hover:bg-muted/55",
            )}
          >
            <span className="block text-sm font-semibold">Gasto</span>
            <span className={cn("mt-1 block text-xs", activeType === "gasto" ? "text-primary-foreground/80" : "text-muted-foreground")}>
              Saida com observacao, fixo e essencial.
            </span>
          </button>
          <button
            type="button"
            onClick={() => setActiveType("receita")}
            className={cn(
              "rounded-2xl border px-4 py-3 text-left transition-colors",
              activeType === "receita"
                ? "border-primary/35 bg-primary text-primary-foreground"
                : "border-border/60 bg-background/60 hover:bg-muted/55",
            )}
          >
            <span className="block text-sm font-semibold">Receita</span>
            <span className={cn("mt-1 block text-xs", activeType === "receita" ? "text-primary-foreground/80" : "text-muted-foreground")}>
              Entrada simples com opcao de recorrencia.
            </span>
          </button>
        </div>

        <div className="mt-4 sm:mt-5">
          {activeType === "gasto" ? (
            <form action={createGastoAction} className="space-y-4">
              <input type="hidden" name="redirectTo" value={redirectTo} />
              <div className="space-y-2">
                <Label htmlFor="modal-gasto-descricao">Descricao</Label>
                <Input id="modal-gasto-descricao" name="descricao" placeholder="Ex.: Mercado" required />
              </div>
              <div className="space-y-2">
                <Label htmlFor="modal-gasto-valor">Valor</Label>
                <Input id="modal-gasto-valor" name="valor" placeholder="Ex.: 45,90" required />
              </div>
              <div className="space-y-2">
                <Label htmlFor="modal-gasto-observacao">Observacao</Label>
                <Textarea
                  id="modal-gasto-observacao"
                  name="observacao"
                  placeholder="Ex.: compra do fim de semana"
                />
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <label
                  htmlFor="modal-gasto-fixo"
                  className={fieldOptionClassName}
                >
                  <Checkbox id="modal-gasto-fixo" name="ehFixo" />
                  Gasto fixo
                </label>
                <label
                  htmlFor="modal-gasto-essencial"
                  className={fieldOptionClassName}
                >
                  <Checkbox id="modal-gasto-essencial" name="ehEssencial" />
                  Gasto essencial
                </label>
              </div>
              <Button type="submit" className="w-full">
                Salvar gasto
              </Button>
            </form>
          ) : (
            <form action={createReceitaAction} className="space-y-4">
              <input type="hidden" name="redirectTo" value={redirectTo} />
              <div className="space-y-2">
                <Label htmlFor="modal-receita-descricao">Descricao</Label>
                <Input id="modal-receita-descricao" name="descricao" placeholder="Ex.: Freelance" required />
              </div>
              <div className="space-y-2">
                <Label htmlFor="modal-receita-valor">Valor</Label>
                <Input id="modal-receita-valor" name="valor" placeholder="Ex.: 300,00" required />
              </div>
              <div className="space-y-2">
                <Label htmlFor="modal-receita-observacao">Observacao</Label>
                <Textarea
                  id="modal-receita-observacao"
                  name="observacao"
                  placeholder="Ex.: pagamento do cliente X"
                />
              </div>
              <label
                htmlFor="modal-receita-fixa"
                className={fieldOptionClassName}
              >
                <Checkbox id="modal-receita-fixa" name="ehFixo" />
                Receita recorrente
              </label>
              <Button type="submit" className="w-full">
                Salvar receita
              </Button>
            </form>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
