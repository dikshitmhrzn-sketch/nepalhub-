/** Mirrors the calculator contracts exposed by the ASP.NET Core API. */

/** The API serialises enums by name, so these are the literal wire values. */
export type CalculatorCategory = 'Finance' | 'Education' | 'Health' | 'Engineering' | 'Everyday';

export type InputKind = 'Amount' | 'Percent' | 'Years' | 'Number';

export type ValueFormat = 'Number' | 'Currency' | 'Percent' | 'Text';

/** The one shape every paged endpoint in the API returns. */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

/** One row on the public catalogue. */
export interface CalculatorSummary {
  slug: string;
  name: string;
  summary: string;
  category: CalculatorCategory;
  isPublished: boolean;
}

/** One input on a calculator. The whole form is described by this array. */
export interface InputFieldDefinition {
  key: string;
  label: string;
  kind: InputKind;
  unit: string | null;
  minimum: number | null;
  maximum: number | null;
  step: number | null;
  defaultValue: number | null;
  required: boolean;
  help: string | null;
}

/** The public view of one calculator, including the form to render. */
export interface CalculatorDetail {
  id: string;
  slug: string;
  name: string;
  summary: string;
  description: string;
  category: CalculatorCategory;
  inputs: InputFieldDefinition[];
  isPublished: boolean;
  sortOrder: number;
  updatedAt: string;
}

/** One output row. A line is numeric or textual, never both. */
export interface CalculationLine {
  key: string;
  label: string;
  format: ValueFormat;
  value: number | null;
  unit: string | null;
  text: string | null;
}

export interface CalculationResult {
  summary: CalculationLine[];
  breakdown: CalculationLine[];
  notes: string[];
}

export interface Calculation {
  slug: string;
  name: string;
  result: CalculationResult;
}

/** One row in the staff list, which deliberately omits the input schema. */
export interface CalculatorAdminSummary {
  id: string;
  slug: string;
  name: string;
  summary: string;
  category: CalculatorCategory;
  isPublished: boolean;
  sortOrder: number;
  updatedAt: string;
  createdByEmail: string | null;
}

/** The staff view of one row, which also carries the input schema. */
export interface CalculatorAdmin extends CalculatorDetail {
  createdAt: string;
  createdByEmail: string | null;
}

/** Create and update share one body, so the admin form sends the same shape. */
export interface SaveCalculatorRequest {
  slug: string;
  name: string;
  summary: string;
  description: string;
  category: CalculatorCategory;
  sortOrder: number;
  isPublished: boolean;
  inputs: InputFieldDefinition[];
}

export interface CalculatorListQuery {
  page?: number;
  pageSize?: number;
  search?: string;
  category?: CalculatorCategory;
}

export const calculatorCategories: readonly CalculatorCategory[] = [
  'Finance',
  'Education',
  'Health',
  'Engineering',
  'Everyday',
];

export const inputKinds: readonly InputKind[] = ['Amount', 'Percent', 'Years', 'Number'];

/** The unit suffix and input step a kind implies, so the form needs no special cases. */
const kindHints: Record<InputKind, { unit: string | null; step: number }> = {
  Amount: { unit: 'NPR', step: 1000 },
  Percent: { unit: '%', step: 0.1 },
  Years: { unit: 'years', step: 1 },
  Number: { unit: null, step: 1 },
};

export function unitForKind(kind: InputKind): string | null {
  return kindHints[kind].unit;
}

export function stepForKind(kind: InputKind): number {
  return kindHints[kind].step;
}

/** An empty row for the "add an input" control on the staff form. */
export function emptyInputField(index: number): InputFieldDefinition {
  return {
    key: `field${index + 1}`,
    label: '',
    kind: 'Number',
    unit: null,
    minimum: null,
    maximum: null,
    step: kindHints.Number.step,
    defaultValue: null,
    required: true,
    help: null,
  };
}
