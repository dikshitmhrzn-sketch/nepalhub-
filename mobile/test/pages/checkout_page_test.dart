import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nepal_hub/core/api/api_client.dart';
import 'package:nepal_hub/features/payments/api.dart';
import 'package:nepal_hub/pages/checkout_page.dart';
import 'package:provider/provider.dart';

import '../helpers/fake_adapter.dart';

Object? envelope(Object? data) => <String, Object?>{
      'success': true,
      'data': data,
      'error': null,
    };

const Map<String, Object?> paymentPayload = <String, Object?>{
  'reference': 'PAY-1001',
  'kind': 'EventTicket',
  'status': 'Succeeded',
  'amountNpr': 500,
  'description': 'Tech meetup ticket',
  'failureCode': null,
  'providerReference': 'esewa-991',
  'createdAt': '2026-10-01T09:00:00',
  'settledAt': '2026-10-01T09:02:00',
  'refundedAt': null,
};

const Checkout handshakedCheckout = Checkout(
  reference: 'PAY-1001',
  checkoutUrl: 'https://pay.example.com/step1',
  fields: <String, String>{'amt': '500', 'pid': 'PAY-1001'},
  status: 'Pending',
);

Future<void> pumpCheckout(WidgetTester tester, TestApi api, Checkout? checkout) async {
  await tester.pumpWidget(
    Provider<ApiClient>.value(
      value: api.client,
      child: MaterialApp(home: CheckoutPage(checkout: checkout)),
    ),
  );
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

  testWidgets('a checkout that was never started says so', (tester) async {
    await pumpCheckout(tester, api, null);

    expect(find.text('This checkout is no longer available'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Close'), findsOneWidget);
  });

  testWidgets('a payment that already finished needs no provider pages',
      (tester) async {
    await pumpCheckout(
      tester,
      api,
      const Checkout(reference: 'PAY-1001', status: 'Succeeded'),
    );

    expect(find.text('That payment has already finished'), findsOneWidget);
    expect(find.textContaining('Reference PAY-1001'), findsOneWidget);
  });

  testWidgets('the buyer can check a payment they paid elsewhere',
      (tester) async {
    api.onJson('POST', '/payments/verify', envelope(paymentPayload));

    await pumpCheckout(tester, api, handshakedCheckout);

    expect(find.text('Complete your payment'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Open the payment page'), findsOneWidget);

    await tester.tap(find.widgetWithText(OutlinedButton, 'I have paid — check'));
    await tester.pumpAndSettle();

    final body = api.requestsWhere('POST', '/payments/verify').single.jsonBody!;
    expect(body, <String, Object?>{'reference': 'PAY-1001'});
    expect(find.text('Payment confirmed'), findsOneWidget);
    expect(find.textContaining('Tech meetup ticket'), findsOneWidget);
    expect(find.textContaining('Reference PAY-1001'), findsOneWidget);
    expect(find.widgetWithText(FilledButton, 'Done'), findsOneWidget);
  });

  testWidgets('an unsettled payment offers another check', (tester) async {
    api.onJson(
      'POST',
      '/payments/verify',
      envelope(<String, Object?>{
        'reference': 'PAY-1001',
        'kind': 'EventTicket',
        'status': 'Pending',
        'amountNpr': 500,
        'description': 'Tech meetup ticket',
        'failureCode': null,
        'providerReference': null,
        'createdAt': '2026-10-01T09:00:00',
        'settledAt': null,
        'refundedAt': null,
      }),
    );

    await pumpCheckout(tester, api, handshakedCheckout);
    await tester.tap(find.widgetWithText(OutlinedButton, 'I have paid — check'));
    await tester.pumpAndSettle();

    expect(find.text('Still waiting for the provider'), findsOneWidget);
    expect(find.widgetWithText(OutlinedButton, 'Check again'), findsOneWidget);
    expect(api.countOf('POST', '/payments/verify'), 1);
  });

  testWidgets('a refused signature can be asked again without the payload',
      (tester) async {
    api.onError(
      'POST',
      '/payments/verify',
      400,
      'payment_signature_invalid',
      'The reply from the payment provider could not be confirmed.',
      times: 1,
    );
    api.onJson('POST', '/payments/verify', envelope(paymentPayload));

    await pumpCheckout(tester, api, handshakedCheckout);
    await tester.tap(find.widgetWithText(OutlinedButton, 'I have paid — check'));
    await tester.pumpAndSettle();

    expect(
      find.text('The reply from the payment provider could not be confirmed.'),
      findsOneWidget,
    );
    expect(find.widgetWithText(TextButton, 'Ask the provider again'), findsOneWidget);

    await tester.tap(find.widgetWithText(TextButton, 'Ask the provider again'));
    await tester.pumpAndSettle();

    expect(api.countOf('POST', '/payments/verify'), 2);
    final second = api.requestsWhere('POST', '/payments/verify').last.jsonBody!;
    expect(second, <String, Object?>{'reference': 'PAY-1001'});
    expect(find.text('Payment confirmed'), findsOneWidget);
  });

  testWidgets('a failed payment is reported as failed', (tester) async {
    api.onJson(
      'POST',
      '/payments/verify',
      envelope(<String, Object?>{
        'reference': 'PAY-1001',
        'kind': 'EventTicket',
        'status': 'Failed',
        'amountNpr': 500,
        'description': 'Tech meetup ticket',
        'failureCode': 'NOT_PAID',
        'providerReference': null,
        'createdAt': '2026-10-01T09:00:00',
        'settledAt': null,
        'refundedAt': null,
      }),
    );

    await pumpCheckout(tester, api, handshakedCheckout);
    await tester.tap(find.widgetWithText(OutlinedButton, 'I have paid — check'));
    await tester.pumpAndSettle();

    expect(find.text('The payment did not go through'), findsOneWidget);
    expect(find.textContaining('Nothing was taken for this one.'), findsOneWidget);
  });
}
