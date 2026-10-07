import 'dart:async';

import 'package:flutter/material.dart';

import '../api/api_exception.dart';
import 'common.dart';
import 'state_message.dart';

typedef LoadBuilder<T> = Widget Function(BuildContext context, T data, VoidCallback reload);

class LoadableView<T> extends StatefulWidget {
  const LoadableView({
    super.key,
    required this.load,
    required this.builder,
    this.initialData,
  });

  final Future<T> Function() load;
  final LoadBuilder<T> builder;
  final T? initialData;

  @override
  State<LoadableView<T>> createState() => _LoadableViewState<T>();
}

class _LoadableViewState<T> extends State<LoadableView<T>> {
  T? _data;
  Object? _error;
  bool _loading = true;
  int _generation = 0;

  @override
  void initState() {
    super.initState();
    _run();
  }

  Future<void> _run() async {
    final generation = ++_generation;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final data = await widget.load();
      if (!mounted || generation != _generation) {
        return;
      }
      setState(() {
        _data = data;
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

  void _reload() {
    unawaited(_run());
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const StateMessage(
        tone: StateMessageTone.loading,
        title: 'Loading',
      );
    }
    if (_error != null) {
      final apiError = _error is ApiException ? _error as ApiException : null;
      if (apiError != null) {
        return StateMessage(
          tone: StateMessageTone.error,
          title: ApiMessage.title(apiError),
          description: ApiMessage.message(apiError),
          actionLabel: 'Try again',
          onAction: _reload,
        );
      }
      return StateMessage(
        tone: StateMessageTone.error,
        title: 'Something went wrong',
        description: '$_error',
        actionLabel: 'Try again',
        onAction: _reload,
      );
    }
    final data = _data;
    if (data == null) {
      return StateMessage(
        tone: StateMessageTone.empty,
        title: 'Nothing here yet',
        actionLabel: 'Try again',
        onAction: _reload,
      );
    }
    return widget.builder(context, data, _reload);
  }
}
