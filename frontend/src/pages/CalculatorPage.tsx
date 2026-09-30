import { Link, useParams } from 'react-router';

import { CalculationResultView } from '@/components/calculators/CalculationResult';
import { CalculatorForm } from '@/components/calculators/CalculatorForm';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import { useCalculation } from '@/features/calculators/useCalculation';
import { useCalculator } from '@/features/calculators/useCalculator';
import { getErrorMessage, getFieldErrors } from '@/lib/apiErrors';

import styles from './CalculatorPage.module.css';

export function CalculatorPage() {
  const { slug = '' } = useParams();
  const calculator = useCalculator(slug);
  const calculation = useCalculation(slug);

  if (calculator.status === 'loading') {
    return (
      <Container as="section" className={styles.page}>
        <StateMessage tone="loading" title="Loading the calculator…" />
      </Container>
    );
  }

  if (calculator.status === 'error') {
    const isMissing = calculator.error.status === 404;

    return (
      <Container as="section" className={styles.page}>
        <StateMessage
          tone="error"
          title={isMissing ? 'That calculator is not here' : 'Could not load the calculator'}
          description={getErrorMessage(calculator.error, 'Please try again in a moment.')}
          action={
            isMissing ? (
              <Link to="/tools" className={styles.backLink}>
                Back to all tools
              </Link>
            ) : (
              <Button variant="secondary" size="small" onClick={calculator.reload}>
                Try again
              </Button>
            )
          }
        />
      </Container>
    );
  }

  const detail = calculator.data;
  const isCalculating = calculation.status === 'loading';

  function handleSubmit(values: Record<string, number>) {
    void calculation.run(values);
  }

  return (
    <Container as="section" className={styles.page}>
      <nav aria-label="Breadcrumb">
        <Link to="/tools" className={styles.backLink}>
          ← All tools
        </Link>
      </nav>

      <header className={styles.header}>
        <p className={styles.category}>{detail.category}</p>
        <h1 className={styles.title}>{detail.name}</h1>
        <p className={styles.description}>{detail.description}</p>
      </header>

      <div className={styles.layout}>
        <div className={styles.formColumn}>
          <h2 className={styles.sectionTitle}>Enter your figures</h2>

          <CalculatorForm
            key={detail.slug}
            fields={detail.inputs}
            isSubmitting={isCalculating}
            formError={
              calculation.status === 'error'
                ? getErrorMessage(calculation.error, 'The calculation could not be run.')
                : null
            }
            fieldErrors={calculation.status === 'error' ? getFieldErrors(calculation.error) : {}}
            onSubmit={handleSubmit}
          />
        </div>

        <div className={styles.resultColumn}>
          {calculation.status === 'idle' ? (
            <StateMessage
              tone="info"
              title="Nothing calculated yet"
              description="Fill in the figures and press Calculate."
            />
          ) : null}

          {calculation.status === 'loading' ? (
            <StateMessage tone="loading" title="Calculating…" />
          ) : null}

          {calculation.status === 'success' ? (
            <>
              <CalculationResultView result={calculation.data.result} />

              <Button variant="ghost" size="small" onClick={calculation.reset}>
                Clear result
              </Button>
            </>
          ) : null}
        </div>
      </div>
    </Container>
  );
}
