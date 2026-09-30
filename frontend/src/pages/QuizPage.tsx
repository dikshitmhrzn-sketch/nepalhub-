import { useState, type FormEvent } from 'react';
import { Link } from 'react-router';

import { QuizResultView } from '@/components/quiz/QuizResultView';
import { Button } from '@/components/ui/Button';
import { Container } from '@/components/ui/Container';
import { StateMessage } from '@/components/ui/StateMessage';
import { useAuth } from '@/features/auth/AuthContext';
import {
  useDailyQuiz,
  useDailyQuizResult,
  useQuizStats,
  useQuizSubmit,
} from '@/features/quiz/useDailyQuiz';
import { getErrorMessage } from '@/lib/apiErrors';
import { quizCategoryLabels, type QuizQuestionResponse } from '@/features/quiz/types';

import styles from './QuizPage.module.css';

function QuizQuestionCard({
  question,
  selectedOptionId,
  disabled,
  onSelect,
}: {
  question: QuizQuestionResponse;
  selectedOptionId: string | undefined;
  disabled: boolean;
  onSelect: (optionId: string) => void;
}) {
  return (
    <li className={styles.question}>
      <p className={styles.questionMeta}>
        <span className={styles.questionNumber}>Question {question.position}</span>
        <span className={styles.questionCategory}>{quizCategoryLabels[question.category]}</span>
      </p>

      <h2 className={styles.questionText}>{question.text}</h2>

      <ul className={styles.options}>
        {question.options.map((option) => {
          const inputId = `${question.id}-${option.id}`;
          const isChecked = selectedOptionId === option.id;

          return (
            <li key={option.id}>
              <label
                htmlFor={inputId}
                className={`${styles.option} ${isChecked ? styles.optionSelected : ''}`}
              >
                <input
                  id={inputId}
                  className={styles.optionInput}
                  type="radio"
                  name={question.id}
                  value={option.id}
                  checked={isChecked}
                  disabled={disabled}
                  onChange={() => onSelect(option.id)}
                />
                <span className={styles.optionText}>{option.text}</span>
              </label>
            </li>
          );
        })}
      </ul>
    </li>
  );
}

function StatsSummary() {
  const stats = useQuizStats(true);

  if (stats.status !== 'success') {
    return null;
  }

  const { data } = stats;

  return (
    <dl className={styles.stats}>
      <div className={styles.stat}>
        <dt>Total XP</dt>
        <dd>{data.totalXp}</dd>
      </div>
      <div className={styles.stat}>
        <dt>Level</dt>
        <dd>{data.level}</dd>
      </div>
      <div className={styles.stat}>
        <dt>Day streak</dt>
        <dd>{data.streakCount}</dd>
      </div>
      <div className={styles.stat}>
        <dt>Quizzes</dt>
        <dd>{data.quizzesCompleted}</dd>
      </div>
    </dl>
  );
}

export function QuizPage() {
  const { status } = useAuth();
  const isSignedIn = status === 'authenticated';

  const daily = useDailyQuiz();
  const submit = useQuizSubmit();

  const [selections, setSelections] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);

  const alreadyCompleted = daily.status === 'success' && daily.data.alreadyCompleted;
  // Only worth asking for once the daily call has said this player already played,
  // otherwise a signed-in visitor on their first visit would collect a pointless 401.
  const stored = useDailyQuizResult(isSignedIn && alreadyCompleted);

  const submittedResult = submit.status === 'success' ? submit.data : null;
  const storedResult = stored.status === 'success' ? stored.data : null;
  const result = submittedResult ?? storedResult;

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();

    if (daily.status !== 'success') {
      return;
    }

    const unanswered = daily.data.questions.filter((question) => !selections[question.id]);

    if (unanswered.length > 0) {
      setFormError(
        `Answer all ${daily.data.questionCount} questions before submitting. ${unanswered.length} still to go.`,
      );

      return;
    }

    setFormError(null);

    await submit.run({
      answers: daily.data.questions.map((question) => ({
        questionId: question.id,
        optionId: selections[question.id],
      })),
    });
  }

  return (
    <Container as="section" className={styles.page}>
      <header className={styles.header}>
        <h1 className={styles.title}>Daily quiz</h1>
        <p className={styles.subtitle}>
          Ten questions about Nepal and the wider world. One attempt a day, and everyone gets the same
          set.
        </p>
        {isSignedIn ? <StatsSummary /> : null}
      </header>

      {result ? (
        <>
          <p className={styles.doneNotice} role="status">
            You have already played today&rsquo;s quiz. Come back tomorrow for a new one.
          </p>
          <QuizResultView result={result} />
        </>
      ) : null}

      {!result && daily.status === 'loading' ? (
        <StateMessage tone="loading" title="Loading today&rsquo;s quiz" />
      ) : null}

      {!result && daily.status === 'error' ? (
        <StateMessage
          tone="error"
          title="Could not load the quiz"
          description={getErrorMessage(daily.error, 'Please try again in a moment.')}
          action={<Button onClick={daily.reload}>Try again</Button>}
        />
      ) : null}

      {!result && daily.status === 'success' ? (
        <form className={styles.form} onSubmit={handleSubmit} noValidate>
          <p className={styles.pot} role="status">
            Up to {daily.data.maxXpAvailable} XP today
            {!isSignedIn ? ' · sign in to collect it' : ''}
          </p>

          {formError ? (
            <p className={styles.formError} role="alert">
              {formError}
            </p>
          ) : null}

          {submit.status === 'error' ? (
            <p className={styles.formError} role="alert">
              {getErrorMessage(submit.error, 'Your answers could not be submitted.')}
            </p>
          ) : null}

          <ol className={styles.questions}>
            {daily.data.questions.map((question) => (
              <QuizQuestionCard
                key={question.id}
                question={question}
                selectedOptionId={selections[question.id]}
                disabled={submit.status === 'submitting'}
                onSelect={(optionId) =>
                  setSelections((current) => ({ ...current, [question.id]: optionId }))
                }
              />
            ))}
          </ol>

          <div className={styles.actions}>
            <Button type="submit" disabled={submit.status === 'submitting'}>
              {submit.status === 'submitting' ? 'Submitting…' : 'Submit answers'}
            </Button>
            {!isSignedIn ? (
              <Link className={styles.signInHint} to="/login">
                Sign in to save your score
              </Link>
            ) : null}
          </div>
        </form>
      ) : null}
    </Container>
  );
}
