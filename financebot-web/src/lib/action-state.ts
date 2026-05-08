export function getQueryMessage(
  value: string | string[] | undefined,
): string | null {
  if (Array.isArray(value)) {
    return value[0] ?? null;
  }

  return value ?? null;
}

export function buildRedirect(path: string, params: Record<string, string>) {
  const [pathname, currentQuery = ""] = path.split("?", 2);
  const nextParams = new URLSearchParams(currentQuery);

  for (const [key, value] of Object.entries(params)) {
    nextParams.set(key, value);
  }

  const query = nextParams.toString();
  return query.length > 0 ? `${pathname}?${query}` : pathname;
}
