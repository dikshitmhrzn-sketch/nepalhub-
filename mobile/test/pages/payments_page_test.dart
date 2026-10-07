import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:nepal_hub/core/api/api_client.dart';
import 'package:nepal_hub/pages/payments_page.dart';
import 'package:provider/provider.dart';

import '../helpers/fake_adapter.dart';

Object? envelope(Object? data) => <String, Object?>{
      'success': true,
      'data': data,
      'error': null,
    };

Object? paged(List<Object?> items, {int page = 1, bool hasNext = false}) =>
    <String, Object?>{
      'items': items,
      'page': page,
      'pageSize': 20,
      'totalCount': items.length,
      'totalPages': hasNext ? 2 : 1,
      'hasPrevious': page > 1,
      'hasNext': hasNext,
    };

const Map<String, Object?> paidTicket = <String, Object?>{
  'reference': 'PAY-1001',
  'kind': 'EventTicket',
  'status': 'Succeeded',
  'amountNpr': 500,
  'description': 'Tech meetup ticket',
  'failureCode': null,
  'createdAt': '2026-10-01T09:00:00',
  'settledAt': '2026-10-01T09:02:00',
  'refundedAt': null,
};

const Map<String, Object?> pendingPlan = <String, Object?>{
  'reference': 'PAY-2002',
  'kind': 'Subscription',
  'status': 'Pending',
  'amountNpr': 999,
  'description': 'Featured plan',
  'failureCode': null,
  'createdAt': '2026-10-02T10:30:00',
  'settledAt': null,
  'refundedAt': null,
};

Future<void> pumpPayments(WidgetTester tester, TestApi api) async {
  await tester.pumpWidget(
    Provider<ApiClient>.value(
      value: api.client,
      child: MaterialApp(home: const PaymentsPage()),
    ),
  );
  await tester.pumpAndSettle();
}

void main() {
  late TestApi api;

  setUp(() {
    api = TestApi();
  });

  testWidgets('the payment history lists each payment with its status',
      (tester) async {
    api.onJson('GET', '/account/payments', envelope(paged(<Object?>[paidTicket, pendingPlan])));

    await pumpPayments(tester, api);

    expect(find.text('Your payments'), findsOneWidget);
    expect(find.text('Tech meetup ticket'), findsOneWidget);
    expect(find.textContaining('Event ticket'), findsOneWidget);
    expect(find.textContaining('Paid'), findsOneWidget);
    expect(find.text('Rs 500'), findsOneWidget);
    expect(find.text('PAY-1001'), findsOneWidget);
    expect(find.text('Featured plan'), findsOneWidget);
    expect(find.textContaining('Waiting for the provider'), findsOneWidget);
    expect(find.text('Rs 999'), findsOneWidget);
    expect(find.textContaining('Next'), findsNothing);
  });

  testWidgets('the history pages forward', (tester) async {
    api.onJson('GET', '/account/payments', envelope(paged(<Object?>[paidTicket], hasNext: true)));
    api.onJson(
      'GET',
      '/account/payments',
      envelope(paged(<Object?>[pendingPlan], page: 2)),
    );

    await pumpPayments(tester, api);

    expect(find.byTooltip('Next page'), findsOneWidget);
    expect(find.text('Page 1 of 2'), findsOneWidget);

    await tester.tap(find.byTooltip('Next page'));
    await tester.pumpAndSettle();

    expect(find.text('Featured plan'), findsOneWidget);
    expect(find.text('Tech meetup ticket'), findsNothing);
    expect(
      api.requestsWhere('GET', '/account/payments').last.query!['page'],
      2,
    );
  });

  testWidgets('an account with nothing paid yet says so', (tester) async {
    api.onJson('GET', '/account/payments', envelope(paged(<Object?>[])));

    await pumpPayments(tester, api);

    expect(find.text('No payments yet'), findsOneWidget);
    expect(find.textContaining('Event tickets and plans'), findsOneWidget);
  });

  testWidgets('a failed history load can be retried', (tester) async {
    api.onError('GET', '/account/payments', 500, 'server_error', 'Something went wrong.', times: 1);
    api.onJson('GET', '/account/payments', envelope(paged(<Object?>[paidTicket])));

    await pumpPayments(tester, api);

    expect(find.text('Something went wrong'), findsOneWidget);

    await tester.tap(find.widgetWithText(OutlinedButton, 'Try again'));
    await tester.pumpAndSettle();

    expect(find.text('Tech meetup ticket'), findsOneWidget);
    expect(api.countOf('GET', '/account/payments'), 2);
  });
}
