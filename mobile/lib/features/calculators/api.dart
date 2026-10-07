import '../../core/api/api_client.dart';
import '../../core/api/paged_result.dart';
import '../../core/api/query_params.dart';
import '../../core/util/formats.dart';

const calculatorCategories = <String>[
  'Finance',
  'Education',
  'Health',
  'Engineering',
  'Everyday',
];

class CalculatorSummary {
  const CalculatorSummary({
    required this.slug,
    required this.name,
    required this.summary,
    required this.category,
  });

  final String slug;
  final String name;
  final String summary;
  final String category;

  factory CalculatorSummary.fromJson(Map<String, dynamic> json) => CalculatorSummary(
        slug: '${json['slug'] ?? ''}',
        name: '${json['name'] ?? ''}',
        summary: '${json['summary'] ?? ''}',
        category: '${json['category'] ?? ''}',
      );
}

class CalculatorInput {
  const CalculatorInput({
    required this.key,
    required this.label,
    required this.kind,
    this.unit,
    this.minimum,
    this.maximum,
    this.step,
    this.defaultValue,
    required this.required,
    this.help,
  });

  final String key;
  final String label;
  final String kind;
  final String? unit;
  final num? minimum;
  final num? maximum;
  final num? step;
  final num? defaultValue;
  final bool required;
  final String? help;

  factory CalculatorInput.fromJson(Map<String, dynamic> json) => CalculatorInput(
        key: '${json['key'] ?? ''}',
        label: '${json['label'] ?? ''}',
        kind: '${json['kind'] ?? 'Number'}',
        unit: json['unit'] as String?,
        minimum: json['minimum'] as num?,
        maximum: json['maximum'] as num?,
        step: json['step'] as num?,
        defaultValue: json['defaultValue'] as num?,
        required: json['required'] as bool? ?? true,
        help: json['help'] as String?,
      );
}

class CalculatorDetail {
  const CalculatorDetail({
    required this.slug,
    required this.name,
    required this.summary,
    required this.description,
    required this.category,
    required this.inputs,
  });

  final String slug;
  final String name;
  final String summary;
  final String description;
  final String category;
  final List<CalculatorInput> inputs;

  factory CalculatorDetail.fromJson(Map<String, dynamic> json) => CalculatorDetail(
        slug: '${json['slug'] ?? ''}',
        name: '${json['name'] ?? ''}',
        summary: '${json['summary'] ?? ''}',
        description: '${json['description'] ?? ''}',
        category: '${json['category'] ?? ''}',
        inputs: json['inputs'] is List
            ? (json['inputs'] as List)
                .whereType<Map<String, dynamic>>()
                .map(CalculatorInput.fromJson)
                .toList()
            : <CalculatorInput>[],
      );
}

class CalculationLine {
  const CalculationLine({
    required this.key,
    required this.label,
    required this.format,
    this.value,
    this.unit,
    this.text,
  });

  final String key;
  final String label;
  final String format;
  final num? value;
  final String? unit;
  final String? text;

  factory CalculationLine.fromJson(Map<String, dynamic> json) => CalculationLine(
        key: '${json['key'] ?? ''}',
        label: '${json['label'] ?? ''}',
        format: '${json['format'] ?? 'Number'}',
        value: json['value'] as num?,
        unit: json['unit'] as String?,
        text: json['text'] as String?,
      );

  String get display {
    if (text != null && text!.trim().isNotEmpty) {
      return text!;
    }
    final amount = value;
    if (amount == null) {
      return '-';
    }
    switch (format) {
      case 'Currency':
        return '${npr(amount)}${unit == null ? '' : ' ${unit!}'}';
      case 'Percent':
        return '${_trim(amount)}%';
      default:
        return '${_trim(amount)}${unit == null ? '' : ' ${unit!}'}';
    }
  }

  static String _trim(num value) =>
      value == value.roundToDouble() ? value.round().toString() : value.toStringAsFixed(2);
}

class CalculationResult {
  const CalculationResult({
    required this.slug,
    required this.name,
    required this.summary,
    required this.breakdown,
    required this.notes,
  });

  final String slug;
  final String name;
  final List<CalculationLine> summary;
  final List<CalculationLine> breakdown;
  final List<String> notes;

  factory CalculationResult.fromJson(Map<String, dynamic> json) => CalculationResult(
        slug: '${json['slug'] ?? ''}',
        name: '${json['name'] ?? ''}',
        summary: _lines(json['summary']),
        breakdown: _lines(json['breakdown']),
        notes: json['notes'] is List
            ? (json['notes'] as List).map((note) => '$note').toList()
            : <String>[],
      );

  static List<CalculationLine> _lines(Object? raw) => raw is List
      ? raw.whereType<Map<String, dynamic>>().map(CalculationLine.fromJson).toList()
      : <CalculationLine>[];
}

class CalculatorsApi {
  CalculatorsApi(this._client);

  final ApiClient _client;

  Future<PagedResult<CalculatorSummary>> list({
    int page = 1,
    int pageSize = 24,
    String? search,
    String? category,
  }) =>
      _client.getPaged<CalculatorSummary>(
        '/calculators',
        query: queryParams(
          page: page,
          pageSize: pageSize,
          search: search,
          filters: <String, String?>{'category': category},
        ),
        fromJson: CalculatorSummary.fromJson,
      );

  Future<CalculatorDetail> detail(String slug) => _client.getObject<CalculatorDetail>(
        '/calculators/$slug',
        fromJson: CalculatorDetail.fromJson,
      );

  Future<CalculationResult> calculate(String slug, Map<String, num> inputs) =>
      _client.postObject<CalculationResult>(
        '/calculators/$slug/calculate',
        body: <String, Object?>{'inputs': inputs},
        fromJson: CalculationResult.fromJson,
      );
}
