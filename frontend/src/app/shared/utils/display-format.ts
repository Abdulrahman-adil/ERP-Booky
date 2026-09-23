const amountFormatter = new Intl.NumberFormat('en-AE', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export function formatAmount(value: number): string {
  return amountFormatter.format(value);
}

export function utcDateInput(date: Date = new Date()): string {
  return date.toISOString().slice(0, 10);
}
