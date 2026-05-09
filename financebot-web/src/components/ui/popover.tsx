import * as React from "react";

import { Popover as PopoverPrimitive } from "@base-ui/react/popover";

import { cn } from "@/lib/utils";

const Popover = PopoverPrimitive.Root;
const PopoverTrigger = PopoverPrimitive.Trigger;
const PopoverClose = PopoverPrimitive.Close;

const PopoverContent = React.forwardRef<
  HTMLDivElement,
  React.ComponentPropsWithoutRef<typeof PopoverPrimitive.Popup> & {
    sideOffset?: number;
    align?: React.ComponentPropsWithoutRef<typeof PopoverPrimitive.Positioner>["align"];
  }
>(({ className, sideOffset = 8, align = "start", ...props }, ref) => (
  <PopoverPrimitive.Portal>
    <PopoverPrimitive.Positioner sideOffset={sideOffset} align={align} className="z-50">
      <PopoverPrimitive.Popup
        ref={ref}
        className={cn(
          "w-72 rounded-2xl border border-border/70 bg-popover p-4 shadow-xl shadow-black/10 outline-none",
          className,
        )}
        {...props}
      />
    </PopoverPrimitive.Positioner>
  </PopoverPrimitive.Portal>
));
PopoverContent.displayName = "PopoverContent";

export { Popover, PopoverTrigger, PopoverContent, PopoverClose };
