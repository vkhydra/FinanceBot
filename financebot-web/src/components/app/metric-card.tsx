import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

type MetricCardProps = {
  title: string;
  value: string;
  description?: string;
};

export function MetricCard({ title, value, description }: MetricCardProps) {
  return (
    <Card className="app-panel min-w-0 gap-1.5">
      <CardHeader className="pb-0 pt-4">
        <CardDescription className="break-words text-[0.72rem] font-medium uppercase tracking-[0.24em]">
          {title}
        </CardDescription>
        <CardTitle className="break-words text-lg leading-tight font-semibold tracking-tight sm:text-xl">
          {value}
        </CardTitle>
      </CardHeader>
      {description ? (
        <CardContent className="break-words pb-4 pt-0 text-sm text-muted-foreground">
          {description}
        </CardContent>
      ) : null}
    </Card>
  );
}
