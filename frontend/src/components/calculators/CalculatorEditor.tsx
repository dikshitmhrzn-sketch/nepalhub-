import { useEffect, useState, type FormEvent } from 'react';

import { Button } from '@/components/ui/Button';
import { StateMessage } from '@/components/ui/StateMessage';
import { createCalculator, updateCalculator } from '@/features/calculators/api';
import { useStaffCalculator } from '@/features/calculators/useStaffCalculators';
import {
  calculatorCategories,
  emptyInputField,
  inputKinds,
  stepForKind,
  type CalculatorAdmin,
  type InputFieldDefinition,
  type SaveCalculatorRequest,
} from '@/features/calculators/types';
import { getErrorMessage, getFieldErrors } from '@/lib/apiErrors';

import styles from './CalculatorEditor.module.css';

interface CalculatorEditorProps {
  id: string | null;
  onCancel: () => void;
  onSaved: (saved: { id: string; name: string }) => void;
}

function emptyRequest(): SaveCalculatorRequest {
  return {
    slug: '',
    name: '',
    summary: '',
    description: '',
    category: 'Everyday',
    sortOrder: 0,
    isPublished: false,
    inputs: [emptyInputField(0)],
  };
}

function toRequest(detail: CalculatorAdmin): SaveCalculatorRequest {
  return {
    slug: detail.slug,
    name: detail.name,
    summary: detail.summary,
    description: detail.description,
    category: detail.category,
    sortOrder: detail.sortOrder,
    isPublished: detail.isPublished,
    inputs: detail.inputs.map((field) => ({ ...field })),
  };
}

/** Turns an empty number box into null rather than NaN, which the API rejects. */
function parseOptionalNumber(value: string): number | null {
  const trimmed = value.trim();

  return trimmed === '' || Number.isNaN(Number(trimmed)) ? null : Number(trimmed);
}

function toOptionalString(value: string): string | null {
  const trimmed = value.trim();

  return trimmed === '' ? null : trimmed;
}

/**
 * Create and edit in one form, because the API shares one request shape for both.
 * The slug is shown read-only while editing: the API treats a changed slug as a
 * different row, so it is refused rather than silently ignored.
 */
export function CalculatorEditor({ id, onCancel, onSaved }: CalculatorEditorProps) {
  const detail = useStaffCalculator(id);
  const loaded = detail.status === 'success' ? detail.data : null;
  const loadedId = loaded?.id ?? null;

  const [form, setForm] = useState<SaveCalculatorRequest>(emptyRequest);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    setForm(loaded ? toRequest(loaded) : emptyRequest());
  }, [loadedId]);

  function updateInput(index: number, patch: Partial<InputFieldDefinition>) {
    setForm((current) => ({
      ...current,
      inputs: current.inputs.map((row, position) =>
        position === index ? { ...row, ...patch } : row,
      ),
    }));
  }

  function addInput() {
    setForm((current) => ({
      ...current,
      inputs: [...current.inputs, emptyInputField(current.inputs.length)],
    }));
  }

  function removeInput(index: number) {
    setForm((current) => ({
      ...current,
      inputs: current.inputs.filter((_, position) => position !== index),
    }));
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setFieldErrors({});
    setFormError(null);

    const body: SaveCalculatorRequest = {
      ...form,
      inputs: form.inputs.map((row) => ({
        ...row,
        key: row.key.trim(),
        label: row.label.trim(),
        unit: toOptionalString(row.unit ?? ''),
        help: toOptionalString(row.help ?? ''),
      })),
    };

    try {
      const saved = id ? await updateCalculator(id, body) : await createCalculator(body);
      onSaved({ id: saved.id, name: saved.name });
    } catch (error) {
      setFieldErrors(getFieldErrors(error));
      setFormError(getErrorMessage(error, 'The calculator could not be saved.'));
    } finally {
      setIsSaving(false);
    }
  }

  if (id && detail.status === 'loading') {
    return <StateMessage tone="loading" title="Loading the calculator…" />;
  }

  if (id && detail.status === 'error') {
    return (
      <StateMessage
        tone="error"
        title="Could not load that calculator"
        description={getErrorMessage(detail.error, 'It may have been deleted.')}
        action={
          <Button variant="secondary" size="small" onClick={onCancel}>
            Back to the list
          </Button>
        }
      />
    );
  }

  return (
    <form className={styles.form} onSubmit={handleSubmit} noValidate>
      <h2 className={styles.title}>{id ? 'Edit calculator' : 'New calculator'}</h2>

      {formError ? (
        <p className={styles.alert} role="alert">
          {formError}
        </p>
      ) : null}

      {id ? (
        <p className={styles.note}>
          The slug is permanent. Create a new calculator instead if the address has to change.
        </p>
      ) : null}

      <div className={styles.field}>
        <label className={styles.label} htmlFor="slug">
          Slug
        </label>
        <input
          className={styles.input}
          id="slug"
          name="slug"
          type="text"
          readOnly={id !== null}
          required
          value={form.slug}
          aria-invalid={fieldErrors.slug ? true : undefined}
          aria-describedby={fieldErrors.slug ? 'slug-error' : undefined}
          onChange={(event) => setForm((current) => ({ ...current, slug: event.target.value }))}
        />
        {fieldErrors.slug ? (
          <span className={styles.fieldError} id="slug-error">
            {fieldErrors.slug}
          </span>
        ) : (
          <span className={styles.help}>Lowercase words with single hyphens, for example loan-emi.</span>
        )}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="name">
          Name
        </label>
        <input
          className={styles.input}
          id="name"
          name="name"
          type="text"
          required
          value={form.name}
          aria-invalid={fieldErrors.name ? true : undefined}
          onChange={(event) => setForm((current) => ({ ...current, name: event.target.value }))}
        />
        {fieldErrors.name ? <span className={styles.fieldError}>{fieldErrors.name}</span> : null}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="summary">
          Summary
        </label>
        <input
          className={styles.input}
          id="summary"
          name="summary"
          type="text"
          required
          value={form.summary}
          aria-invalid={fieldErrors.summary ? true : undefined}
          aria-describedby="summary-hint"
          onChange={(event) => setForm((current) => ({ ...current, summary: event.target.value }))}
        />
        <span className={styles.help} id="summary-hint">
          One line for the catalogue card, 10 to 280 characters.
        </span>
        {fieldErrors.summary ? <span className={styles.fieldError}>{fieldErrors.summary}</span> : null}
      </div>

      <div className={styles.field}>
        <label className={styles.label} htmlFor="description">
          Description
        </label>
        <textarea
          className={styles.textarea}
          id="description"
          name="description"
          rows={4}
          required
          value={form.description}
          aria-invalid={fieldErrors.description ? true : undefined}
          onChange={(event) =>
            setForm((current) => ({ ...current, description: event.target.value }))
          }
        />
        {fieldErrors.description ? (
          <span className={styles.fieldError}>{fieldErrors.description}</span>
        ) : null}
      </div>

      <div className={styles.pair}>
        <div className={styles.field}>
          <label className={styles.label} htmlFor="category">
            Category
          </label>
          <select
            className={styles.input}
            id="category"
            name="category"
            value={form.category}
            onChange={(event) =>
              setForm((current) => ({
                ...current,
                category: event.target.value as SaveCalculatorRequest['category'],
              }))
            }
          >
            {calculatorCategories.map((value) => (
              <option key={value} value={value}>
                {value}
              </option>
            ))}
          </select>
        </div>

        <div className={styles.field}>
          <label className={styles.label} htmlFor="sortOrder">
            Sort order
          </label>
          <input
            className={styles.input}
            id="sortOrder"
            name="sortOrder"
            type="number"
            min={0}
            max={10000}
            value={form.sortOrder}
            onChange={(event) =>
              setForm((current) => ({
                ...current,
                sortOrder: event.target.value === '' ? 0 : Number(event.target.value),
              }))
            }
          />
        </div>
      </div>

      <div className={styles.checkboxField}>
        <input
          id="isPublished"
          name="isPublished"
          type="checkbox"
          checked={form.isPublished}
          aria-invalid={fieldErrors.isPublished ? true : undefined}
          aria-describedby={fieldErrors.isPublished ? 'isPublished-error' : 'isPublished-hint'}
          onChange={(event) =>
            setForm((current) => ({ ...current, isPublished: event.target.checked }))
          }
        />
        <label className={styles.label} htmlFor="isPublished">
          Published
        </label>
      </div>
      {fieldErrors.isPublished ? (
        <span className={styles.fieldError} id="isPublished-error">
          {fieldErrors.isPublished}
        </span>
      ) : (
        <span className={styles.help} id="isPublished-hint">
          Only a published calculator with a server-side handler can be published.
        </span>
      )}

      <fieldset className={styles.fieldset}>
        <legend className={styles.legend}>Inputs</legend>

        {fieldErrors.inputs ? (
          <p className={styles.fieldError}>{fieldErrors.inputs}</p>
        ) : null}

        {form.inputs.map((row, index) => (
          <div className={styles.inputRow} key={index}>
            <div className={styles.pair}>
              <div className={styles.field}>
                <label className={styles.label} htmlFor={`input-key-${index}`}>
                  Key
                </label>
                <input
                  className={styles.input}
                  id={`input-key-${index}`}
                  type="text"
                  value={row.key}
                  onChange={(event) => updateInput(index, { key: event.target.value })}
                />
              </div>

              <div className={styles.field}>
                <label className={styles.label} htmlFor={`input-label-${index}`}>
                  Label
                </label>
                <input
                  className={styles.input}
                  id={`input-label-${index}`}
                  type="text"
                  value={row.label}
                  onChange={(event) => updateInput(index, { label: event.target.value })}
                />
              </div>
            </div>

            <div className={styles.pair}>
              <div className={styles.field}>
                <label className={styles.label} htmlFor={`input-kind-${index}`}>
                  Kind
                </label>
                <select
                  className={styles.input}
                  id={`input-kind-${index}`}
                  value={row.kind}
                  onChange={(event) =>
                    updateInput(index, {
                      kind: event.target.value as InputFieldDefinition['kind'],
                      step: stepForKind(event.target.value as InputFieldDefinition['kind']),
                    })
                  }
                >
                  {inputKinds.map((value) => (
                    <option key={value} value={value}>
                      {value}
                    </option>
                  ))}
                </select>
              </div>

              <div className={styles.field}>
                <label className={styles.label} htmlFor={`input-unit-${index}`}>
                  Unit
                </label>
                <input
                  className={styles.input}
                  id={`input-unit-${index}`}
                  type="text"
                  value={row.unit ?? ''}
                  placeholder="NPR"
                  onChange={(event) => updateInput(index, { unit: event.target.value })}
                />
              </div>
            </div>

            <div className={styles.pair}>
              <div className={styles.field}>
                <label className={styles.label} htmlFor={`input-min-${index}`}>
                  Minimum
                </label>
                <input
                  className={styles.input}
                  id={`input-min-${index}`}
                  type="number"
                  value={row.minimum ?? ''}
                  onChange={(event) =>
                    updateInput(index, { minimum: parseOptionalNumber(event.target.value) })
                  }
                />
              </div>

              <div className={styles.field}>
                <label className={styles.label} htmlFor={`input-max-${index}`}>
                  Maximum
                </label>
                <input
                  className={styles.input}
                  id={`input-max-${index}`}
                  type="number"
                  value={row.maximum ?? ''}
                  onChange={(event) =>
                    updateInput(index, { maximum: parseOptionalNumber(event.target.value) })
                  }
                />
              </div>
            </div>

            <div className={styles.pair}>
              <div className={styles.field}>
                <label className={styles.label} htmlFor={`input-step-${index}`}>
                  Step
                </label>
                <input
                  className={styles.input}
                  id={`input-step-${index}`}
                  type="number"
                  value={row.step ?? ''}
                  onChange={(event) =>
                    updateInput(index, { step: parseOptionalNumber(event.target.value) })
                  }
                />
              </div>

              <div className={styles.field}>
                <label className={styles.label} htmlFor={`input-default-${index}`}>
                  Default
                </label>
                <input
                  className={styles.input}
                  id={`input-default-${index}`}
                  type="number"
                  value={row.defaultValue ?? ''}
                  onChange={(event) =>
                    updateInput(index, { defaultValue: parseOptionalNumber(event.target.value) })
                  }
                />
              </div>
            </div>

            <div className={styles.rowFooter}>
              <div className={styles.checkboxField}>
                <input
                  id={`input-required-${index}`}
                  type="checkbox"
                  checked={row.required}
                  onChange={(event) => updateInput(index, { required: event.target.checked })}
                />
                <label className={styles.label} htmlFor={`input-required-${index}`}>
                  Required
                </label>
              </div>

              <Button
                variant="ghost"
                size="small"
                onClick={() => removeInput(index)}
                disabled={form.inputs.length === 1}
              >
                Remove input
              </Button>
            </div>
          </div>
        ))}

        <Button variant="secondary" size="small" onClick={addInput}>
          Add input
        </Button>
      </fieldset>

      <div className={styles.actions}>
        <Button type="submit" disabled={isSaving}>
          {isSaving ? 'Saving…' : id ? 'Save changes' : 'Create calculator'}
        </Button>
        <Button type="button" variant="ghost" onClick={onCancel} disabled={isSaving}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
