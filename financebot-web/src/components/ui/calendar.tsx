"use client";

import * as React from "react";

import { ChevronLeft, ChevronRight } from "lucide-react";
import { DayPicker, getDefaultClassNames } from "react-day-picker";

import { buttonVariants } from "@/components/ui/button";
import { cn } from "@/lib/utils";

export function Calendar({
  className,
  classNames,
  showOutsideDays = true,
  ...props
}: React.ComponentProps<typeof DayPicker>) {
  const defaultClassNames = getDefaultClassNames();

  return (
    <DayPicker
      showOutsideDays={showOutsideDays}
      className={cn("p-0", className)}
      classNames={{
        root: cn("w-fit", defaultClassNames.root),
        months: cn("relative flex w-fit flex-col gap-2.5", defaultClassNames.months),
        month: cn("flex w-fit flex-col gap-2.5", defaultClassNames.month),
        month_caption: cn(
          "relative flex h-[var(--cell-size)] items-center justify-center",
          defaultClassNames.month_caption,
        ),
        caption_label: cn("text-center text-[0.7rem] font-semibold sm:text-xs", defaultClassNames.caption_label),
        nav: cn(
          "absolute inset-x-0 top-0 flex h-[var(--cell-size)] items-center justify-between px-0.5",
          defaultClassNames.nav,
        ),
        button_previous: cn(
          buttonVariants({ variant: "outline", size: "icon-sm" }),
          "size-[1.625rem] rounded-lg border-border/70 bg-background/90 p-0 shadow-sm transition-colors hover:bg-accent/80 sm:size-7 sm:rounded-lg",
          defaultClassNames.button_previous,
        ),
        button_next: cn(
          buttonVariants({ variant: "outline", size: "icon-sm" }),
          "size-[1.625rem] rounded-lg border-border/70 bg-background/90 p-0 shadow-sm transition-colors hover:bg-accent/80 sm:size-7 sm:rounded-lg",
          defaultClassNames.button_next,
        ),
        month_grid: cn("mx-auto w-fit border-collapse", defaultClassNames.month_grid),
        weekdays: cn("flex justify-center gap-0.5", defaultClassNames.weekdays),
        weekday: cn(
          "flex h-[1.375rem] w-[var(--cell-size)] items-center justify-center text-center text-[0.65rem] font-medium text-muted-foreground sm:h-6 sm:text-[0.7rem]",
          defaultClassNames.weekday,
        ),
        weeks: cn("mt-1 flex flex-col gap-0.5 sm:mt-1.5 sm:gap-0.5", defaultClassNames.weeks),
        week: cn("flex justify-center gap-0.5", defaultClassNames.week),
        day: cn(
          "h-[var(--cell-size)] w-[var(--cell-size)] p-0 text-center",
          defaultClassNames.day,
        ),
        day_button: cn(
          buttonVariants({ variant: "ghost", size: "icon-sm" }),
          "h-[var(--cell-size)] w-[var(--cell-size)] rounded-md p-0 text-[0.76rem] font-medium text-foreground transition-colors hover:bg-primary/12 hover:text-primary focus-visible:bg-primary/12 focus-visible:text-primary sm:rounded-lg sm:text-[0.82rem]",
          defaultClassNames.day_button,
        ),
        range_start: cn(
          "rounded-md bg-primary text-primary-foreground hover:bg-primary hover:text-primary-foreground sm:rounded-lg",
          defaultClassNames.range_start,
        ),
        range_middle: cn(
          "rounded-md bg-primary/12 text-primary hover:bg-primary/20 hover:text-primary sm:rounded-lg",
          defaultClassNames.range_middle,
        ),
        range_end: cn(
          "rounded-md bg-primary text-primary-foreground hover:bg-primary hover:text-primary-foreground sm:rounded-lg",
          defaultClassNames.range_end,
        ),
        selected: cn(
          "rounded-md bg-primary text-primary-foreground hover:bg-primary hover:text-primary-foreground sm:rounded-lg",
          defaultClassNames.selected,
        ),
        today: cn("rounded-md bg-accent/80 text-accent-foreground sm:rounded-lg", defaultClassNames.today),
        outside: cn("text-muted-foreground opacity-45", defaultClassNames.outside),
        disabled: cn("text-muted-foreground opacity-45", defaultClassNames.disabled),
        hidden: cn("invisible", defaultClassNames.hidden),
        ...classNames,
      }}
      components={{
        Chevron: ({ orientation, className: iconClassName, ...iconProps }) =>
          orientation === "left" ? (
            <ChevronLeft className={cn("size-4", iconClassName)} {...iconProps} />
          ) : (
            <ChevronRight className={cn("size-4", iconClassName)} {...iconProps} />
          ),
      }}
      {...props}
    />
  );
}
