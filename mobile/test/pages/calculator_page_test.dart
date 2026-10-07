import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nepal_hub/core/api/api_client.dart';
import 'package:nepal_hub/pages/calculator_page.dart';
import 'package:provider/provider.dart';

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

Future<void> pumpCalculator(WidgetTester tester, TestApi api) async {
  tester.view.physicalSize = const Size(1080, 2400);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  await tester.pumpWidget(
    Provider<ApiClient>.value(
      value: api.client,
      child: const MaterialApp(home: CalculatorPage(slug: 'loan-emi')),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> fillAndCalculate(WidgetTester tester) async {
  await tester.enterText(find.byType(TextField).at(0), '1000000');
  await tester.enterText(find.byType(TextField).at(1), '12');
  await tester.enterText(find.byType(TextField).at(2), '60');
  await tester.tap(find.widgetWithText(FilledButton, 'Calculate'));
  await tester.pumpAndSettle();
}

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

  testWidgets('a calculation shows its result under the form', (tester) async {
    api.onJson('GET', '/calculators/loan-emi', envelope(detailPayload));
    api.onJson('POST', '/calculators/loan-emi/calculate', envelope(calculatePayload));

    await pumpCalculator(tester, api);

    expect(find.text('Loan EMI'), findsOneWidget);
    expect(find.text('Loan amount'), findsOneWidget);

    await fillAndCalculate(tester);

    expect(find.text('Result'), findsOneWidget);
    expect(find.text('Monthly payment'), findsOneWidget);
    expect(find.text('Rs 22,244 NPR'), findsOneWidget);
    expect(find.text('Breakdown'), findsOneWidget);
    expect(find.text('Total interest'), findsOneWidget);
    expect(find.text('Rs 334,654 NPR'), findsOneWidget);
    expect(find.text('Comfortable'), findsOneWidget);
    expect(find.text('Rates are annual and compounded monthly.'), findsOneWidget);

    final body = api.requestsWhere('POST', '/calculators/loan-emi/calculate').single.jsonBody!;
    expect(body['inputs'], <String, Object?>{
      'principal': 1000000,
      'rate': 12,
      'months': 60,
    });
  });

  testWidgets('an empty required field is caught before the request',
      (tester) async {
    api.onJson('GET', '/calculators/loan-emi', envelope(detailPayload));

    await pumpCalculator(tester, api);

    await tester.enterText(find.byType(TextField).at(1), '12');
    await tester.tap(find.widgetWithText(FilledButton, 'Calculate'));
    await tester.pumpAndSettle();

    expect(find.text('Enter a value for Loan amount.'), findsOneWidget);
    expect(api.countOf('POST', '/calculators/loan-emi/calculate'), 0);
  });

  testWidgets('a refusal from the server is explained', (tester) async {
    api.onJson('GET', '/calculators/loan-emi', envelope(detailPayload));
    api.onError(
      'POST',
      '/calculators/loan-emi/calculate',
      400,
      'calculation_failed',
      'Those inputs could not be calculated.',
    );

    await pumpCalculator(tester, api);
    await fillAndCalculate(tester);

    expect(find.text('Those inputs could not be calculated.'), findsOneWidget);
    expect(find.text('Result'), findsNothing);
  });
}
