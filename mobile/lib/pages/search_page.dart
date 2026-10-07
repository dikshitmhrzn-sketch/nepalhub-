import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_client.dart';
import '../core/api/api_exception.dart';
import '../core/theme/tokens.dart';
import '../core/widgets/common.dart';
import '../core/widgets/search_field.dart';
import '../core/widgets/state_message.dart';
import '../features/search/api.dart';

class SearchPage extends StatefulWidget {
  const SearchPage({super.key});

  @override
  State<SearchPage> createState() => _SearchPageState();
}

class _SearchPageState extends State<SearchPage> {
  String? _query;
  String _module = 'All';
  int _page = 1;
  SearchResult? _result;
  Object? _error;
  bool _loading = false;
  int _generation = 0;

  bool get _ready => (_query ?? '').length >= 2;

  void _onSearch(String value) {
    _query = value.isEmpty ? null : value;
    _page = 1;
    _run();
  }

  void _selectModule(String module) {
    setState(() {
      _module = module;
      _page = 1;
    });
    _run();
  }

  Future<void> _run() async {
    final query = _query;
    if (query == null || query.length < 2) {
      setState(() {
        _result = null;
        _error = null;
        _loading = false;
      });
      return;
    }
    final generation = ++_generation;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final api = SearchApi(context.read<ApiClient>());
      final result = await api.search(
        query: query,
        page: _page,
        module: _module,
      );
      if (!mounted || generation != _generation) {
        return;
      }
      setState(() {
        _result = result;
        _page = result.page;
        _loading = false;
      });
    } catch (error) {
      if (!mounted || generation != _generation) {
        return;
      }
      setState(() {
        _error = error;
        _loading = false;
      });
    }
  }

  void _goToPage(int page) {
    _page = page;
    _run();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    return Scaffold(
      appBar: AppBar(title: const Text('Search')),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(
              NhSpace.s4,
              NhSpace.s3,
              NhSpace.s4,
              NhSpace.s2,
            ),
            child: SearchField(hint: 'Search Nepal Hub', onChanged: _onSearch),
          ),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: NhSpace.s4),
            child: Align(
              alignment: Alignment.centerLeft,
              child: SizedBox(
                height: 40,
                child: ListView.separated(
                  scrollDirection: Axis.horizontal,
                  itemCount: searchModules.length,
                  separatorBuilder: (context, index) =>
                      const SizedBox(width: NhSpace.s2),
                  itemBuilder: (context, index) {
                    final module = searchModules[index];
                    return ChoiceChip(
                      label: Text(module == 'All' ? 'Everything' : module),
                      selected: _module == module,
                      onSelected: (_) => _selectModule(module),
                    );
                  },
                ),
              ),
            ),
          ),
          Expanded(child: _buildBody(theme, colors)),
        ],
      ),
    );
  }

  Widget _buildBody(ThemeData theme, ColorScheme colors) {
    if (!_ready) {
      return const StateMessage(
        tone: StateMessageTone.empty,
        title: 'Search everything in one place',
        description:
            'Find businesses, jobs, offers, events, resources and calculators.',
      );
    }
    if (_loading) {
      return const StateMessage(tone: StateMessageTone.loading, title: 'Searching');
    }
    if (_error != null) {
      final apiError = _error is ApiException ? _error as ApiException : null;
      return StateMessage(
        tone: StateMessageTone.error,
        title: apiError == null ? 'Something went wrong' : ApiMessage.title(apiError),
        description: apiError == null ? '$_error' : ApiMessage.message(apiError),
        actionLabel: 'Try again',
        onAction: _run,
      );
    }
    final result = _result;
    if (result == null) {
      return const StateMessage(
        tone: StateMessageTone.empty,
        title: 'Type at least two characters',
      );
    }
    if (result.isEmpty) {
      return StateMessage(
        tone: StateMessageTone.empty,
        title: 'No results for "${result.query}"',
        description: 'Try a shorter search or a different module.',
      );
    }
    return ListView(
      padding: const EdgeInsets.only(bottom: NhSpace.s4),
      children: [
        Padding(
          padding: const EdgeInsets.fromLTRB(
            NhSpace.s4,
            NhSpace.s3,
            NhSpace.s4,
            NhSpace.s1,
          ),
          child: Text(
            '${result.totalCount} result${result.totalCount == 1 ? '' : 's'}'
            '${result.truncated ? ' (showing the closest matches)' : ''}',
            style: theme.textTheme.bodySmall?.copyWith(color: colors.onSurfaceVariant),
          ),
        ),
        for (final group in result.groups) ...[
          Padding(
            padding: const EdgeInsets.fromLTRB(
              NhSpace.s4,
              NhSpace.s3,
              NhSpace.s4,
              NhSpace.s1,
            ),
            child: Row(
              children: [
                Text(
                  group.label,
                  style: theme.textTheme.titleSmall?.copyWith(
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(width: NhSpace.s2),
                Text(
                  '${group.totalCount}',
                  style: theme.textTheme.labelMedium?.copyWith(
                    color: colors.primary,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ],
            ),
          ),
          for (final item in group.items)
            if (item.route.isNotEmpty)
              ListTile(
                title: Text(
                  item.title,
                  style: theme.textTheme.titleSmall?.copyWith(
                    fontWeight: FontWeight.w600,
                  ),
                ),
                subtitle: item.subtitle.isEmpty
                    ? null
                    : Text(
                        item.subtitle,
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                      ),
                trailing: Icon(Icons.chevron_right_rounded, color: colors.onSurfaceVariant),
                onTap: () => context.push(item.route),
              ),
        ],
        PaginationBar(
          page: result.page,
          totalPages: result.totalPages,
          hasPrevious: result.hasPrevious,
          hasNext: result.hasNext,
          onPage: _goToPage,
        ),
      ],
    );
  }
}
