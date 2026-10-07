import '../../core/api/api_client.dart';
import '../../core/api/paged_result.dart';
import '../../core/api/query_params.dart';

const businessCategories = <String>[
  'Restaurants',
  'Cafes',
  'HotelsAndAccommodation',
  'GymsAndFitness',
  'SalonsAndBeauty',
  'EducationAndTuition',
  'RepairAndMaintenance',
  'Shops',
  'InformationTechnology',
  'ProfessionalServices',
  'HealthcareServices',
  'Other',
];

const businessSorts = <String>['Name', 'Newest'];

class BusinessSummary {
  const BusinessSummary({
    required this.slug,
    required this.name,
    required this.category,
    required this.districtName,
    required this.provinceName,
    this.address,
    this.phone,
    required this.isVerified,
    required this.isOpenNow,
  });

  final String slug;
  final String name;
  final String category;
  final String districtName;
  final String provinceName;
  final String? address;
  final String? phone;
  final bool isVerified;
  final bool isOpenNow;

  factory BusinessSummary.fromJson(Map<String, dynamic> json) => BusinessSummary(
        slug: '${json['slug'] ?? ''}',
        name: '${json['name'] ?? ''}',
        category: '${json['category'] ?? ''}',
        districtName: '${json['districtName'] ?? ''}',
        provinceName: '${json['provinceName'] ?? ''}',
        address: json['address'] as String?,
        phone: json['phone'] as String?,
        isVerified: json['isVerified'] as bool? ?? false,
        isOpenNow: json['isOpenNow'] as bool? ?? false,
      );
}

class OpeningHour {
  const OpeningHour({
    required this.dayOfWeek,
    required this.isClosed,
    this.opensAt,
    this.closesAt,
  });

  final String dayOfWeek;
  final bool isClosed;
  final String? opensAt;
  final String? closesAt;

  factory OpeningHour.fromJson(Map<String, dynamic> json) => OpeningHour(
        dayOfWeek: '${json['dayOfWeek'] ?? ''}',
        isClosed: json['isClosed'] as bool? ?? false,
        opensAt: json['opensAt'] as String?,
        closesAt: json['closesAt'] as String?,
      );
}

class BusinessDetail {
  const BusinessDetail({
    required this.id,
    required this.slug,
    required this.name,
    required this.category,
    required this.districtName,
    required this.provinceName,
    this.address,
    this.phone,
    this.email,
    this.website,
    required this.isVerified,
    required this.isOpenNow,
    required this.openingHours,
    required this.isOwner,
    required this.description,
  });

  final String id;
  final String slug;
  final String name;
  final String category;
  final String districtName;
  final String provinceName;
  final String? address;
  final String? phone;
  final String? email;
  final String? website;
  final bool isVerified;
  final bool isOpenNow;
  final List<OpeningHour> openingHours;
  final bool isOwner;
  final String description;

  factory BusinessDetail.fromJson(Map<String, dynamic> json) => BusinessDetail(
        id: '${json['id'] ?? ''}',
        slug: '${json['slug'] ?? ''}',
        name: '${json['name'] ?? ''}',
        category: '${json['category'] ?? ''}',
        districtName: '${json['districtName'] ?? ''}',
        provinceName: '${json['provinceName'] ?? ''}',
        address: json['address'] as String?,
        phone: json['phone'] as String?,
        email: json['email'] as String?,
        website: json['website'] as String?,
        isVerified: json['isVerified'] as bool? ?? false,
        isOpenNow: json['isOpenNow'] as bool? ?? false,
        openingHours: json['openingHours'] is List
            ? (json['openingHours'] as List)
                .whereType<Map<String, dynamic>>()
                .map(OpeningHour.fromJson)
                .toList()
            : <OpeningHour>[],
        isOwner: json['isOwner'] as bool? ?? false,
        description: '${json['description'] ?? ''}',
      );
}

class BusinessesApi {
  BusinessesApi(this._client);

  final ApiClient _client;

  Future<PagedResult<BusinessSummary>> list({
    int page = 1,
    int pageSize = 24,
    String? search,
    String? province,
    String? district,
    String? category,
    String? verified,
    String? sort,
  }) =>
      _client.getPaged<BusinessSummary>(
        '/businesses',
        query: queryParams(
          page: page,
          pageSize: pageSize,
          search: search,
          filters: <String, String?>{
            'province': province,
            'district': district,
            'category': category,
            'verified': verified,
            'sort': sort,
          },
        ),
        fromJson: BusinessSummary.fromJson,
      );

  Future<List<BusinessSummary>> featured() => _client.getList<BusinessSummary>(
        '/businesses/featured',
        fromJson: BusinessSummary.fromJson,
      );

  Future<BusinessDetail> detail(String slug) => _client.getObject<BusinessDetail>(
        '/businesses/$slug',
        fromJson: BusinessDetail.fromJson,
      );
}
