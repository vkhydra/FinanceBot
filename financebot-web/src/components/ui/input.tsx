import * as React from "react"
import { Input as InputPrimitive } from "@base-ui/react/input"

import { cn } from "@/lib/utils"

const fieldControlClassName =
  "w-full min-w-0 rounded-2xl border border-border/70 bg-background/75 text-sm shadow-sm outline-none transition-[color,box-shadow,border-color] placeholder:text-muted-foreground focus-visible:border-primary/40 focus-visible:ring-4 focus-visible:ring-primary/10 disabled:pointer-events-none disabled:cursor-not-allowed disabled:bg-input/50 disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-3 aria-invalid:ring-destructive/20 dark:bg-input/25 dark:aria-invalid:border-destructive/50 dark:aria-invalid:ring-destructive/40"

const fieldInputClassName = `${fieldControlClassName} h-11 px-3.5 py-2`
const fieldSelectClassName = `${fieldInputClassName} appearance-none`
const fieldPickerTriggerClassName = `${fieldInputClassName} justify-start text-left font-normal`
const fieldOptionClassName =
  `${fieldControlClassName} flex items-center gap-3 px-4 py-3 text-sm text-muted-foreground`

function Input({ className, type, ...props }: React.ComponentProps<"input">) {
  return (
    <InputPrimitive
      type={type}
      data-slot="input"
      className={cn(
        fieldInputClassName,
        "file:inline-flex file:h-6 file:border-0 file:bg-transparent file:text-sm file:font-medium file:text-foreground",
        className
      )}
      {...props}
    />
  )
}

export { Input, fieldControlClassName, fieldInputClassName, fieldOptionClassName, fieldPickerTriggerClassName, fieldSelectClassName }
