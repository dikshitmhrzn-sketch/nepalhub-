import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../api/paged_result.dart';
import '../theme/tokens.dart';
import '../../features/reference/api.dart';
import 'filter_sheet.dart';
import 'paged_list.dart';
import 'search_field.dart';

typedef ModuleLoader<T> = Future<PagedResult<T>> Function(
  int page,
  String? search,
  Map<String, String?> filters,
);

class ModuleListScaffold<T> extends StatefulWidget {
  const ModuleListScaffold({
    super.key,
    required this.title,
    required this.loader,
    required this.itemBuilder,
    this.filterTitle = 'Filters',
    this.filterFields = const <FilterField>[],
    this.location = false,
    this.searchHint = 'Search',
    this.emptyTitle = 'Nothing here yet',
    this.emptyDescription,
  });

  final String title;
  final ModuleLoader<T> loader;
  final PagedItemBuilder<T> itemBuilder;
  final String filterTitle;
  final List<FilterField> filterFields;
  final bool location;
  final String searchHint;
  final String emptyTitle;
  final String? emptyDescription;

  @override
  State<ModuleListScaffold<T>> createState() => _ModuleListScaffoldState<T>();
}

class _ModuleListScaffoldState<T> extends State<ModuleListScaffold<T>> {
  String? _search;
  final Map<String, String?> _filters = <String, String?>{};
  int _reloadKey = 0;

  int get _activeCount => widget.filterFields
      .where((field) => (_filters[field.key] ?? '').isNotEmpty)
      .length;

  void _reload() => setState(() => _reloadKey++);

  void _onSearch(String value) {
    _search = value.isEmpty ? null : value;
    _reload();
  }

  Future<void> _openFilters() async {
    final applied = await openFilterSheet(
      context,
      title: widget.filterTitle,
      fields: widget.filterFields,
      values: Map<String, String?>.from(_filters),
    );
    if (applied == null || !mounted) {
      return;
    }
    setState(() {
      _filters
        ..clear()
        ..addAll(applied);
      _reloadKey++;
    });
  }

  void _removeFilter(String key) {
    setState(() {
      _filters.remove(key);
      _reloadKey++;
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(widget.title)),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(
              NhSpace.s4,
              NhSpace.s3,
              NhSpace.s4,
              NhSpace.s2,
            ),
            child: SearchField(hint: widget.searchHint, onChanged: _onSearch),
          ),
          if (widget.location)
            _LocationRow(
              province: _filters['province'],
              district: _filters['district'],
              onChanged: (province, district) {
                setState(() {
                  _filters['province'] = province;
                  _filters['district'] = district;
                  _reloadKey++;
                });
              },
            ),
          if (widget.filterFields.isNotEmpty)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: NhSpace.s4),
              child: Align(
                alignment: Alignment.centerLeft,
                child: OutlinedButton.icon(
                  onPressed: _openFilters,
                  icon: const Icon(Icons.tune_rounded, size: 18),
                  label: Text(
                    _activeCount == 0
                        ? widget.filterTitle
                        : '${widget.filterTitle} ($_activeCount)',
                  ),
                ),
              ),
            ),
          ActiveFilterChips(
            fields: widget.filterFields,
            values: _filters,
            onRemove: _removeFilter,
          ),
          Expanded(
            child: PagedListView<T>(
              reloadKey: _reloadKey,
              emptyTitle: widget.emptyTitle,
              emptyDescription: widget.emptyDescription,
              load: (page) => widget.loader(page, _search, _filters),
              itemBuilder: widget.itemBuilder,
            ),
          ),
        ],
      ),
    );
  }
}

class _LocationRow extends StatefulWidget {
  const _LocationRow({
    required this.province,
    required this.district,
    required this.onChanged,
  });

  final String? province;
  final String? district;
  final void Function(String? province, String? district) onChanged;

  @override
  State<_LocationRow> createState() => _LocationRowState();
}

class _LocationRowState extends State<_LocationRow> {
  List<PlaceRef>? _provinces;
  List<PlaceRef>? _districts;
  String? _districtsFor;

  @override
  void initState() {
    super.initState();
    _loadProvinces();
  }

  @override
  void didUpdateWidget(covariant _LocationRow oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.province != widget.province) {
      _loadDistricts();
    }
  }

  Future<void> _loadProvinces() async {
    final client = context.read<ApiClient>();
    try {
      final provinces = await ReferenceApi(client).provinces();
      if (!mounted) {
        return;
      }
      setState(() => _provinces = provinces);
    } catch (_) {
      if (!mounted) {
        return;
      }
      setState(() => _provinces = <PlaceRef>[]);
    }
  }

  Future<void> _loadDistricts() async {
    final province = widget.province;
    if (province == null || province == _districtsFor) {
      return;
    }
    final client = context.read<ApiClient>();
    try {
      final districts = await ReferenceApi(client).districts(province);
      if (!mounted || widget.province != province) {
        return;
      }
      setState(() {
        _districts = districts;
        _districtsFor = province;
      });
    } catch (_) {
      if (!mounted || widget.province != province) {
        return;
      }
      setState(() {
        _districts = <PlaceRef>[];
        _districtsFor = province;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final provinces = _provinces;
    if (provinces == null || provinces.isEmpty) {
      return const SizedBox.shrink();
    }
    final theme = Theme.of(context);
    final province = widget.province;
    return Padding(
      padding: const EdgeInsets.fromLTRB(NhSpace.s4, NhSpace.s2, NhSpace.s4, 0),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _chipRow(
            theme,
            'Province',
            <(String?, String)>[
              (null, 'Anywhere'),
              for (final place in provinces) (place.code, place.name),
            ],
            province,
            (value) => widget.onChanged(value, null),
          ),
          if (province != null) ...[
            const SizedBox(height: NhSpace.s1),
            if (_districtsFor != province)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: NhSpace.s1),
                child: LinearProgressIndicator(minHeight: 2),
              )
            else
              _chipRow(
                theme,
                'District',
                <(String?, String)>[
                  (null, 'Anywhere'),
                  for (final place in _districts ?? const <PlaceRef>[])
                    (place.code, place.name),
                ],
                widget.district,
                (value) => widget.onChanged(province, value),
              ),
          ],
        ],
      ),
    );
  }

  Widget _chipRow(
    ThemeData theme,
    String label,
    List<(String?, String)> options,
    String? selected,
    ValueChanged<String?> onSelected,
  ) =>
      Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: theme.textTheme.labelMedium?.copyWith(
              color: theme.colorScheme.onSurfaceVariant,
            ),
          ),
          const SizedBox(height: NhSpace.s1),
          Wrap(
            spacing: NhSpace.s2,
            runSpacing: NhSpace.s1,
            children: [
              for (final option in options)
                ChoiceChip(
                  label: Text(option.$2),
                  selected: selected == option.$1,
                  onSelected: (_) => onSelected(option.$1),
                ),
            ],
          ),
        ],
      );
}
