import '../../core/api/api_client.dart';
import '../../core/api/paged_result.dart';
import '../../core/api/query_params.dart';

const studentTypes = <String>['Note', 'QuestionPaper', 'Scholarship'];

const studentCategories = <String>[
  'General',
  'Mathematics',
  'Science',
  'English',
  'Nepali',
  'SocialStudies',
  'ComputerScience',
  'Management',
];

const studentLevels = <String>[
  'General',
  'Grade6to8',
  'Grade9to10',
  'Grade11to12',
  'PlusTwo',
  'Bachelors',
  'Competitive',
];

const studentSorts = <String>['Newest', 'Expiring', 'Title'];

const studentLevelLabels = <String, String>{
  'General': 'General',
  'Grade6to8': 'Grade 6-8',
  'Grade9to10': 'Grade 9-10',
  'Grade11to12': 'Grade 11-12',
  'PlusTwo': 'Plus Two',
  'Bachelors': 'Bachelors',
  'Competitive': 'Competitive',
};

class ResourceSummary {
  const ResourceSummary({
    required this.slug,
    required this.type,
    required this.category,
    required this.level,
    required this.title,
    required this.summary,
    required this.hasExternalUrl,
    this.expiresOn,
    required this.updatedAt,
  });

  final String slug;
  final String type;
  final String category;
  final String level;
  final String title;
  final String summary;
  final bool hasExternalUrl;
  final String? expiresOn;
  final String updatedAt;

  factory ResourceSummary.fromJson(Map<String, dynamic> json) => ResourceSummary(
        slug: '${json['slug'] ?? ''}',
        type: '${json['type'] ?? 'Note'}',
        category: '${json['category'] ?? ''}',
        level: '${json['level'] ?? ''}',
        title: '${json['title'] ?? ''}',
        summary: '${json['summary'] ?? ''}',
        hasExternalUrl: json['hasExternalUrl'] as bool? ?? false,
        expiresOn: json['expiresOn'] as String?,
        updatedAt: '${json['updatedAt'] ?? ''}',
      );
}

class ResourceDetail {
  const ResourceDetail({
    required this.slug,
    required this.type,
    required this.category,
    required this.level,
    required this.title,
    required this.summary,
    required this.body,
    this.externalUrl,
    this.academicYear,
    this.provider,
    this.awardAmountNpr,
    this.eligibility,
    this.expiresOn,
    required this.isExpired,
  });

  final String slug;
  final String type;
  final String category;
  final String level;
  final String title;
  final String summary;
  final String body;
  final String? externalUrl;
  final int? academicYear;
  final String? provider;
  final num? awardAmountNpr;
  final String? eligibility;
  final String? expiresOn;
  final bool isExpired;

  factory ResourceDetail.fromJson(Map<String, dynamic> json) => ResourceDetail(
        slug: '${json['slug'] ?? ''}',
        type: '${json['type'] ?? 'Note'}',
        category: '${json['category'] ?? ''}',
        level: '${json['level'] ?? ''}',
        title: '${json['title'] ?? ''}',
        summary: '${json['summary'] ?? ''}',
        body: '${json['body'] ?? ''}',
        externalUrl: json['externalUrl'] as String?,
        academicYear: (json['academicYear'] as num?)?.toInt(),
        provider: json['provider'] as String?,
        awardAmountNpr: json['awardAmountNpr'] as num?,
        eligibility: json['eligibility'] as String?,
        expiresOn: json['expiresOn'] as String?,
        isExpired: json['isExpired'] as bool? ?? false,
      );
}

class StudentsApi {
  StudentsApi(this._client);

  final ApiClient _client;

  Future<PagedResult<ResourceSummary>> list({
    int page = 1,
    int pageSize = 24,
    String? search,
    String? type,
    String? category,
    String? level,
    String? sort,
  }) =>
      _client.getPaged<ResourceSummary>(
        '/students/resources',
        query: queryParams(
          page: page,
          pageSize: pageSize,
          search: search,
          filters: <String, String?>{
            'type': type,
            'category': category,
            'level': level,
            'sort': sort,
          },
        ),
        fromJson: ResourceSummary.fromJson,
      );

  Future<ResourceDetail> detail(String slug) => _client.getObject<ResourceDetail>(
        '/students/resources/$slug',
        fromJson: ResourceDetail.fromJson,
      );
}
