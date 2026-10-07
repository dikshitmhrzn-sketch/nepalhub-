import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../api/api_client.dart';
import '../api/api_exception.dart';
import '../auth/session_gate.dart';
import '../theme/tokens.dart';
import '../widgets/error_banner.dart';
import '../../features/reports/api.dart';

class ReportAction extends StatelessWidget {
  const ReportAction({
    super.key,
    required this.targetType,
    required this.targetId,
  });

  final String targetType;
  final String targetId;

  @override
  Widget build(BuildContext context) {
    return OutlinedButton.icon(
      icon: const Icon(Icons.flag_outlined, size: 18),
      label: const Text('Report this listing'),
      onPressed: () async {
        final signedIn = await ensureSignedIn(context);
        if (!signedIn || !context.mounted) {
          return;
        }
        await openReportSheet(
          context,
          targetType: targetType,
          targetId: targetId,
        );
      },
    );
  }
}

Future<void> openReportSheet(
  BuildContext context, {
  required String targetType,
  required String targetId,
}) =>
    showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      builder: (sheetContext) => _ReportSheet(
        targetType: targetType,
        targetId: targetId,
      ),
    );

class _ReportSheet extends StatefulWidget {
  const _ReportSheet({
    required this.targetType,
    required this.targetId,
  });

  final String targetType;
  final String targetId;

  @override
  State<_ReportSheet> createState() => _ReportSheetState();
}

class _ReportSheetState extends State<_ReportSheet> {
  final TextEditingController _note = TextEditingController();
  String _reason = reportReasons.keys.first;
  bool _submitting = false;
  ApiException? _error;

  @override
  void dispose() {
    _note.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final api = ReportsApi(context.read<ApiClient>());
    final messenger = ScaffoldMessenger.of(context);
    final theme = Theme.of(context);
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      await api.create(
        targetType: widget.targetType,
        targetId: widget.targetId,
        reason: _reason,
        note: _note.text,
      );
      if (!mounted) {
        return;
      }
      Navigator.of(context).pop();
      messenger.showSnackBar(
        SnackBar(
          backgroundColor: theme.colorScheme.primary,
          content: Text(
            'Report submitted. Our team will review this listing.',
            style: theme.textTheme.bodyMedium?.copyWith(
              color: theme.colorScheme.onPrimary,
            ),
          ),
        ),
      );
    } catch (error) {
      if (!mounted) {
        return;
      }
      setState(() {
        _submitting = false;
        _error = error is ApiException ? error : null;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return SafeArea(
      child: Padding(
        padding: EdgeInsets.only(bottom: MediaQuery.viewInsetsOf(context).bottom),
        child: ConstrainedBox(
          constraints: BoxConstraints(
            maxHeight: MediaQuery.sizeOf(context).height * 0.7,
          ),
          child: SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(
              NhSpace.s4,
              NhSpace.s3,
              NhSpace.s4,
              NhSpace.s4,
            ),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Center(
                  child: Container(
                    width: 36,
                    height: 4,
                    decoration: BoxDecoration(
                      color: theme.colorScheme.outlineVariant,
                      borderRadius: BorderRadius.circular(NhRadius.pill),
                    ),
                  ),
                ),
                const SizedBox(height: NhSpace.s3),
                Text(
                  'Report ${reportTargetLabels[widget.targetType] ?? 'this listing'}',
                  style: theme.textTheme.titleMedium
                      ?.copyWith(fontWeight: FontWeight.w700),
                ),
                const SizedBox(height: NhSpace.s1),
                Text(
                  'Tell us what is wrong with this listing. Reports are reviewed by the moderation team.',
                  style: theme.textTheme.bodySmall
                      ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                ),
                const SizedBox(height: NhSpace.s2),
                for (final entry in reportReasons.entries)
                  InkWell(
                    onTap: _submitting
                        ? null
                        : () => setState(() => _reason = entry.key),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(vertical: NhSpace.s1),
                      child: Row(
                        children: [
                          Icon(
                            _reason == entry.key
                                ? Icons.radio_button_checked_rounded
                                : Icons.radio_button_unchecked_rounded,
                            size: 20,
                            color: _reason == entry.key
                                ? theme.colorScheme.primary
                                : theme.colorScheme.onSurfaceVariant,
                          ),
                          const SizedBox(width: NhSpace.s2),
                          Expanded(child: Text(entry.value)),
                        ],
                      ),
                    ),
                  ),
                const SizedBox(height: NhSpace.s2),
                TextField(
                  controller: _note,
                  maxLength: 500,
                  enabled: !_submitting,
                  decoration: const InputDecoration(
                    labelText: 'Add a note (optional)',
                    hintText: 'What should we look at?',
                  ),
                ),
                if (_error != null) ...[
                  const SizedBox(height: NhSpace.s2),
                  ErrorBanner.fromApi(_error!),
                ],
                const SizedBox(height: NhSpace.s3),
                SizedBox(
                  width: double.infinity,
                  child: FilledButton(
                    onPressed: _submitting ? null : _submit,
                    child: _submitting
                        ? const SizedBox(
                            width: 18,
                            height: 18,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          )
                        : const Text('Send report'),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
