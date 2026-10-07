import 'package:flutter_test/flutter_test.dart';
import 'package:nepal_hub/features/payments/api.dart';

import '../helpers/fake_adapter.dart';

Map<String, Object?> envelope(Object? data) => <String, Object?>{
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

  test('the payment config reports the provider', () async {
    api.onJson(
      'GET',
      '/payments/config',
      envelope(<String, Object?>{'available': true, 'provider': 'esewa'}),
    );

    final config = await PaymentsApi(api.client).config();

    expect(config.available, isTrue);
    expect(config.provider, 'esewa');
  });

  test('a ticket checkout posts kind, slug and quantity', () async {
    api.onJson(
      'POST',
      '/payments/checkout',
      envelope(<String, Object?>{
        'reference': 'PAY-1001',
        'checkoutUrl': 'https://pay.example.com/step1',
        'fields': <String, Object?>{'amt': '500', 'pid': 'PAY-1001'},
        'status': 'Pending',
      }),
    );

    final checkout = await PaymentsApi(api.client).checkout(
      kind: 'EventTicket',
      slug: 'tech-meetup',
      quantity: 3,
    );

    final request = api.requestsWhere('POST', '/payments/checkout').single;
    expect(request.jsonBody, <String, Object?>{
      'kind': 'EventTicket',
      'slug': 'tech-meetup',
      'quantity': 3,
    });
    expect(request.header('X-CSRF-TOKEN'), 'csrf-token-1');
    expect(checkout.reference, 'PAY-1001');
    expect(checkout.needsForm, isTrue);
    expect(checkout.fields, <String, String>{'amt': '500', 'pid': 'PAY-1001'});
  });

  test('a subscription checkout carries the tier', () async {
    api.onJson(
      'POST',
      '/payments/checkout',
      envelope(<String, Object?>{
        'reference': 'PAY-2002',
        'checkoutUrl': null,
        'fields': null,
        'status': 'Succeeded',
      }),
    );

    final checkout = await PaymentsApi(api.client).checkout(
      kind: 'Subscription',
      slug: 'himalaya-labs',
      tier: 'Featured',
    );

    final body = api.requestsWhere('POST', '/payments/checkout').single.jsonBody!;
    expect(body['tier'], 'Featured');
    expect(body['quantity'], 1);
    expect(checkout.needsForm, isFalse);
  });

  test('verification sends the carried payload when there is one', () async {
    api.onJson('POST', '/payments/verify', envelope(paymentPayload));

    final payment = await PaymentsApi(api.client).verify(
      reference: 'PAY-1001',
      data: 'c2lnbmFs',
      signature: 'c2ln',
    );

    final body = api.requestsWhere('POST', '/payments/verify').single.jsonBody!;
    expect(body, <String, Object?>{
      'reference': 'PAY-1001',
      'data': 'c2lnbmFs',
      'signature': 'c2ln',
    });
    expect(payment.isSettled, isTrue);
    expect(payment.amountNpr, 500);
    expect(payment.kind, 'EventTicket');
  });

  test('verification can ask by reference alone', () async {
    api.onJson('POST', '/payments/verify', envelope(paymentPayload));

    final payment = await PaymentsApi(api.client).verify(reference: 'PAY-1001');

    final body = api.requestsWhere('POST', '/payments/verify').single.jsonBody!;
    expect(body, <String, Object?>{'reference': 'PAY-1001'});
    expect(payment.providerReference, 'esewa-991');
  });

  test('one payment reads back by reference', () async {
    api.onJson('GET', '/payments/PAY-1001', envelope(paymentPayload));

    final payment = await PaymentsApi(api.client).payment('PAY-1001');

    expect(payment.reference, 'PAY-1001');
    expect(payment.settledAt, isNotNull);
  });

  test('payment history is paged per account', () async {
    api.onJson(
      'GET',
      '/account/payments',
      <String, Object?>{
        'success': true,
        'data': <String, Object?>{
          'items': [
            <String, Object?>{
              'reference': 'PAY-1001',
              'kind': 'EventTicket',
              'status': 'Succeeded',
              'amountNpr': 500,
              'description': 'Tech meetup ticket',
              'failureCode': null,
              'createdAt': '2026-10-01T09:00:00',
              'settledAt': '2026-10-01T09:02:00',
              'refundedAt': null,
            },
          ],
          'page': 2,
          'pageSize': 20,
          'totalCount': 21,
          'totalPages': 2,
          'hasPrevious': true,
          'hasNext': false,
        },
        'error': null,
      },
    );

    final result = await PaymentsApi(api.client).myPayments(page: 2);

    final query = api.requestsWhere('GET', '/account/payments').single.query!;
    expect(query['page'], 2);
    expect(query['pageSize'], 20);
    expect(result.page, 2);
    expect(result.items.single.reference, 'PAY-1001');
    expect(result.items.single.status, 'Succeeded');
    expect(result.hasPrevious, isTrue);
  });
}
