import 'package:flutter_test/flutter_test.dart';
import 'package:nepal_hub/features/calculators/api.dart';

import '../helpers/fake_adapter.dart';

Object? envelope(Object? data) => <String, Object?>{
      'success': true,
      'data': data,
      'error': null,
    };

const Map<String, Object?> detailPayload = <String, Object?>{
  'id': 'c-1',
  'slug': 'loan-emi',
  'name': 'Loan EMI',
  'summary': 'Monthly instalment for a reducing balance loan.',
  'description': 'Enter the amount, the annual rate and the term in months.',
  'category': 'Finance',
  'inputs': <Object?>[
    <String, Object?>{
      'key': 'principal',
      'label': 'Loan amount',
      'kind': 'Number',
      'unit': 'NPR',
      'minimum': 1000,
      'maximum': 100000000,
      'step': 1000,
      'defaultValue': null,
      'required': true,
      'help': 'The amount you borrow.',
    },
    <String, Object?>{
      'key': 'rate',
      'label': 'Annual interest rate',
      'kind': 'Number',
      'unit': '%',
      'minimum': 0,
      'maximum': 40,
      'step': 0.25,
      'defaultValue': 12,
      'required': true,
      'help': null,
    },
    <String, Object?>{
      'key': 'months',
      'label': 'Term in months',
      'kind': 'Number',
      'unit': 'months',
      'minimum': 1,
      'maximum': 360,
      'step': 1,
      'defaultValue': 60,
      'required': true,
      'help': null,
    },
  ],
  'isPublished': true,
  'sortOrder': 1,
  'updatedAt': '2026-10-01T09:00:00',
};

const Map<String, Object?> calculatePayload = <String, Object?>{
  'slug': 'loan-emi',
  'name': 'Loan EMI',
  'result': <String, Object?>{
    'summary': <Object?>[
      <String, Object?>{
        'key': 'emi',
        'label': 'Monthly payment',
        'format': 'Currency',
        'value': 22244.24,
        'unit': 'NPR',
        'text': null,
      },
      <String, Object?>{
        'key': 'total',
        'label': 'Total payable',
        'format': 'Currency',
        'value': 1334654.4,
        'unit': 'NPR',
        'text': null,
      },
    ],
    'breakdown': <Object?>[
      <String, Object?>{
        'key': 'interest',
        'label': 'Total interest',
        'format': 'Currency',
        'value': 334654.4,
        'unit': 'NPR',
        'text': null,
      },
      <String, Object?>{
        'key': 'ratio',
        'label': 'Interest share',
        'format': 'Percent',
        'value': 25.1,
        'unit': null,
        'text': null,
      },
      <String, Object?>{
        'key': 'band',
        'label': 'Comfort band',
        'format': 'Text',
        'value': null,
        'unit': null,
        'text': 'Comfortable',
      },
    ],
    'notes': <Object?>['Rates are annual and compounded monthly.'],
  },
};

void main() {
  late TestApi api;

  setUp(() {
    api = TestApi();
    api.onJson(
      'GET',
      '/auth/csrf',
      envelope(<String, Object?>{
        'headerName': 'X-CSRF-TOKEN',
        'token': 'csrf-token-1',
      }),
      times: 5,
    );
  });

  test('the calculator detail carries the input schema', () async {
    api.onJson('GET', '/calculators/loan-emi', envelope(detailPayload));

    final detail = await CalculatorsApi(api.client).detail('loan-emi');

    expect(detail.slug, 'loan-emi');
    expect(detail.category, 'Finance');
    expect(detail.inputs, hasLength(3));

    final principal = detail.inputs.first;
    expect(principal.key, 'principal');
    expect(principal.label, 'Loan amount');
    expect(principal.required, isTrue);
    expect(principal.unit, 'NPR');
    expect(principal.minimum, 1000);
    expect(principal.help, 'The amount you borrow.');

    final rate = detail.inputs[1];
    expect(rate.defaultValue, 12);
    expect(rate.help, isNull);
  });

  test('a calculation posts the inputs and reads the wrapped result',
      () async {
    api.onJson('POST', '/calculators/loan-emi/calculate', envelope(calculatePayload));

    final result = await CalculatorsApi(api.client).calculate(
      'loan-emi',
      <String, num>{'principal': 1000000, 'rate': 12, 'months': 60},
    );

    final request = api.requestsWhere('POST', '/calculators/loan-emi/calculate').single;
    expect(request.jsonBody, <String, Object?>{
      'inputs': <String, Object?>{
        'principal': 1000000,
        'rate': 12,
        'months': 60,
      },
    });
    expect(request.header('X-CSRF-TOKEN'), isNotNull);

    expect(result.slug, 'loan-emi');
    expect(result.name, 'Loan EMI');
    expect(result.summary, hasLength(2));
    expect(result.summary.first.label, 'Monthly payment');
    expect(result.summary.first.display, 'Rs 22,244 NPR');
    expect(result.summary.last.display, 'Rs 1,334,654 NPR');
    expect(result.breakdown, hasLength(3));
    expect(result.breakdown[1].display, '25.10%');
    expect(result.breakdown[2].display, 'Comfortable');
    expect(result.notes, <String>['Rates are annual and compounded monthly.']);
  });

  test('a result without the wrapper still reads', () async {
    api.onJson(
      'POST',
      '/calculators/loan-emi/calculate',
      envelope(<String, Object?>{
        'slug': 'loan-emi',
        'name': 'Loan EMI',
        'summary': <Object?>[
          <String, Object?>{
            'key': 'emi',
            'label': 'Monthly payment',
            'format': 'Currency',
            'value': 100,
            'unit': null,
            'text': null,
          },
        ],
        'breakdown': <Object?>[],
        'notes': <Object?>[],
      }),
    );

    final result = await CalculatorsApi(api.client).calculate(
      'loan-emi',
      <String, num>{'principal': 1000},
    );

    expect(result.summary.single.display, 'Rs 100');
    expect(result.breakdown, isEmpty);
  });
}
