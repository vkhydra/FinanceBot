import Link from "next/link";

import { cn } from "@/lib/utils";

type PageSectionNavItem = {
  href: string;
  label: string;
  description: string;
  active?: boolean;
};

type PageSectionNavProps = {
  items: PageSectionNavItem[];
};

export function PageSectionNav({ items }: PageSectionNavProps) {
  return (
    <nav className="app-panel overflow-hidden" aria-label="Navegacao da pagina">
      <div className="border-b border-border/60 px-4 py-3 sm:px-5">
        <p className="text-[0.68rem] font-medium uppercase tracking-[0.24em] text-muted-foreground">
          Navegacao da pagina
        </p>
        <p className="mt-1 text-sm text-muted-foreground">
          Escolha a secao que voce quer abrir agora.
        </p>
      </div>
      <div
        className="grid gap-2 p-2"
        style={{
          gridTemplateColumns: "repeat(auto-fit, minmax(12rem, 1fr))",
        }}
      >
        {items.map((item) => (
          <Link
            key={item.href}
            href={item.href}
            scroll={false}
            aria-current={item.active ? "page" : undefined}
            className={cn(
              "flex min-h-[5.25rem] min-w-0 flex-col items-start justify-center rounded-2xl border px-4 py-3 text-left transition-colors",
              item.active
                ? "border-primary/35 bg-primary text-primary-foreground shadow-[0_10px_22px_-18px_rgba(15,23,42,0.5)]"
                : "border-border/60 bg-background/60 text-foreground hover:border-border/80 hover:bg-muted/55",
            )}
          >
            <span
              className={cn(
                "mb-1 inline-flex items-center gap-2 text-[0.65rem] font-medium uppercase tracking-[0.22em]",
                item.active ? "text-primary-foreground/78" : "text-muted-foreground",
              )}
            >
              <span
                className={cn(
                  "size-1.5 rounded-full",
                  item.active ? "bg-primary-foreground/80" : "bg-primary/55",
                )}
              />
              {item.active ? "Secao atual" : "Abrir secao"}
            </span>
            <span className="w-full break-words text-sm font-semibold leading-5">{item.label}</span>
            <span
              className={cn(
                "mt-1 w-full break-words text-xs leading-5",
                item.active ? "text-primary-foreground/80" : "text-muted-foreground",
              )}
            >
              {item.description}
            </span>
          </Link>
        ))}
      </div>
    </nav>
  );
}
