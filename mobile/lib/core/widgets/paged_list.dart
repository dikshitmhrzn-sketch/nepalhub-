import 'package:flutter/material.dart';

import '../api/api_exception.dart';
import '../api/paged_result.dart';
import '../theme/tokens.dart';
import 'common.dart';
import 'state_message.dart';

typedef PagedLoader<T> = Future<PagedResult<T>> Function(int page);

typedef PagedItemBuilder<T> = Widget Function(BuildContext context, T item, int index);

class PagedListView<T> extends StatefulWidget {
  const PagedListView({
    super.key,
    required this.load,
    required this.itemBuilder,
    this.reloadKey = 0,
    this.emptyTitle = 'Nothing here yet',
    this.emptyDescription,
    this.itemPadding = const EdgeInsets.symmetric(horizontal: NhSpace.s4, vertical: NhSpace.s1),
  });

  final PagedLoader<T> load;
  final PagedItemBuilder<T> itemBuilder;
  final int reloadKey;
  final String emptyTitle;
  final String? emptyDescription;
  final EdgeInsetsGeometry itemPadding;

  @override
  State<PagedListView<T>> createState() => _PagedListViewState<T>();
}

class _PagedListViewState<T> extends State<PagedListView<T>> {
  final ScrollController _scroll = ScrollController();

  PagedResult<T>? _result;
  Object? _error;
  bool _loading = true;
  int _generation = 0;
  int _page = 1;

  @override
  void initState() {
    super.initState();
    _run(1);
  }

  @override
  void didUpdateWidget(covariant PagedListView<T> oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.reloadKey != widget.reloadKey) {
      _run(1);
    }
  }

  @override
  void dispose() {
    _scroll.dispose();
    super.dispose();
  }

  Future<void> _run(int page) async {
    final generation = ++_generation;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final result = await widget.load(page);
      if (!mounted || generation != _generation) {
        return;
      }
      setState(() {
        _result = result;
        _page = result.page;
        _loading = false;
      });
      if (result.page > 1 && _scroll.hasClients) {
        _scroll.jumpTo(0);
      }
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

  Future<void> _refresh() => _run(_page);

  void _retry() {
    final failedPage = _error != null ? _page : 1;
    _run(failedPage);
  }

  @override
  Widget build(BuildContext context) {
    if (_loading && _result == null) {
      return const StateMessage(tone: StateMessageTone.loading, title: 'Loading');
    }
    if (_error != null) {
      final apiError = _error is ApiException ? _error as ApiException : null;
      return StateMessage(
        tone: StateMessageTone.error,
        title: apiError == null ? 'Something went wrong' : ApiMessage.title(apiError),
        description: apiError == null ? '$_error' : ApiMessage.message(apiError),
        actionLabel: apiError != null && !ApiMessage.canRetry(apiError) ? null : 'Try again',
        onAction: apiError != null && !ApiMessage.canRetry(apiError) ? null : _retry,
      );
    }
    final result = _result;
    if (result == null || result.items.isEmpty) {
      return StateMessage(
        tone: StateMessageTone.empty,
        title: widget.emptyTitle,
        description: widget.emptyDescription,
        actionLabel: 'Refresh',
        onAction: () => _run(1),
      );
    }
    final showPagination = result.hasPrevious || result.hasNext;
    return RefreshIndicator(
      onRefresh: _refresh,
      child: ListView.builder(
        controller: _scroll,
        physics: const AlwaysScrollableScrollPhysics(),
        itemCount: result.items.length + (showPagination ? 1 : 0),
        itemBuilder: (context, index) {
          if (index >= result.items.length) {
            return PaginationBar(
              page: result.page,
              totalPages: result.totalPages,
              hasPrevious: result.hasPrevious,
              hasNext: result.hasNext,
              onPage: _run,
            );
          }
          return Padding(
            padding: widget.itemPadding,
            child: widget.itemBuilder(context, result.items[index], index),
          );
        },
      ),
    );
  }
}
