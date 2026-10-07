import '../../core/api/api_client.dart';
import '../../core/api/paged_result.dart';

const paymentStatusLabels = <String, String>{
  'Pending': 'Waiting for the provider',
  'Succeeded': 'Paid',
  'Failed': 'Not completed',
  'RefundDue': 'Refund owed',
  'Refunded': 'Refunded',
};

const paymentKindLabels = <String, String>{
  'Subscription': 'Plan',
  'EventTicket': 'Event ticket',
};

String paymentStatusLabel(String status) =>
    paymentStatusLabels[status] ?? status;

class PaymentConfig {
  const PaymentConfig({required this.available, required this.provider});

  final bool available;
  final String provider;

  factory PaymentConfig.fromJson(Map<String, dynamic> json) => PaymentConfig(
        available: json['available'] as bool? ?? false,
        provider: '${json['provider'] ?? 'none'}',
      );
}

class Checkout {
  const Checkout({
    required this.reference,
    this.checkoutUrl,
    this.fields,
    required this.status,
  });

  final String reference;
  final String? checkoutUrl;
  final Map<String, String>? fields;
  final String status;

  /// A reference with no form is a payment that already finished one way or another.
  bool get needsForm => checkoutUrl != null && fields != null;

  factory Checkout.fromJson(Map<String, dynamic> json) => Checkout(
        reference: '${json['reference'] ?? ''}',
        checkoutUrl: json['checkoutUrl'] as String?,
        fields: json['fields'] is Map<String, dynamic>
            ? (json['fields'] as Map<String, dynamic>)
                .map((key, value) => MapEntry(key, '$value'))
            : null,
        status: '${json['status'] ?? 'Pending'}',
      );
}

class Payment {
  const Payment({
    required this.reference,
    required this.kind,
    required this.status,
    required this.amountNpr,
    required this.description,
    this.failureCode,
    this.providerReference,
    required this.createdAt,
    this.settledAt,
    this.refundedAt,
  });

  final String reference;
  final String kind;
  final String status;
  final int amountNpr;
  final String description;
  final String? failureCode;
  final String? providerReference;
  final String createdAt;
  final String? settledAt;
  final String? refundedAt;

  bool get isSettled => status == 'Succeeded';
  bool get isPending => status == 'Pending';
  bool get failed => status == 'Failed';

  factory Payment.fromJson(Map<String, dynamic> json) => Payment(
        reference: '${json['reference'] ?? ''}',
        kind: '${json['kind'] ?? 'Subscription'}',
        status: '${json['status'] ?? 'Pending'}',
        amountNpr: (json['amountNpr'] as num?)?.toInt() ?? 0,
        description: '${json['description'] ?? ''}',
        failureCode: json['failureCode'] as String?,
        providerReference: json['providerReference'] as String?,
        createdAt: '${json['createdAt'] ?? ''}',
        settledAt: json['settledAt'] as String?,
        refundedAt: json['refundedAt'] as String?,
      );
}

class PaymentSummary {
  const PaymentSummary({
    required this.reference,
    required this.kind,
    required this.status,
    required this.amountNpr,
    required this.description,
    this.failureCode,
    required this.createdAt,
    this.settledAt,
    this.refundedAt,
  });

  final String reference;
  final String kind;
  final String status;
  final int amountNpr;
  final String description;
  final String? failureCode;
  final String createdAt;
  final String? settledAt;
  final String? refundedAt;

  factory PaymentSummary.fromJson(Map<String, dynamic> json) => PaymentSummary(
        reference: '${json['reference'] ?? ''}',
        kind: '${json['kind'] ?? 'Subscription'}',
        status: '${json['status'] ?? 'Pending'}',
        amountNpr: (json['amountNpr'] as num?)?.toInt() ?? 0,
        description: '${json['description'] ?? ''}',
        failureCode: json['failureCode'] as String?,
        createdAt: '${json['createdAt'] ?? ''}',
        settledAt: json['settledAt'] as String?,
        refundedAt: json['refundedAt'] as String?,
      );
}

class PaymentsApi {
  PaymentsApi(this._client);

  final ApiClient _client;

  Future<PaymentConfig> config() => _client.getObject<PaymentConfig>(
        '/payments/config',
        fromJson: PaymentConfig.fromJson,
      );

  Future<Checkout> checkout({
    required String kind,
    required String slug,
    String? tier,
    int quantity = 1,
  }) {
    final body = <String, Object?>{
      'kind': kind,
      'slug': slug,
      'quantity': quantity,
    };
    if (tier != null) {
      body['tier'] = tier;
    }
    return _client.postObject<Checkout>(
      '/payments/checkout',
      body: body,
      fromJson: Checkout.fromJson,
    );
  }

  Future<Payment> verify({
    required String reference,
    String? data,
    String? signature,
  }) {
    final body = <String, Object?>{'reference': reference};
    if (data != null) {
      body['data'] = data;
    }
    if (signature != null) {
      body['signature'] = signature;
    }
    return _client.postObject<Payment>(
      '/payments/verify',
      body: body,
      fromJson: Payment.fromJson,
    );
  }

  Future<Payment> payment(String reference) => _client.getObject<Payment>(
        '/payments/$reference',
        fromJson: Payment.fromJson,
      );

  Future<PagedResult<PaymentSummary>> myPayments({
    int page = 1,
    int pageSize = 20,
  }) =>
      _client.getPaged<PaymentSummary>(
        '/account/payments',
        query: <String, dynamic>{'page': page, 'pageSize': pageSize},
        fromJson: PaymentSummary.fromJson,
      );
}
