import { quizCategoryLabels, type QuizResultResponse } from '@/features/quiz/types';

import styles from './QuizResultView.module.css';

interface QuizResultViewProps {
  result: QuizResultResponse;
}

function formatCompletedAt(value: string): string {
  const parsed = new Date(value);

  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
}

export function QuizResultView({ result }: QuizResultViewProps) {
  const { progression, reviews } = result;
  const percentage = result.maxScore > 0 ? Math.round((result.score / result.maxScore) * 100) : 0;

  return (
    <div className={styles.result}>
      <header className={styles.summary}>
        <p className={styles.eyebrow}>{formatCompletedAt(result.completedAt)}</p>
        <h2 className={styles.score}>
          {result.score}
          <span className={styles.scoreMax}> / {result.maxScore}</span>
        </h2>
        <p className={styles.summaryLine}>
          {result.correctCount} correct of {result.maxScore} · {percentage}%
        </p>

        {progression.recorded ? (
          <dl className={styles.progression}>
            <div className={styles.progressionItem}>
              <dt>XP earned</dt>
              <dd>+{progression.xpAwarded}</dd>
            </div>
            <div className={styles.progressionItem}>
              <dt>Total XP</dt>
              <dd>{progression.totalXp}</dd>
            </div>
            <div className={styles.progressionItem}>
              <dt>Level</dt>
              <dd>{progression.level}</dd>
            </div>
            <div className={styles.progressionItem}>
              <dt>Day streak</dt>
              <dd>{progression.streakCount}</dd>
            </div>
          </dl>
        ) : (
          <p className={styles.guestNotice}>
            You played as a guest, so this score was not saved. Sign in to collect XP and keep a
            streak.
          </p>
        )}
      </header>

      <section aria-labelledby="review-heading" className={styles.reviews}>
        <h3 id="review-heading" className={styles.reviewsHeading}>
          Review
        </h3>

        <ol className={styles.reviewList}>
          {reviews.map((review) => (
            <li
              key={review.questionId}
              className={`${styles.review} ${review.isCorrect ? styles.reviewCorrect : styles.reviewWrong}`}
            >
              <div className={styles.reviewHead}>
                <span className={styles.reviewCategory}>{quizCategoryLabels[review.category]}</span>
                <span className={styles.reviewVerdict}>{review.isCorrect ? 'Correct' : 'Incorrect'}</span>
              </div>

              <p className={styles.reviewQuestion}>{review.text}</p>

              <dl className={styles.answerList}>
                <div className={styles.answer}>
                  <dt>You answered</dt>
                  <dd className={review.isCorrect ? styles.answerRight : styles.answerWrong}>
                    {review.selectedOptionText}
                  </dd>
                </div>

                {!review.isCorrect ? (
                  <div className={styles.answer}>
                    <dt>Correct answer</dt>
                    <dd className={styles.answerRight}>{review.correctOptionText}</dd>
                  </div>
                ) : null}

                <div className={styles.answer}>
                  <dt>Why</dt>
                  <dd>{review.explanation}</dd>
                </div>
              </dl>
            </li>
          ))}
        </ol>
      </section>
    </div>
  );
}
