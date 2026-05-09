"use client";

import { LaunchDatePicker } from "@/components/app/launch-date-picker";
import { Label } from "@/components/ui/label";

type LaunchDateRangePickerProps = {
  initialStart: string;
  initialEnd: string;
  startName?: string;
  endName?: string;
};

export function LaunchDateRangePicker({
  initialStart,
  initialEnd,
  startName = "inicio",
  endName = "fim",
}: LaunchDateRangePickerProps) {
  return (
    <div className="min-w-0 space-y-2 md:col-span-2 xl:col-span-1">
      <Label>Periodo (inicio/fim)</Label>
      <div className="grid gap-3 sm:grid-cols-2">
        <div className="min-w-0">
          <LaunchDatePicker
            id={`${startName}-picker`}
            initialValue={initialStart}
            name={startName}
            ariaLabel="Data inicial do periodo"
          />
        </div>
        <div className="min-w-0">
          <LaunchDatePicker
            id={`${endName}-picker`}
            initialValue={initialEnd}
            name={endName}
            ariaLabel="Data final do periodo"
          />
        </div>
      </div>
    </div>
  );
}
