import '../../core/api/api_client.dart';
import '../../core/api/paged_result.dart';
import '../../core/api/query_params.dart';

const jobCategories = <String>[
  'InformationTechnology',
  'Teaching',
  'Healthcare',
  'Administration',
  'Finance',
  'Sales',
  'Engineering',
  'Construction',
  'Hospitality',
  'Agriculture',
  'Driving',
  'Security',
  'Government',
  'NGO',
  'Other',
];

const jobEmploymentTypes = <String>['FullTime', 'PartTime', 'Contract', 'Internship'];

const jobWorkModes = <String>['OnSite', 'Remote', 'Hybrid'];

const jobSorts = <String>['Newest', 'Deadline'];

class JobSummary {
  const JobSummary({
    required this.slug,
    required this.title,
    required this.organization,
    required this.category,
    required this.employmentType,
    required this.workMode,
    required this.districtName,
    required this.provinceName,
    required this.applicationDeadline,
    required this.status,
    required this.createdAt,
  });

  final String slug;
  final String title;
  final String organization;
  final String category;
  final String employmentType;
  final String workMode;
  final String districtName;
  final String provinceName;
  final String applicationDeadline;
  final String status;
  final String createdAt;

  factory JobSummary.fromJson(Map<String, dynamic> json) => JobSummary(
        slug: '${json['slug'] ?? ''}',
        title: '${json['title'] ?? ''}',
        organization: '${json['organization'] ?? ''}',
        category: '${json['category'] ?? ''}',
        employmentType: '${json['employmentType'] ?? ''}',
        workMode: '${json['workMode'] ?? ''}',
        districtName: '${json['districtName'] ?? ''}',
        provinceName: '${json['provinceName'] ?? ''}',
        applicationDeadline: '${json['applicationDeadline'] ?? ''}',
        status: '${json['status'] ?? 'Open'}',
        createdAt: '${json['createdAt'] ?? ''}',
      );
}

class JobDetail {
  const JobDetail({
    required this.id,
    required this.slug,
    required this.title,
    required this.organization,
    required this.category,
    required this.employmentType,
    required this.workMode,
    required this.districtName,
    required this.provinceName,
    required this.applicationDeadline,
    required this.status,
    required this.description,
    required this.applicationUrl,
  });

  final String id;
  final String slug;
  final String title;
  final String organization;
  final String category;
  final String employmentType;
  final String workMode;
  final String districtName;
  final String provinceName;
  final String applicationDeadline;
  final String status;
  final String description;
  final String applicationUrl;

  factory JobDetail.fromJson(Map<String, dynamic> json) => JobDetail(
        id: '${json['id'] ?? ''}',
        slug: '${json['slug'] ?? ''}',
        title: '${json['title'] ?? ''}',
        organization: '${json['organization'] ?? ''}',
        category: '${json['category'] ?? ''}',
        employmentType: '${json['employmentType'] ?? ''}',
        workMode: '${json['workMode'] ?? ''}',
        districtName: '${json['districtName'] ?? ''}',
        provinceName: '${json['provinceName'] ?? ''}',
        applicationDeadline: '${json['applicationDeadline'] ?? ''}',
        status: '${json['status'] ?? 'Open'}',
        description: '${json['description'] ?? ''}',
        applicationUrl: '${json['applicationUrl'] ?? ''}',
      );
}

class JobsApi {
  JobsApi(this._client);

  final ApiClient _client;

  Future<PagedResult<JobSummary>> list({
    int page = 1,
    int pageSize = 24,
    String? search,
    String? province,
    String? district,
    String? category,
    String? employmentType,
    String? workMode,
    String? sort,
  }) =>
      _client.getPaged<JobSummary>(
        '/jobs',
        query: queryParams(
          page: page,
          pageSize: pageSize,
          search: search,
          filters: <String, String?>{
            'province': province,
            'district': district,
            'category': category,
            'employmentType': employmentType,
            'workMode': workMode,
            'sort': sort,
          },
        ),
        fromJson: JobSummary.fromJson,
      );

  Future<JobDetail> detail(String slug) => _client.getObject<JobDetail>(
        '/jobs/$slug',
        fromJson: JobDetail.fromJson,
      );
}
