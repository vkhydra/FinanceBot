export function getQueryMessage(
  value: string | string[] | undefined,
): string | null {
  if (Array.isArray(value)) {
    return value[0] ?? null;
  }

  return value ?? null;
}

export function buildRedirect(path: string, params: Record<string, string>) {
  const query = new URLSearchParams(params).toString();
  return query.length > 0 ? `${path}?${query}` : path;
}
