import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/api/api_client.dart';
import '../../core/api/api_exception.dart';
import '../../core/theme/tokens.dart';
import '../../core/widgets/common.dart';
import '../../core/widgets/loadable_view.dart';
import '../../features/calculators/api.dart';

class CalculatorPage extends StatefulWidget {
  const CalculatorPage({super.key, required this.slug});

  final String slug;

  @override
  State<CalculatorPage> createState() => _CalculatorPageState();
}

class _CalculatorPageState extends State<CalculatorPage> {
  final Map<String, TextEditingController> _controllers =
      <String, TextEditingController>{};

  CalculationResult? _result;
  Object? _error;
  bool _calculating = false;

  @override
  void dispose() {
    for (final controller in _controllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  Future<void> _calculate(CalculatorDetail detail) async {
    final inputs = <String, num>{};
    for (final input in detail.inputs) {
      final text = _controllers[input.key]?.text.trim() ?? '';
      if (text.isEmpty) {
        if (input.required) {
          setState(() => _error = 'Enter a value for ${input.label}.');
          return;
        }
        continue;
      }
      final value = num.tryParse(text);
      if (value == null) {
        setState(() => _error = 'Enter a number for ${input.label}.');
        return;
      }
      inputs[input.key] = value;
    }
    setState(() {
      _calculating = true;
      _error = null;
      _result = null;
    });
    try {
      final api = CalculatorsApi(context.read<ApiClient>());
      final result = await api.calculate(widget.slug, inputs);
      if (!mounted) {
        return;
      }
      setState(() {
        _result = result;
        _calculating = false;
      });
    } catch (error) {
      if (!mounted) {
        return;
      }
      setState(() {
        _error = error;
        _calculating = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final api = CalculatorsApi(context.read<ApiClient>());
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    return Scaffold(
      appBar: AppBar(title: const Text('Calculator')),
      body: LoadableView<CalculatorDetail>(
        load: () => api.detail(widget.slug),
        builder: (context, detail, _) {
          for (final input in detail.inputs) {
            _controllers.putIfAbsent(
              input.key,
              () => TextEditingController(
                text: input.defaultValue == null ? '' : '${input.defaultValue}',
              ),
            );
          }
          return ListView(
            padding: const EdgeInsets.all(NhSpace.s4),
            children: [
              Text(
                detail.name,
                style: theme.textTheme.headlineSmall?.copyWith(
                  fontWeight: FontWeight.w800,
                ),
              ),
              if (detail.summary.trim().isNotEmpty) ...[
                const SizedBox(height: NhSpace.s1),
                Text(
                  detail.summary,
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: colors.onSurfaceVariant,
                  ),
                ),
              ],
              if (detail.description.trim().isNotEmpty) ...[
                const SizedBox(height: NhSpace.s3),
                Text(detail.description, style: theme.textTheme.bodyMedium),
              ],
              const SizedBox(height: NhSpace.s4),
              for (final input in detail.inputs) ...[
                Padding(
                  padding: const EdgeInsets.only(bottom: NhSpace.s3),
                  child: TextField(
                    controller: _controllers[input.key],
                    keyboardType: const TextInputType.numberWithOptions(decimal: true),
                    decoration: InputDecoration(
                      labelText: input.label,
                      hintText: input.kind,
                      suffixText: input.unit,
                      helperText: input.help,
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(12),
                      ),
                      isDense: true,
                    ),
                  ),
                ),
              ],
              FilledButton.icon(
                onPressed: _calculating ? null : () => _calculate(detail),
                icon: _calculating
                    ? const SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : const Icon(Icons.functions_rounded, size: 18),
                label: Text(_calculating ? 'Calculating' : 'Calculate'),
              ),
              if (_error != null) ...[
                const SizedBox(height: NhSpace.s3),
                Text(
                  _error is ApiException
                      ? ApiMessage.message(_error as ApiException)
                      : '$_error',
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: colors.error,
                  ),
                ),
              ],
              if (_result != null) ...[
                const SizedBox(height: NhSpace.s4),
                _ResultCard(result: _result!),
              ],
            ],
          );
        },
      ),
    );
  }
}

class _ResultCard extends StatelessWidget {
  const _ResultCard({required this.result});

  final CalculationResult result;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    return Card(
      margin: EdgeInsets.zero,
      child: Padding(
        padding: const EdgeInsets.all(NhSpace.s4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Result',
              style: theme.textTheme.titleMedium?.copyWith(fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: NhSpace.s2),
            for (final line in result.summary) _Line(line: line),
            if (result.breakdown.isNotEmpty) ...[
              const SizedBox(height: NhSpace.s3),
              Text(
                'Breakdown',
                style: theme.textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w700),
              ),
              const SizedBox(height: NhSpace.s1),
              for (final line in result.breakdown) _Line(line: line),
            ],
            if (result.notes.isNotEmpty) ...[
              const SizedBox(height: NhSpace.s3),
              for (final note in result.notes)
                Padding(
                  padding: const EdgeInsets.only(bottom: NhSpace.s1),
                  child: Text(
                    note,
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: colors.onSurfaceVariant,
                    ),
                  ),
                ),
            ],
          ],
        ),
      ),
    );
  }
}

class _Line extends StatelessWidget {
  const _Line({required this.line});

  final CalculationLine line;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: NhSpace.s1),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(
            child: Text(line.label, style: theme.textTheme.bodyMedium),
          ),
          const SizedBox(width: NhSpace.s2),
          Text(
            line.display,
            style: theme.textTheme.bodyMedium?.copyWith(
              fontWeight: FontWeight.w700,
            ),
          ),
        ],
      ),
    );
  }
}
