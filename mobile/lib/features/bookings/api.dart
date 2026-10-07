import '../../core/api/api_client.dart';

class Booking {
  const Booking({
    required this.id,
    required this.eventSlug,
    required this.title,
    required this.startsAt,
    required this.quantity,
    required this.status,
    required this.createdAt,
    this.cancelledAt,
  });

  final String id;
  final String eventSlug;
  final String title;
  final String startsAt;
  final int quantity;
  final String status;
  final String createdAt;
  final String? cancelledAt;

  bool get isActive => status == 'Booked';

  factory Booking.fromJson(Map<String, dynamic> json) => Booking(
        id: '${json['id'] ?? ''}',
        eventSlug: '${json['eventSlug'] ?? ''}',
        title: '${json['title'] ?? ''}',
        startsAt: '${json['startsAt'] ?? ''}',
        quantity: (json['quantity'] as num?)?.toInt() ?? 1,
        status: '${json['status'] ?? 'Booked'}',
        createdAt: '${json['createdAt'] ?? ''}',
        cancelledAt: json['cancelledAt'] as String?,
      );
}

class BookingsApi {
  BookingsApi(this._client);

  final ApiClient _client;

  Future<Booking> book(String slug, {int quantity = 1}) =>
      _client.postObject<Booking>(
        '/events/$slug/booking',
        body: <String, Object?>{'quantity': quantity},
        fromJson: Booking.fromJson,
      );

  Future<Booking> cancel(String slug) =>
      _client.deleteObject<Booking>('/events/$slug/booking', fromJson: Booking.fromJson);
}
