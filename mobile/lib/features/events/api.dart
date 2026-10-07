import '../../core/api/api_client.dart';
import '../../core/api/paged_result.dart';
import '../../core/api/query_params.dart';

const eventCategories = <String>[
  'Community',
  'Cultural',
  'Sports',
  'Education',
  'Religious',
  'Business',
  'Exhibition',
  'Other',
];

const eventSorts = <String>['Soonest', 'Newest'];

class EventSummary {
  const EventSummary({
    required this.slug,
    required this.title,
    this.summary,
    required this.category,
    this.organiser,
    this.businessName,
    required this.districtName,
    required this.provinceName,
    this.venue,
    required this.startsAt,
    required this.endsAt,
    required this.isOngoing,
  });

  final String slug;
  final String title;
  final String? summary;
  final String category;
  final String? organiser;
  final String? businessName;
  final String districtName;
  final String provinceName;
  final String? venue;
  final String startsAt;
  final String endsAt;
  final bool isOngoing;

  factory EventSummary.fromJson(Map<String, dynamic> json) => EventSummary(
        slug: '${json['slug'] ?? ''}',
        title: '${json['title'] ?? ''}',
        summary: json['summary'] as String?,
        category: '${json['category'] ?? 'Other'}',
        organiser: json['organiser'] as String?,
        businessName: json['businessName'] as String?,
        districtName: '${json['districtName'] ?? ''}',
        provinceName: '${json['provinceName'] ?? ''}',
        venue: json['venue'] as String?,
        startsAt: '${json['startsAt'] ?? ''}',
        endsAt: '${json['endsAt'] ?? ''}',
        isOngoing: json['isOngoing'] as bool? ?? false,
      );
}

class EventDetail {
  const EventDetail({
    required this.slug,
    required this.title,
    this.summary,
    required this.category,
    this.organiser,
    this.businessName,
    this.districtName,
    this.provinceName,
    this.venue,
    required this.startsAt,
    required this.endsAt,
    required this.isOngoing,
    required this.description,
    this.businessSlug,
    this.registrationUrl,
    required this.capacity,
    required this.registrationCount,
    required this.remainingSpots,
    required this.ticketPrice,
  });

  final String slug;
  final String title;
  final String? summary;
  final String category;
  final String? organiser;
  final String? businessName;
  final String? districtName;
  final String? provinceName;
  final String? venue;
  final String startsAt;
  final String endsAt;
  final bool isOngoing;
  final String description;
  final String? businessSlug;
  final String? registrationUrl;
  final int capacity;
  final int registrationCount;
  final int remainingSpots;
  final num ticketPrice;

  factory EventDetail.fromJson(Map<String, dynamic> json) => EventDetail(
        slug: '${json['slug'] ?? ''}',
        title: '${json['title'] ?? ''}',
        summary: json['summary'] as String?,
        category: '${json['category'] ?? 'Other'}',
        organiser: json['organiser'] as String?,
        businessName: json['businessName'] as String?,
        districtName: json['districtName'] as String?,
        provinceName: json['provinceName'] as String?,
        venue: json['venue'] as String?,
        startsAt: '${json['startsAt'] ?? ''}',
        endsAt: '${json['endsAt'] ?? ''}',
        isOngoing: json['isOngoing'] as bool? ?? false,
        description: '${json['description'] ?? ''}',
        businessSlug: json['businessSlug'] as String?,
        registrationUrl: json['registrationUrl'] as String?,
        capacity: (json['capacity'] as num?)?.toInt() ?? 0,
        registrationCount: (json['registrationCount'] as num?)?.toInt() ?? 0,
        remainingSpots: (json['remainingSpots'] as num?)?.toInt() ?? 0,
        ticketPrice: json['ticketPrice'] as num? ?? 0,
      );
}

class EventsApi {
  EventsApi(this._client);

  final ApiClient _client;

  Future<PagedResult<EventSummary>> list({
    int page = 1,
    int pageSize = 24,
    String? search,
    String? province,
    String? district,
    String? category,
    String? sort,
  }) =>
      _client.getPaged<EventSummary>(
        '/events',
        query: queryParams(
          page: page,
          pageSize: pageSize,
          search: search,
          filters: <String, String?>{
            'province': province,
            'district': district,
            'category': category,
            'sort': sort,
          },
        ),
        fromJson: EventSummary.fromJson,
      );

  Future<EventDetail> detail(String slug) => _client.getObject<EventDetail>(
        '/events/$slug',
        fromJson: EventDetail.fromJson,
      );
}
