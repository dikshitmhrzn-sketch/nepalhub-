import type { CalculationLine } from './types';

/**
 * The API decides the format and the unit; the client only groups digits. Values are
 * already rounded server-side, so a percentage arrives as percentage points (12.5,
 * not 0.125) and must not be scaled again here.
 */
const numberFormatter = new Intl.NumberFormat('en-NP', {
  maximumFractionDigits: 2,
  minimumFractionDigits: 0,
});

export function formatNumber(value: number): string {
  return numberFormatter.format(value);
}

/** Renders one output line as it should appear to a visitor. */
export function formatLine(line: CalculationLine): string {
  if (line.format === 'Text' || line.value === null) {
    return line.text ?? '—';
  }

  const formatted = formatNumber(line.value);

  return line.unit ? `${formatted} ${line.unit}` : formatted;
}
