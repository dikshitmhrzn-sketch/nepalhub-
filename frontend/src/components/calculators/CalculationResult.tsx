import { formatLine } from '@/features/calculators/format';
import type { CalculationLine, CalculationResult } from '@/features/calculators/types';

import styles from './CalculationResult.module.css';

interface CalculationResultProps {
  result: CalculationResult;
}

function LineList({ lines }: { lines: CalculationLine[] }) {
  return (
    <dl className={styles.lines}>
      {lines.map((line) => (
        <div className={styles.line} key={line.key}>
          <dt className={styles.lineLabel}>{line.label}</dt>
          <dd className={styles.lineValue}>{formatLine(line)}</dd>
        </div>
      ))}
    </dl>
  );
}

/** Renders a finished calculation: the headline answers, then the working. */
export function CalculationResultView({ result }: CalculationResultProps) {
  return (
    <section className={styles.result} aria-live="polite">
      <h3 className={styles.heading}>Result</h3>

      <LineList lines={result.summary} />

      {result.breakdown.length > 0 ? (
        <>
          <h4 className={styles.subheading}>How this was worked out</h4>
          <LineList lines={result.breakdown} />
        </>
      ) : null}

      {result.notes.length > 0 ? (
        <>
          <h4 className={styles.subheading}>Good to know</h4>
          <ul className={styles.notes}>
            {result.notes.map((note) => (
              <li key={note}>{note}</li>
            ))}
          </ul>
        </>
      ) : null}
    </section>
  );
}
