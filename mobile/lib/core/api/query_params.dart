Map<String, dynamic> queryParams({
  int? page,
  int? pageSize,
  String? search,
  Map<String, String?> filters = const <String, String?>{},
}) {
  final query = <String, dynamic>{};
  if (page != null) {
    query['page'] = page;
  }
  if (pageSize != null) {
    query['pageSize'] = pageSize;
  }
  final term = search?.trim() ?? '';
  if (term.isNotEmpty) {
    query['search'] = term;
  }
  for (final entry in filters.entries) {
    final value = entry.value?.trim() ?? '';
    if (value.isNotEmpty) {
      query[entry.key] = value;
    }
  }
  return query;
}
