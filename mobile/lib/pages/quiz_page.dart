import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/api/api_client.dart';
import '../../core/api/api_exception.dart';
import '../../core/theme/tokens.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/state_message.dart';
import '../../features/quiz/api.dart';

class QuizPage extends StatefulWidget {
  const QuizPage({super.key});

  @override
  State<QuizPage> createState() => _QuizPageState();
}

class _QuizPageState extends State<QuizPage> {
  DailyQuiz? _quiz;
  QuizResult? _result;
  final Map<String, String> _answers = <String, String>{};
  Object? _error;
  bool _loading = true;
  bool _submitting = false;
  String? _submitError;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
      _submitError = null;
    });
    try {
      final api = QuizApi(context.read<ApiClient>());
      final quiz = await api.daily();
      if (!mounted) {
        return;
      }
      setState(() {
        _quiz = quiz;
        _loading = false;
      });
    } catch (error) {
      if (!mounted) {
        return;
      }
      setState(() {
        _error = error;
        _loading = false;
      });
    }
  }

  Future<void> _submit() async {
    final quiz = _quiz;
    if (quiz == null) {
      return;
    }
    setState(() {
      _submitting = true;
      _submitError = null;
    });
    final answers = [
      for (final question in quiz.questions)
        if (_answers.containsKey(question.id))
          QuizAnswer(
            questionId: question.id,
            optionId: _answers[question.id]!,
          ),
    ];
    try {
      final api = QuizApi(context.read<ApiClient>());
      final result = await api.submit(answers);
      if (!mounted) {
        return;
      }
      setState(() {
        _result = result;
        _submitting = false;
      });
    } catch (error) {
      if (!mounted) {
        return;
      }
      setState(() {
        _submitError = error is ApiException
            ? ApiMessage.message(error)
            : 'Could not submit your answers. Please try again.';
        _submitting = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      appBar: AppBar(title: const Text('Daily quiz')),
      body: _buildBody(theme),
    );
  }

  Widget _buildBody(ThemeData theme) {
    if (_loading) {
      return const StateMessage(tone: StateMessageTone.loading, title: 'Loading');
    }
    if (_error != null) {
      final apiError = _error is ApiException ? _error as ApiException : null;
      return StateMessage(
        tone: StateMessageTone.error,
        title: apiError == null ? 'Something went wrong' : ApiMessage.title(apiError),
        description: apiError == null ? '$_error' : ApiMessage.message(apiError),
        actionLabel: 'Try again',
        onAction: _load,
      );
    }
    final result = _result;
    if (result != null) {
      return _ResultView(result: result, onRestart: _load);
    }
    final quiz = _quiz;
    if (quiz == null || quiz.questions.isEmpty) {
      return StateMessage(
        tone: StateMessageTone.empty,
        title: 'No quiz today',
        description: 'The daily quiz has not been published yet. Come back tomorrow.',
        actionLabel: 'Refresh',
        onAction: _load,
      );
    }
    if (quiz.alreadyCompleted) {
      return const StateMessage(
        tone: StateMessageTone.empty,
        title: 'You already played today',
        description: 'Come back tomorrow for a new set of questions.',
      );
    }
    final answered = _answers.length == quiz.questions.length;
    return ListView(
      padding: const EdgeInsets.all(NhSpace.s4),
      children: [
        Text(
          '${quiz.questionCount} questions  ·  up to ${quiz.maxXpAvailable} XP',
          style: theme.textTheme.bodyMedium?.copyWith(
            color: theme.colorScheme.onSurfaceVariant,
          ),
        ),
        const SizedBox(height: NhSpace.s3),
        for (final question in quiz.questions) ...[
          Card(
            margin: const EdgeInsets.only(bottom: NhSpace.s3),
            child: Padding(
              padding: const EdgeInsets.all(NhSpace.s3),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    '${question.position + 1}. ${question.text}',
                    style: theme.textTheme.titleSmall?.copyWith(
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  if (quizCategories[question.category] != null) ...[
                    const SizedBox(height: NhSpace.s1),
                    Text(
                      quizCategories[question.category]!,
                      style: theme.textTheme.labelSmall?.copyWith(
                        color: theme.colorScheme.primary,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                  const SizedBox(height: NhSpace.s1),
                  RadioGroup<String>(
                    groupValue: _answers[question.id],
                    onChanged: (value) {
                      if (value == null) {
                        return;
                      }
                      setState(() => _answers[question.id] = value);
                    },
                    child: Column(
                      children: [
                        for (final option in question.options)
                          RadioListTile<String>(
                            value: option.id,
                            dense: true,
                            contentPadding: EdgeInsets.zero,
                            title: Text(option.text),
                          ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ],
        if (_submitError != null)
          Padding(
            padding: const EdgeInsets.only(bottom: NhSpace.s2),
            child: Text(
              _submitError!,
              style: theme.textTheme.bodyMedium?.copyWith(color: theme.colorScheme.error),
            ),
          ),
        FilledButton.icon(
          onPressed: answered && !_submitting ? _submit : null,
          icon: _submitting
              ? const SizedBox(
                  width: 16,
                  height: 16,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Icon(Icons.check_rounded, size: 18),
          label: Text(_submitting ? 'Submitting' : 'Submit answers'),
        ),
        if (!answered)
          Padding(
            padding: const EdgeInsets.only(top: NhSpace.s2),
            child: Text(
              'Answer every question to submit.',
              style: theme.textTheme.bodySmall?.copyWith(
                color: theme.colorScheme.onSurfaceVariant,
              ),
            ),
          ),
      ],
    );
  }
}

class _ResultView extends StatelessWidget {
  const _ResultView({required this.result, required this.onRestart});

  final QuizResult result;
  final VoidCallback onRestart;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    return ListView(
      padding: const EdgeInsets.all(NhSpace.s4),
      children: [
        Card(
          margin: EdgeInsets.zero,
          child: Padding(
            padding: const EdgeInsets.all(NhSpace.s4),
            child: Column(
              children: [
                Text(
                  '${result.score} / ${result.maxScore}',
                  style: theme.textTheme.displaySmall?.copyWith(
                    fontWeight: FontWeight.w800,
                    color: colors.primary,
                  ),
                ),
                const SizedBox(height: NhSpace.s1),
                Text(
                  result.score == result.maxScore
                      ? 'Perfect score'
                      : '${result.correctCount} correct out of ${result.maxScore}',
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                if (result.progression != null) ...[
                  const SizedBox(height: NhSpace.s3),
                  Wrap(
                    spacing: NhSpace.s2,
                    alignment: WrapAlignment.center,
                    children: [
                      _Pill(label: '+${result.progression!.xpAwarded} XP'),
                      _Pill(label: 'Level ${result.progression!.level}'),
                      _Pill(label: 'Streak ${result.progression!.streakCount}'),
                    ],
                  ),
                ],
              ],
            ),
          ),
        ),
        const SizedBox(height: NhSpace.s4),
        Text(
          'Review',
          style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
        ),
        const SizedBox(height: NhSpace.s2),
        for (final review in result.reviews) ...[
          Card(
            margin: const EdgeInsets.only(bottom: NhSpace.s2),
            child: Padding(
              padding: const EdgeInsets.all(NhSpace.s3),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Icon(
                        review.isCorrect ? Icons.check_circle_rounded : Icons.cancel_rounded,
                        size: 20,
                        color: review.isCorrect ? const Color(0xFF128A4C) : colors.error,
                      ),
                      const SizedBox(width: NhSpace.s2),
                      Expanded(
                        child: Text(
                          review.text,
                          style: theme.textTheme.titleSmall?.copyWith(
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: NhSpace.s1),
                  Text(
                    'Your answer: ${review.selectedOptionText}',
                    style: theme.textTheme.bodyMedium?.copyWith(
                      color: review.isCorrect ? const Color(0xFF128A4C) : colors.error,
                    ),
                  ),
                  if (!review.isCorrect)
                    Text(
                      'Correct answer: ${review.correctOptionText}',
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: const Color(0xFF128A4C),
                      ),
                    ),
                  if (review.explanation.trim().isNotEmpty) ...[
                    const SizedBox(height: NhSpace.s1),
                    Text(
                      review.explanation,
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: colors.onSurfaceVariant,
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
        ],
        const SizedBox(height: NhSpace.s3),
        OutlinedButton(onPressed: onRestart, child: const Text('Back to the quiz')),
      ],
    );
  }
}

class _Pill extends StatelessWidget {
  const _Pill({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    final tint = Theme.of(context).colorScheme.primary;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: NhSpace.s2, vertical: 3),
      decoration: BoxDecoration(
        color: tint.withValues(alpha: 0.12),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: tint,
              fontWeight: FontWeight.w700,
            ),
      ),
    );
  }
}
