import '../../core/api/api_client.dart';

const quizCategories = <String, String>{
  'Nepal': 'Nepal',
  'GeneralKnowledge': 'General knowledge',
  'Science': 'Science',
  'Technology': 'Technology',
  'History': 'History',
  'Geography': 'Geography',
};

class QuizOption {
  const QuizOption({required this.id, required this.position, required this.text});

  final String id;
  final int position;
  final String text;

  factory QuizOption.fromJson(Map<String, dynamic> json) => QuizOption(
        id: '${json['id'] ?? ''}',
        position: (json['position'] as num?)?.toInt() ?? 0,
        text: '${json['text'] ?? ''}',
      );
}

class QuizQuestion {
  const QuizQuestion({
    required this.id,
    required this.position,
    required this.text,
    required this.category,
    required this.options,
  });

  final String id;
  final int position;
  final String text;
  final String category;
  final List<QuizOption> options;

  factory QuizQuestion.fromJson(Map<String, dynamic> json) => QuizQuestion(
        id: '${json['id'] ?? ''}',
        position: (json['position'] as num?)?.toInt() ?? 0,
        text: '${json['text'] ?? ''}',
        category: '${json['category'] ?? 'Nepal'}',
        options: json['options'] is List
            ? (json['options'] as List)
                .whereType<Map<String, dynamic>>()
                .map(QuizOption.fromJson)
                .toList()
            : <QuizOption>[],
      );
}

class DailyQuiz {
  const DailyQuiz({
    required this.id,
    required this.quizDate,
    required this.questionCount,
    required this.maxXpAvailable,
    required this.alreadyCompleted,
    required this.questions,
  });

  final String id;
  final String quizDate;
  final int questionCount;
  final int maxXpAvailable;
  final bool alreadyCompleted;
  final List<QuizQuestion> questions;

  factory DailyQuiz.fromJson(Map<String, dynamic> json) => DailyQuiz(
        id: '${json['id'] ?? ''}',
        quizDate: '${json['quizDate'] ?? ''}',
        questionCount: (json['questionCount'] as num?)?.toInt() ?? 0,
        maxXpAvailable: (json['maxXpAvailable'] as num?)?.toInt() ?? 0,
        alreadyCompleted: json['alreadyCompleted'] as bool? ?? false,
        questions: json['questions'] is List
            ? (json['questions'] as List)
                .whereType<Map<String, dynamic>>()
                .map(QuizQuestion.fromJson)
                .toList()
            : <QuizQuestion>[],
      );
}

class QuizAnswer {
  const QuizAnswer({required this.questionId, required this.optionId});

  final String questionId;
  final String optionId;

  Map<String, Object?> toJson() =>
      <String, Object?>{'questionId': questionId, 'optionId': optionId};
}

class QuizReview {
  const QuizReview({
    required this.questionId,
    required this.text,
    required this.category,
    required this.selectedOptionId,
    required this.selectedOptionText,
    required this.correctOptionId,
    required this.correctOptionText,
    required this.isCorrect,
    required this.explanation,
  });

  final String questionId;
  final String text;
  final String category;
  final String selectedOptionId;
  final String selectedOptionText;
  final String correctOptionId;
  final String correctOptionText;
  final bool isCorrect;
  final String explanation;

  factory QuizReview.fromJson(Map<String, dynamic> json) => QuizReview(
        questionId: '${json['questionId'] ?? ''}',
        text: '${json['text'] ?? ''}',
        category: '${json['category'] ?? 'Nepal'}',
        selectedOptionId: '${json['selectedOptionId'] ?? ''}',
        selectedOptionText: '${json['selectedOptionText'] ?? ''}',
        correctOptionId: '${json['correctOptionId'] ?? ''}',
        correctOptionText: '${json['correctOptionText'] ?? ''}',
        isCorrect: json['isCorrect'] as bool? ?? false,
        explanation: '${json['explanation'] ?? ''}',
      );
}

class QuizProgression {
  const QuizProgression({
    required this.recorded,
    required this.xpAwarded,
    required this.totalXp,
    required this.level,
    required this.streakCount,
  });

  final bool recorded;
  final int xpAwarded;
  final int totalXp;
  final int level;
  final int streakCount;

  factory QuizProgression.fromJson(Map<String, dynamic> json) => QuizProgression(
        recorded: json['recorded'] as bool? ?? false,
        xpAwarded: (json['xpAwarded'] as num?)?.toInt() ?? 0,
        totalXp: (json['totalXp'] as num?)?.toInt() ?? 0,
        level: (json['level'] as num?)?.toInt() ?? 0,
        streakCount: (json['streakCount'] as num?)?.toInt() ?? 0,
      );
}

class QuizResult {
  const QuizResult({
    required this.attemptId,
    required this.quizDate,
    required this.score,
    required this.maxScore,
    required this.correctCount,
    this.progression,
    required this.reviews,
  });

  final String attemptId;
  final String quizDate;
  final int score;
  final int maxScore;
  final int correctCount;
  final QuizProgression? progression;
  final List<QuizReview> reviews;

  factory QuizResult.fromJson(Map<String, dynamic> json) => QuizResult(
        attemptId: '${json['attemptId'] ?? ''}',
        quizDate: '${json['quizDate'] ?? ''}',
        score: (json['score'] as num?)?.toInt() ?? 0,
        maxScore: (json['maxScore'] as num?)?.toInt() ?? 0,
        correctCount: (json['correctCount'] as num?)?.toInt() ?? 0,
        progression: json['progression'] is Map<String, dynamic>
            ? QuizProgression.fromJson(json['progression'] as Map<String, dynamic>)
            : null,
        reviews: json['reviews'] is List
            ? (json['reviews'] as List)
                .whereType<Map<String, dynamic>>()
                .map(QuizReview.fromJson)
                .toList()
            : <QuizReview>[],
      );
}

class QuizApi {
  QuizApi(this._client);

  final ApiClient _client;

  Future<DailyQuiz> daily() => _client.getObject<DailyQuiz>(
        '/quiz/daily',
        fromJson: DailyQuiz.fromJson,
      );

  Future<QuizResult> submit(List<QuizAnswer> answers) =>
      _client.postObject<QuizResult>(
        '/quiz/daily/submit',
        body: <String, Object?>{
          'answers': answers.map((answer) => answer.toJson()).toList(),
        },
        fromJson: QuizResult.fromJson,
      );
}
