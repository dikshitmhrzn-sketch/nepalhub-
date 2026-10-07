import '../../core/api/api_client.dart';

const reportTargetLabels = <String, String>{
  'Job': 'Job',
  'Business': 'Business',
  'Deal': 'Offer',
  'Event': 'Event',
  'StudentResource': 'Student resource',
};

const reportReasons = <String, String>{
  'Spam': 'Spam or repeated posting',
  'Misleading': 'Misleading or false',
  'Inappropriate': 'Inappropriate content',
  'Broken': 'Broken link or details',
  'Other': 'Something else',
};

class Report {
  const Report({
    required this.id,
    required this.targetType,
    required this.targetId,
    required this.reason,
    required this.status,
    required this.createdAt,
  });

  final String id;
  final String targetType;
  final String targetId;
  final String reason;
  final String status;
  final String createdAt;

  factory Report.fromJson(Map<String, dynamic> json) => Report(
        id: '${json['id'] ?? ''}',
        targetType: '${json['targetType'] ?? ''}',
        targetId: '${json['targetId'] ?? ''}',
        reason: '${json['reason'] ?? ''}',
        status: '${json['status'] ?? 'Open'}',
        createdAt: '${json['createdAt'] ?? ''}',
      );
}

class ReportsApi {
  ReportsApi(this._client);

  final ApiClient _client;

  Future<Report> create({
    required String targetType,
    required String targetId,
    required String reason,
    String? note,
  }) =>
      _client.postObject<Report>(
        '/reports',
        body: <String, Object?>{
          'targetType': targetType,
          'targetId': targetId,
          'reason': reason,
          if ((note ?? '').trim().isNotEmpty) 'note': note!.trim(),
        },
        fromJson: Report.fromJson,
      );
}
