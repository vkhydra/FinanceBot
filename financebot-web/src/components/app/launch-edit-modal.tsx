"use client";

import { useState, type ReactNode } from "react";

import { X } from "lucide-react";

import { updateGastoAction, updateReceitaAction } from "@/actions/launches";
import { LaunchDatePicker } from "@/components/app/launch-date-picker";
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
import type { MovimentoResponse } from "@/lib/financebot-api";

type LaunchEditModalProps = {
  movimento: MovimentoResponse;
  returnTo: string;
  className?: string;
};

export function LaunchEditModal({ movimento, returnTo, className }: LaunchEditModalProps) {
  const [open, setOpen] = useState(false);

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger
        render={
          <Button type="button" variant="outline" className={className}>
            Editar
          </Button>
        }
      />

      <DialogContent className="app-panel">
        <div className="relative flex flex-col gap-3 pr-12 sm:flex-row sm:items-start sm:justify-between sm:pr-0">
          <DialogHeader className="pr-1">
            <p className="text-xs font-medium uppercase tracking-[0.24em] text-muted-foreground">
              Editar lancamento
            </p>
            <DialogTitle>{movimento.tipo === "Gasto" ? "Ajustar gasto" : "Ajustar receita"}</DialogTitle>
            <DialogDescription>
              Revise os dados sem sair do extrato.
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

        <div className="mt-4 sm:mt-5">
          {movimento.tipo === "Gasto" ? (
            <form action={updateGastoAction} className="space-y-4">
              <input type="hidden" name="gastoId" value={movimento.id} />
              <input type="hidden" name="returnTo" value={returnTo} />
              <div className="grid gap-3 md:grid-cols-2">
                <Field label="Descricao" htmlFor={`descricao-${movimento.id}`}>
                  <Input id={`descricao-${movimento.id}`} name="descricao" defaultValue={movimento.descricao} required />
                </Field>
                <Field label="Valor" htmlFor={`valor-${movimento.id}`}>
                  <Input
                    id={`valor-${movimento.id}`}
                    name="valor"
                    type="number"
                    step="0.01"
                    min="0.01"
                    defaultValue={movimento.valor.toFixed(2)}
                    required
                  />
                </Field>
                <Field label="Data" htmlFor={`data-${movimento.id}`}>
                  <LaunchDatePicker id={`data-${movimento.id}`} initialValue={movimento.data.slice(0, 10)} name="data" />
                </Field>
                <Field label="Categoria" htmlFor={`categoria-${movimento.id}`}>
                  <Input
                    id={`categoria-${movimento.id}`}
                    name="categoria"
                    defaultValue={movimento.categoria ?? "Outros"}
                    required
                  />
                </Field>
                <label
                  htmlFor={`fixo-${movimento.id}`}
                  className={fieldOptionClassName}
                >
                  <Checkbox id={`fixo-${movimento.id}`} name="ehFixo" defaultChecked={movimento.ehFixo ?? false} />
                  Gasto fixo
                </label>
                <label
                  htmlFor={`essencial-${movimento.id}`}
                  className={fieldOptionClassName}
                >
                  <Checkbox
                    id={`essencial-${movimento.id}`}
                    name="ehEssencial"
                    defaultChecked={movimento.ehEssencial ?? false}
                  />
                  Gasto essencial
                </label>
                <Field label="Observacao" htmlFor={`observacao-${movimento.id}`} className="md:col-span-2">
                  <Textarea
                    id={`observacao-${movimento.id}`}
                    name="observacao"
                    defaultValue={movimento.observacao ?? ""}
                    className="min-h-28"
                  />
                </Field>
              </div>
              <Button type="submit" className="w-full">
                Salvar gasto
              </Button>
            </form>
          ) : (
            <form action={updateReceitaAction} className="space-y-4">
              <input type="hidden" name="receitaId" value={movimento.id} />
              <input type="hidden" name="returnTo" value={returnTo} />
              <div className="grid gap-3 md:grid-cols-2">
                <Field label="Descricao" htmlFor={`descricao-${movimento.id}`}>
                  <Input id={`descricao-${movimento.id}`} name="descricao" defaultValue={movimento.descricao} required />
                </Field>
                <Field label="Valor" htmlFor={`valor-${movimento.id}`}>
                  <Input
                    id={`valor-${movimento.id}`}
                    name="valor"
                    type="number"
                    step="0.01"
                    min="0.01"
                    defaultValue={movimento.valor.toFixed(2)}
                    required
                  />
                </Field>
                <Field label="Data" htmlFor={`data-${movimento.id}`}>
                  <LaunchDatePicker id={`data-${movimento.id}`} initialValue={movimento.data.slice(0, 10)} name="data" />
                </Field>
                <label
                  htmlFor={`fixo-${movimento.id}`}
                  className={`${fieldOptionClassName} md:col-span-2`}
                >
                  <Checkbox id={`fixo-${movimento.id}`} name="ehFixo" defaultChecked={movimento.ehFixo ?? false} />
                  Receita recorrente
                </label>
                <Field label="Observacao" htmlFor={`observacao-${movimento.id}`} className="md:col-span-2">
                  <Textarea
                    id={`observacao-${movimento.id}`}
                    name="observacao"
                    defaultValue={movimento.observacao ?? ""}
                    className="min-h-28"
                  />
                </Field>
              </div>
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

function Field({
  label,
  htmlFor,
  children,
  className = "",
}: {
  label: string;
  htmlFor: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <div className={`space-y-2 ${className}`}>
      <Label htmlFor={htmlFor}>{label}</Label>
      {children}
    </div>
  );
}
