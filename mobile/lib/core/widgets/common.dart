import 'package:flutter/material.dart';

import '../api/api_exception.dart';
import '../theme/tokens.dart';

class ApiMessage {
  ApiMessage._();

  static String title(ApiException error) {
    if (error.isNetwork) {
      return 'You appear to be offline';
    }
    if (error.isTimeout) {
      return 'The request took too long';
    }
    if (error.isRateLimited) {
      return 'Too many attempts';
    }
    if (error.isSessionInvalid) {
      return 'Please sign in again';
    }
    if (error.code == ApiException.csrfUnavailable) {
      return 'Could not start a secure session';
    }
    return 'Something went wrong';
  }

  static String message(ApiException error) {
    if (error.isNetwork) {
      return 'Could not reach the server. Check your connection.';
    }
    if (error.isTimeout) {
      return 'The request timed out. Please try again.';
    }
    if (error.isRateLimited) {
      return 'Too many attempts. Please wait a minute and try again.';
    }
    if (error.isSessionInvalid) {
      return 'Your session is no longer valid. Please sign in again.';
    }
    if (error.isCancelled) {
      return 'The request was cancelled.';
    }
    if (error.message.trim().isEmpty) {
      return 'Please try again.';
    }
    return error.message;
  }

  static bool canRetry(ApiException error) =>
      !error.isSessionInvalid || !error.isCancelled;
}

class PaginationBar extends StatelessWidget {
  const PaginationBar({
    super.key,
    required this.page,
    required this.totalPages,
    required this.hasPrevious,
    required this.hasNext,
    required this.onPage,
  });

  final int page;
  final int totalPages;
  final bool hasPrevious;
  final bool hasNext;
  final ValueChanged<int> onPage;

  @override
  Widget build(BuildContext context) {
    if (totalPages <= 1) {
      return const SizedBox.shrink();
    }
    final colors = Theme.of(context).colorScheme;
    return Padding(
      padding: const EdgeInsets.symmetric(
        horizontal: NhSpace.s4,
        vertical: NhSpace.s3,
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          IconButton(
            tooltip: 'Previous page',
            onPressed: hasPrevious ? () => onPage(page - 1) : null,
            icon: const Icon(Icons.chevron_left_rounded),
            color: colors.primary,
            disabledColor: colors.onSurfaceVariant,
          ),
          Text(
            'Page $page of $totalPages',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                  color: colors.onSurfaceVariant,
                ),
          ),
          IconButton(
            tooltip: 'Next page',
            onPressed: hasNext ? () => onPage(page + 1) : null,
            icon: const Icon(Icons.chevron_right_rounded),
            color: colors.primary,
            disabledColor: colors.onSurfaceVariant,
          ),
        ],
      ),
    );
  }
}
