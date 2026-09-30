import { useState, type FormEvent } from 'react';

import { Button } from '@/components/ui/Button';
import type { InputFieldDefinition } from '@/features/calculators/types';

import styles from './CalculatorForm.module.css';

interface CalculatorFormProps {
  fields: InputFieldDefinition[];
  isSubmitting: boolean;
  formError: string | null;
  fieldErrors: Record<string, string>;
  onSubmit: (values: Record<string, number>) => void;
}

function initialValues(fields: InputFieldDefinition[]): Record<string, string> {
  const values: Record<string, string> = {};

  for (const field of fields) {
    values[field.key] = field.defaultValue === null ? '' : String(field.defaultValue);
  }

  return values;
}

/**
 * Renders the form described by the API's input schema. Because the schema is data,
 * a calculator published later needs no new component here.
 */
export function CalculatorForm({
  fields,
  isSubmitting,
  formError,
  fieldErrors,
  onSubmit,
}: CalculatorFormProps) {
  const [values, setValues] = useState<Record<string, string>>(() => initialValues(fields));

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    // A blank value is left out entirely, so the server reports it as the missing
    // field it is rather than as a value that failed to parse.
    const payload: Record<string, number> = {};

    for (const field of fields) {
      const raw = values[field.key]?.trim();

      if (raw) {
        payload[field.key] = Number(raw);
      }
    }

    onSubmit(payload);
  }

  return (
    <form className={styles.form} onSubmit={handleSubmit} noValidate>
      {formError ? (
        <p className={styles.alert} role="alert">
          {formError}
        </p>
      ) : null}

      {fields.map((field) => {
        const errorId = `${field.key}-error`;
        const helpId = `${field.key}-help`;

        return (
          <div className={styles.field} key={field.key}>
            <label className={styles.label} htmlFor={field.key}>
              {field.label}
              {field.unit ? <span className={styles.unit}> ({field.unit})</span> : null}
              {field.required ? (
                <span className={styles.required} aria-hidden="true">
                  {' '}
                  *
                </span>
              ) : null}
            </label>

            <input
              className={styles.input}
              id={field.key}
              name={field.key}
              type="number"
              inputMode="decimal"
              step={field.step ?? undefined}
              min={field.minimum ?? undefined}
              max={field.maximum ?? undefined}
              required={field.required}
              value={values[field.key] ?? ''}
              aria-invalid={fieldErrors[field.key] ? true : undefined}
              aria-describedby={
                [fieldErrors[field.key] ? errorId : null, field.help ? helpId : null]
                  .filter(Boolean)
                  .join(' ') || undefined
              }
              onChange={(event) =>
                setValues((current) => ({ ...current, [field.key]: event.target.value }))
              }
            />

            {field.help ? (
              <span className={styles.help} id={helpId}>
                {field.help}
              </span>
            ) : null}

            {fieldErrors[field.key] ? (
              <span className={styles.fieldError} id={errorId}>
                {fieldErrors[field.key]}
              </span>
            ) : null}
          </div>
        );
      })}

      <Button type="submit" disabled={isSubmitting}>
        {isSubmitting ? 'Calculating…' : 'Calculate'}
      </Button>
    </form>
  );
}
