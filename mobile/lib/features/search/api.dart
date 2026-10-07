import '../../core/api/api_client.dart';

const searchModules = <String>[
  'All',
  'Students',
  'Jobs',
  'Businesses',
  'Deals',
  'Events',
  'Calculators',
];

class SearchItem {
  const SearchItem({
    required this.module,
    required this.slug,
    required this.title,
    this.summary,
    this.category,
    this.organisation,
    this.place,
    this.discountText,
    required this.isVerified,
  });

  final String module;
  final String slug;
  final String title;
  final String? summary;
  final String? category;
  final String? organisation;
  final String? place;
  final String? discountText;
  final bool isVerified;

  String get route {
    switch (module) {
      case 'Students':
        return '/students/resources/$slug';
      case 'Jobs':
        return '/jobs/$slug';
      case 'Businesses':
        return '/businesses/$slug';
      case 'Deals':
        return '/deals/$slug';
      case 'Events':
        return '/events/$slug';
      case 'Calculators':
        return '/calculators/$slug';
      default:
        return '';
    }
  }

  String get subtitle {
    final parts = <String>[
      if ((category ?? '').trim().isNotEmpty) category!,
      if ((organisation ?? '').trim().isNotEmpty) organisation!,
      if ((place ?? '').trim().isNotEmpty) place!,
      if ((discountText ?? '').trim().isNotEmpty) discountText!,
    ];
    return parts.join('  ·  ');
  }

  factory SearchItem.fromJson(Map<String, dynamic> json) => SearchItem(
        module: '${json['module'] ?? ''}',
        slug: '${json['slug'] ?? ''}',
        title: '${json['title'] ?? ''}',
        summary: json['summary'] as String?,
        category: json['category'] as String?,
        organisation: json['organisation'] as String?,
        place: json['place'] as String?,
        discountText: json['discountText'] as String?,
        isVerified: json['isVerified'] as bool? ?? false,
      );
}

class SearchGroup {
  const SearchGroup({
    required this.module,
    required this.label,
    required this.totalCount,
    required this.items,
  });

  final String module;
  final String label;
  final int totalCount;
  final List<SearchItem> items;

  factory SearchGroup.fromJson(Map<String, dynamic> json) => SearchGroup(
        module: '${json['module'] ?? ''}',
        label: '${json['label'] ?? ''}',
        totalCount: (json['totalCount'] as num?)?.toInt() ?? 0,
        items: json['items'] is List
            ? (json['items'] as List)
                .whereType<Map<String, dynamic>>()
                .map(SearchItem.fromJson)
                .toList()
            : <SearchItem>[],
      );
}

class SearchResult {
  const SearchResult({
    required this.query,
    required this.page,
    required this.pageSize,
    required this.totalCount,
    required this.totalPages,
    required this.hasPrevious,
    required this.hasNext,
    required this.truncated,
    required this.groups,
  });

  final String query;
  final int page;
  final int pageSize;
  final int totalCount;
  final int totalPages;
  final bool hasPrevious;
  final bool hasNext;
  final bool truncated;
  final List<SearchGroup> groups;

  bool get isEmpty => totalCount == 0;

  factory SearchResult.fromJson(Map<String, dynamic> json) => SearchResult(
        query: '${json['query'] ?? ''}',
        page: (json['page'] as num?)?.toInt() ?? 1,
        pageSize: (json['pageSize'] as num?)?.toInt() ?? 20,
        totalCount: (json['totalCount'] as num?)?.toInt() ?? 0,
        totalPages: (json['totalPages'] as num?)?.toInt() ?? 0,
        hasPrevious: json['hasPrevious'] as bool? ?? false,
        hasNext: json['hasNext'] as bool? ?? false,
        truncated: json['truncated'] as bool? ?? false,
        groups: json['groups'] is List
            ? (json['groups'] as List)
                .whereType<Map<String, dynamic>>()
                .map(SearchGroup.fromJson)
                .toList()
            : <SearchGroup>[],
      );
}

class SearchApi {
  SearchApi(this._client);

  final ApiClient _client;

  Future<SearchResult> search({
    required String query,
    int page = 1,
    int pageSize = 20,
    String module = 'All',
  }) =>
      _client.getObject<SearchResult>(
        '/search',
        query: <String, dynamic>{
          'q': query,
          'page': page,
          'pageSize': pageSize,
          'module': module,
        },
        fromJson: SearchResult.fromJson,
      );
}
