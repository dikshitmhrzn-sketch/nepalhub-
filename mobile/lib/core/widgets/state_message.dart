import 'package:flutter/material.dart';

import '../theme/tokens.dart';

enum StateMessageTone { loading, error, empty }

class StateMessage extends StatelessWidget {
  const StateMessage({
    super.key,
    required this.tone,
    required this.title,
    this.description,
    this.actionLabel,
    this.onAction,
    this.compact = false,
  });

  final StateMessageTone tone;
  final String title;
  final String? description;
  final String? actionLabel;
  final VoidCallback? onAction;
  final bool compact;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final muted = colors.onSurfaceVariant;

    final IconData icon;
    switch (tone) {
      case StateMessageTone.loading:
        icon = Icons.hourglass_top_rounded;
      case StateMessageTone.error:
        icon = Icons.error_outline_rounded;
      case StateMessageTone.empty:
        icon = Icons.inbox_outlined;
    }

    final children = <Widget>[
      if (tone == StateMessageTone.loading)
        SizedBox(
          width: 28,
          height: 28,
          child: CircularProgressIndicator(
            strokeWidth: 2.5,
            color: colors.primary,
          ),
        )
      else
        Icon(icon, size: compact ? 28 : 36, color: muted),
      SizedBox(height: compact ? NhSpace.s2 : NhSpace.s3),
      Text(
        title,
        textAlign: TextAlign.center,
        style: Theme.of(context).textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w700,
              color: colors.onSurface,
            ),
      ),
      if (description != null && description!.trim().isNotEmpty) ...[
        SizedBox(height: NhSpace.s1),
        Text(
          description!,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodyMedium?.copyWith(color: muted),
        ),
      ],
      if (actionLabel != null && onAction != null) ...[
        SizedBox(height: NhSpace.s3),
        OutlinedButton(onPressed: onAction, child: Text(actionLabel!)),
      ],
    ];

    return Center(
      child: SingleChildScrollView(
        padding: EdgeInsets.symmetric(
          horizontal: NhSpace.s5,
          vertical: compact ? NhSpace.s4 : NhSpace.s7,
        ),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 480),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: children,
          ),
        ),
      ),
    );
  }
}
