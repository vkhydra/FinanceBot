import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

type MetricCardProps = {
  title: string;
  value: string;
  description?: string;
};

export function MetricCard({ title, value, description }: MetricCardProps) {
  return (
    <Card className="app-panel gap-2">
      <CardHeader className="pb-0 pt-5">
        <CardDescription className="text-[0.72rem] font-medium uppercase tracking-[0.24em]">
          {title}
        </CardDescription>
        <CardTitle className="text-xl font-semibold tracking-tight sm:text-2xl">
          {value}
        </CardTitle>
      </CardHeader>
      {description ? (
        <CardContent className="pb-5 pt-0 text-sm text-muted-foreground">
          {description}
        </CardContent>
      ) : null}
    </Card>
  );
}
