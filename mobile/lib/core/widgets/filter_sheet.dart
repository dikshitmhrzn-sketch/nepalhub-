import 'package:flutter/material.dart';

import '../theme/tokens.dart';
import '../util/formats.dart';

class FilterOption {
  const FilterOption(this.value, this.label);

  final String value;
  final String label;
}

List<FilterOption> filterOptions(
  Iterable<String> values, {
  Map<String, String> labels = const <String, String>{},
}) =>
    values
        .map((value) => FilterOption(value, labels[value] ?? humanize(value)))
        .toList();

class FilterField {
  const FilterField(this.key, this.label, {required this.options});

  final String key;
  final String label;
  final List<FilterOption> options;

  String labelFor(String? value) {
    if (value == null || value.isEmpty) {
      return label;
    }
    for (final option in options) {
      if (option.value == value) {
        return '$label: ${option.label}';
      }
    }
    return '$label: $value';
  }
}

Future<Map<String, String?>?> openFilterSheet(
  BuildContext context, {
  required String title,
  required List<FilterField> fields,
  Map<String, String?> values = const <String, String?>{},
}) =>
    showModalBottomSheet<Map<String, String?>>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      builder: (sheetContext) {
        final selected = Map<String, String?>.from(values);
        return SafeArea(
          child: Padding(
            padding: const EdgeInsets.fromLTRB(
              NhSpace.s4,
              0,
              NhSpace.s4,
              NhSpace.s4,
            ),
            child: StatefulBuilder(
              builder: (context, setSheetState) {
                return Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    ConstrainedBox(
                      constraints: BoxConstraints(
                        maxHeight: MediaQuery.sizeOf(context).height * 0.6,
                      ),
                      child: SingleChildScrollView(
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              title,
                              style: Theme.of(context)
                                  .textTheme
                                  .titleMedium
                                  ?.copyWith(fontWeight: FontWeight.w700),
                            ),
                            for (final field in fields) ...[
                              const SizedBox(height: NhSpace.s4),
                              Text(
                                field.label,
                                style: Theme.of(context)
                                    .textTheme
                                    .bodyMedium
                                    ?.copyWith(
                                      color: Theme.of(context)
                                          .colorScheme
                                          .onSurfaceVariant,
                                    ),
                              ),
                              const SizedBox(height: NhSpace.s2),
                              Wrap(
                                spacing: NhSpace.s2,
                                runSpacing: NhSpace.s1,
                                children: [
                                  ChoiceChip(
                                    label: const Text('All'),
                                    selected:
                                        (selected[field.key] ?? '').isEmpty,
                                    onSelected: (_) => setSheetState(
                                      () => selected[field.key] = null,
                                    ),
                                  ),
                                  for (final option in field.options)
                                    ChoiceChip(
                                      label: Text(option.label),
                                      selected:
                                          selected[field.key] == option.value,
                                      onSelected: (_) => setSheetState(
                                        () => selected[field.key] = option.value,
                                      ),
                                    ),
                                ],
                              ),
                            ],
                          ],
                        ),
                      ),
                    ),
                    const SizedBox(height: NhSpace.s4),
                    Row(
                      children: [
                        TextButton(
                          onPressed: () => setSheetState(selected.clear),
                          child: const Text('Reset'),
                        ),
                        const Spacer(),
                        FilledButton(
                          onPressed: () => Navigator.pop(sheetContext, selected),
                          child: const Text('Apply'),
                        ),
                      ],
                    ),
                  ],
                );
              },
            ),
          ),
        );
      },
    );

class ActiveFilterChips extends StatelessWidget {
  const ActiveFilterChips({
    super.key,
    required this.fields,
    required this.values,
    required this.onRemove,
  });

  final List<FilterField> fields;
  final Map<String, String?> values;
  final ValueChanged<String> onRemove;

  @override
  Widget build(BuildContext context) {
    final active =
        fields.where((field) => (values[field.key] ?? '').isNotEmpty).toList();
    if (active.isEmpty) {
      return const SizedBox.shrink();
    }
    return Padding(
      padding: const EdgeInsets.fromLTRB(
        NhSpace.s4,
        NhSpace.s2,
        NhSpace.s4,
        0,
      ),
      child: Wrap(
        spacing: NhSpace.s2,
        runSpacing: NhSpace.s1,
        children: [
          for (final field in active)
            Chip(
              label: Text(field.labelFor(values[field.key])),
              visualDensity: VisualDensity.compact,
              onDeleted: () => onRemove(field.key),
            ),
        ],
      ),
    );
  }
}
