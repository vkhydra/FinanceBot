"use client";

import { useState } from "react";

import { addMonths, format, parseISO } from "date-fns";
import { CalendarIcon, ChevronLeft, ChevronRight } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Calendar } from "@/components/ui/calendar";
import { fieldPickerTriggerClassName } from "@/components/ui/input";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";

type LaunchDatePickerProps = {
  initialValue: string;
  name: string;
  id?: string;
  ariaLabel?: string;
};

export function LaunchDatePicker({ initialValue, name, id, ariaLabel }: LaunchDatePickerProps) {
  const initialDate = parseDate(initialValue) ?? new Date();
  const [open, setOpen] = useState(false);
  const [date, setDate] = useState(initialDate);
  const [draftDate, setDraftDate] = useState<Date | undefined>(initialDate);
  const [month, setMonth] = useState(initialDate);

  return (
    <>
      <input type="hidden" name={name} value={format(date, "yyyy-MM-dd")} />
      <Popover
        open={open}
        onOpenChange={(nextOpen) => {
          setOpen(nextOpen);

          if (nextOpen) {
            setDraftDate(date);
            setMonth(date);
          }
        }}
      >
        <PopoverTrigger
          render={
            <Button
              id={id}
              type="button"
              variant="outline"
              size="lg"
              className={fieldPickerTriggerClassName}
              aria-label={ariaLabel}
            >
              <CalendarIcon data-icon="inline-start" />
              {format(date, "dd/MM/yyyy")}
            </Button>
          }
        />
        <PopoverContent className="w-auto p-0" align="start">
          <div className="flex items-center justify-between border-b border-border/70 px-2 py-1.5 sm:px-2.5 sm:py-2">
            <Button
              type="button"
              variant="outline"
              size="icon"
              className="size-7 rounded-lg"
              onClick={() => setMonth((currentMonth) => addMonths(currentMonth, -1))}
              aria-label="Mes anterior"
            >
              <ChevronLeft className="size-4" />
            </Button>
            <p className="text-[0.7rem] font-semibold capitalize sm:text-xs">
              {month.toLocaleDateString("pt-BR", { month: "long", year: "numeric" })}
            </p>
            <Button
              type="button"
              variant="outline"
              size="icon"
              className="size-7 rounded-lg"
              onClick={() => setMonth((currentMonth) => addMonths(currentMonth, 1))}
              aria-label="Proximo mes"
            >
              <ChevronRight className="size-4" />
            </Button>
          </div>
          <Calendar
            mode="single"
            month={month}
            onMonthChange={setMonth}
            hideNavigation
            selected={draftDate}
            onSelect={(nextDate) => {
              setDraftDate(nextDate);

              if (nextDate) {
                setMonth(nextDate);
              }
            }}
            classNames={{ month_caption: "hidden" }}
            className="border-0 p-1.5 [--cell-size:1.875rem] sm:p-2 sm:[--cell-size:2.125rem]"
          />
          <div className="space-y-2 border-t border-border/70 px-2 py-2 sm:space-y-2.5 sm:px-2.5 sm:py-2.5">
            <p className="text-[0.7rem] text-muted-foreground sm:text-xs">Escolha a data e toque em Confirmar.</p>
            <div className="grid grid-cols-2 gap-2">
              <Button
                type="button"
                variant="outline"
                className="rounded-xl sm:rounded-2xl"
                onClick={() => {
                  setDraftDate(date);
                  setOpen(false);
                }}
              >
                Fechar
              </Button>
              <Button
                type="button"
                className="rounded-xl sm:rounded-2xl"
                onClick={() => {
                  if (draftDate) {
                    setDate(draftDate);
                  }

                  setOpen(false);
                }}
              >
                Confirmar
              </Button>
            </div>
          </div>
        </PopoverContent>
      </Popover>
    </>
  );
}

function parseDate(value: string) {
  if (!value) {
    return undefined;
  }

  const parsed = parseISO(`${value}T12:00:00`);
  return Number.isNaN(parsed.getTime()) ? undefined : parsed;
}
