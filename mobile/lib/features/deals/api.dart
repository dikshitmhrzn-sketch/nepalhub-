import '../../core/api/api_client.dart';
import '../../core/api/paged_result.dart';
import '../../core/api/query_params.dart';

const dealSorts = <String>['EndingSoon', 'Newest'];

class DealSummary {
  const DealSummary({
    required this.slug,
    required this.title,
    this.summary,
    required this.discountText,
    this.businessName,
    this.businessSlug,
    required this.districtName,
    required this.provinceName,
    required this.startsOn,
    required this.endsOn,
    required this.daysRemaining,
  });

  final String slug;
  final String title;
  final String? summary;
  final String discountText;
  final String? businessName;
  final String? businessSlug;
  final String districtName;
  final String provinceName;
  final String startsOn;
  final String endsOn;
  final int daysRemaining;

  factory DealSummary.fromJson(Map<String, dynamic> json) => DealSummary(
        slug: '${json['slug'] ?? ''}',
        title: '${json['title'] ?? ''}',
        summary: json['summary'] as String?,
        discountText: '${json['discountText'] ?? ''}',
        businessName: json['businessName'] as String?,
        businessSlug: json['businessSlug'] as String?,
        districtName: '${json['districtName'] ?? ''}',
        provinceName: '${json['provinceName'] ?? ''}',
        startsOn: '${json['startsOn'] ?? ''}',
        endsOn: '${json['endsOn'] ?? ''}',
        daysRemaining: (json['daysRemaining'] as num?)?.toInt() ?? 0,
      );
}

class DealDetail {
  const DealDetail({
    required this.slug,
    required this.title,
    this.summary,
    required this.discountText,
    this.businessName,
    this.businessSlug,
    this.districtName,
    this.provinceName,
    required this.description,
    this.redemptionUrl,
    required this.startsOn,
    required this.endsOn,
    required this.daysRemaining,
    required this.isRunning,
  });

  final String slug;
  final String title;
  final String? summary;
  final String discountText;
  final String? businessName;
  final String? businessSlug;
  final String? districtName;
  final String? provinceName;
  final String description;
  final String? redemptionUrl;
  final String startsOn;
  final String endsOn;
  final int daysRemaining;
  final bool isRunning;

  factory DealDetail.fromJson(Map<String, dynamic> json) => DealDetail(
        slug: '${json['slug'] ?? ''}',
        title: '${json['title'] ?? ''}',
        summary: json['summary'] as String?,
        discountText: '${json['discountText'] ?? ''}',
        businessName: json['businessName'] as String?,
        businessSlug: json['businessSlug'] as String?,
        districtName: json['districtName'] as String?,
        provinceName: json['provinceName'] as String?,
        description: '${json['description'] ?? ''}',
        redemptionUrl: json['redemptionUrl'] as String?,
        startsOn: '${json['startsOn'] ?? ''}',
        endsOn: '${json['endsOn'] ?? ''}',
        daysRemaining: (json['daysRemaining'] as num?)?.toInt() ?? 0,
        isRunning: json['isRunning'] as bool? ?? false,
      );
}

class DealsApi {
  DealsApi(this._client);

  final ApiClient _client;

  Future<PagedResult<DealSummary>> list({
    int page = 1,
    int pageSize = 24,
    String? search,
    String? province,
    String? district,
    String? sort,
  }) =>
      _client.getPaged<DealSummary>(
        '/deals',
        query: queryParams(
          page: page,
          pageSize: pageSize,
          search: search,
          filters: <String, String?>{
            'province': province,
            'district': district,
            'sort': sort,
          },
        ),
        fromJson: DealSummary.fromJson,
      );

  Future<DealDetail> detail(String slug) => _client.getObject<DealDetail>(
        '/deals/$slug',
        fromJson: DealDetail.fromJson,
      );
}
