import 'package:flutter_test/flutter_test.dart';
import 'package:nepal_hub/core/api/api_exception.dart';
import 'package:nepal_hub/features/bookings/api.dart';
import 'package:nepal_hub/features/reports/api.dart';

import '../helpers/fake_adapter.dart';

Map<String, Object?> envelope(Object? data) => <String, Object?>{
      'success': true,
      'data': data,
      'error': null,
    };

const Map<String, Object?> bookingPayload = <String, Object?>{
  'id': 'b-1',
  'eventId': 'e-1',
  'eventSlug': 'tech-meetup',
  'title': 'Tech meetup',
  'startsAt': '2026-11-05T10:00:00',
  'quantity': 2,
  'status': 'Booked',
  'createdAt': '2026-10-01T09:00:00',
  'cancelledAt': null,
};

const Map<String, Object?> reportPayload = <String, Object?>{
  'id': 'r-1',
  'targetType': 'Job',
  'targetId': '0f4a1c2e-1111-2222-3333-444455556666',
  'reason': 'Spam',
  'status': 'Open',
  'createdAt': '2026-10-01T09:00:00',
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

  test('booking posts the quantity and returns the booking', () async {
    api.onJson('POST', '/events/tech-meetup/booking', envelope(bookingPayload));

    final booking = await BookingsApi(api.client).book('tech-meetup', quantity: 2);

    final request = api.requestsWhere('POST', '/events/tech-meetup/booking').single;
    expect(request.jsonBody, <String, Object?>{'quantity': 2});
    expect(request.header('X-CSRF-TOKEN'), 'csrf-token-1');
    expect(booking.eventSlug, 'tech-meetup');
    expect(booking.quantity, 2);
    expect(booking.isActive, isTrue);
    expect(booking.cancelledAt, isNull);
  });

  test('releasing a place sends a delete', () async {
    api.onJson(
      'DELETE',
      '/events/tech-meetup/booking',
      envelope(<String, Object?>{
        'id': 'b-1',
        'eventSlug': 'tech-meetup',
        'title': 'Tech meetup',
        'startsAt': '2026-11-05T10:00:00',
        'quantity': 1,
        'status': 'Cancelled',
        'createdAt': '2026-10-01T09:00:00',
        'cancelledAt': '2026-10-02T09:00:00',
      }),
    );

    final booking = await BookingsApi(api.client).cancel('tech-meetup');

    expect(api.countOf('DELETE', '/events/tech-meetup/booking'), 1);
    expect(booking.isActive, isFalse);
    expect(booking.cancelledAt, isNotNull);
  });

  test('a booked event refuses a second booking with its own message', () async {
    api.onError(
      'POST',
      '/events/tech-meetup/booking',
      409,
      'already_booked',
      'You already hold a place at this event.',
    );

    await expectLater(
      BookingsApi(api.client).book('tech-meetup'),
      throwsA(
        isA<ApiException>()
            .having((error) => error.code, 'code', 'already_booked')
            .having((error) => error.status, 'status', 409),
      ),
    );
  });

  test('a report posts the target and reason', () async {
    api.onJson('POST', '/reports', envelope(reportPayload), status: 201);

    final report = await ReportsApi(api.client).create(
      targetType: 'Job',
      targetId: '0f4a1c2e-1111-2222-3333-444455556666',
      reason: 'Spam',
      note: '   posted five times   ',
    );

    final request = api.requestsWhere('POST', '/reports').single;
    expect(request.jsonBody, <String, Object?>{
      'targetType': 'Job',
      'targetId': '0f4a1c2e-1111-2222-3333-444455556666',
      'reason': 'Spam',
      'note': 'posted five times',
    });
    expect(report.status, 'Open');
    expect(report.targetType, 'Job');
  });

  test('an empty note is left out of the report body', () async {
    api.onJson('POST', '/reports', envelope(reportPayload), status: 201);

    await ReportsApi(api.client).create(
      targetType: 'Event',
      targetId: '0f4a1c2e-1111-2222-3333-444455556666',
      reason: 'Other',
      note: '   ',
    );

    final body = api.requestsWhere('POST', '/reports').single.jsonBody!;
    expect(body.containsKey('note'), isFalse);
    expect(body['targetType'], 'Event');
  });

  test('the reason and target labels cover every reportable module', () {
    expect(reportReasons.keys, containsAll(<String>[
      'Spam',
      'Misleading',
      'Inappropriate',
      'Broken',
      'Other',
    ]));
    expect(reportTargetLabels.keys, <String>[
      'Job',
      'Business',
      'Deal',
      'Event',
      'StudentResource',
    ]);
  });
}
