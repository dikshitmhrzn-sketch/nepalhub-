import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:webview_flutter/webview_flutter.dart';

import '../core/api/api_client.dart';
import '../core/api/api_exception.dart';
import '../core/theme/tokens.dart';
import '../core/util/external.dart';
import '../core/util/formats.dart';
import '../core/widgets/error_banner.dart';
import '../core/widgets/state_message.dart';
import '../features/payments/api.dart';

class CheckoutPage extends StatefulWidget {
  const CheckoutPage({super.key, required this.checkout});

  final Checkout? checkout;

  @override
  State<CheckoutPage> createState() => _CheckoutPageState();
}

class _CheckoutPageState extends State<CheckoutPage> {
  Payment? _payment;
  ApiException? _error;
  bool _verifying = false;
  bool _offerPayload = true;
  String? _returnData;
  String? _returnSignature;
  WebViewController? _webView;

  @override
  void initState() {
    super.initState();
    final checkout = widget.checkout;
    if (checkout != null && checkout.needsForm && _webViewAvailable) {
      _webView = _buildWebView(checkout);
    }
  }

  bool get _webViewAvailable {
    try {
      return !kIsWeb && Platform.isAndroid && WebViewPlatform.instance != null;
    } catch (_) {
      return false;
    }
  }

  WebViewController _buildWebView(Checkout checkout) {
    final controller = WebViewController()
      ..setJavaScriptMode(JavaScriptMode.unrestricted)
      ..setNavigationDelegate(
        NavigationDelegate(
          onNavigationRequest: (request) {
            final uri = Uri.tryParse(request.url);
            if (uri != null && uri.path.contains('/return')) {
              _onProviderReturn(uri);
              return NavigationDecision.prevent;
            }
            return NavigationDecision.navigate;
          },
        ),
      );
    controller.loadHtmlString(_checkoutForm(checkout));
    return controller;
  }

  void _onProviderReturn(Uri uri) {
    _returnData = uri.queryParameters['data'];
    _returnSignature = uri.queryParameters['signature'];
    _verify();
  }

  Future<void> _verify() async {
    final checkout = widget.checkout;
    if (checkout == null || _verifying) {
      return;
    }
    final api = PaymentsApi(context.read<ApiClient>());
    setState(() {
      _verifying = true;
      _error = null;
    });
    try {
      final payment = await api.verify(
        reference: checkout.reference,
        data: _offerPayload ? _returnData : null,
        signature: _offerPayload ? _returnSignature : null,
      );
      if (!mounted) {
        return;
      }
      setState(() {
        _payment = payment;
        _verifying = false;
      });
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }
      setState(() {
        _verifying = false;
        _error = error;
        if (error.code == 'payment_signature_invalid') {
          _offerPayload = false;
        }
      });
    }
  }

  String _checkoutForm(Checkout checkout) {
    final buffer = StringBuffer()
      ..writeln('<!doctype html>')
      ..writeln('<html><head><meta charset="utf-8">')
      ..writeln(
        '<meta name="viewport" content="width=device-width, initial-scale=1">',
      )
      ..writeln(
        '<style>body{font-family:sans-serif;padding:24px;color:#12171F}'
        'p{line-height:1.5}</style>',
      )
      ..writeln('</head><body>')
      ..writeln('<p>Taking you to the payment page&hellip;</p>')
      ..writeln('<form id="checkout" method="POST" action="${_escape(checkout.checkoutUrl!)}">');
    checkout.fields!.forEach((name, value) {
      buffer.writeln(
        '<input type="hidden" name="${_escape(name)}" value="${_escape(value)}">',
      );
    });
    buffer
      ..writeln('</form>')
      ..writeln(
        '<script>document.getElementById("checkout").submit();</script>',
      )
      ..writeln('</body></html>');
    return buffer.toString();
  }

  String _escape(String value) => value
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#39;');

  void _done() {
    final navigator = Navigator.of(context);
    if (navigator.canPop()) {
      navigator.pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final webView = _webView;
    return Scaffold(
      appBar: AppBar(title: const Text('Checkout')),
      body: webView != null && widget.checkout?.needsForm == true
          ? WebViewWidget(controller: webView)
          : Center(
              child: SingleChildScrollView(
                padding: const EdgeInsets.all(NhSpace.s5),
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 480),
                  child: _buildContent(theme),
                ),
              ),
            ),
    );
  }

  Widget _buildContent(ThemeData theme) {
    final checkout = widget.checkout;
    final payment = _payment;

    if (payment != null) {
      return _result(theme, payment);
    }
    if (checkout == null) {
      return Column(
        children: [
          const StateMessage(
            tone: StateMessageTone.error,
            title: 'This checkout is no longer available',
            description: 'Start it again from the page you were buying on.',
          ),
          SizedBox(
            width: double.infinity,
            child: OutlinedButton(
              onPressed: _done,
              child: const Text('Close'),
            ),
          ),
        ],
      );
    }
    if (!checkout.needsForm) {
      return Column(
        children: [
          StateMessage(
            tone: StateMessageTone.empty,
            title: 'That payment has already finished',
            description:
                'Reference ${checkout.reference} · ${paymentStatusLabel(checkout.status)}.',
          ),
          SizedBox(
            width: double.infinity,
            child: OutlinedButton(
              onPressed: _done,
              child: const Text('Close'),
            ),
          ),
        ],
      );
    }

    final colors = theme.colorScheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'Complete your payment',
          style: theme.textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w800),
        ),
        const SizedBox(height: NhSpace.s1),
        Text(
          'You are paying through your payment provider. Reference ${checkout.reference}.',
          style: theme.textTheme.bodyMedium
              ?.copyWith(color: colors.onSurfaceVariant),
        ),
        const SizedBox(height: NhSpace.s4),
        FilledButton.icon(
          onPressed: () => openExternal(context, checkout.checkoutUrl!),
          icon: const Icon(Icons.open_in_new_rounded, size: 18),
          label: const Text('Open the payment page'),
        ),
        const SizedBox(height: NhSpace.s2),
        OutlinedButton(
          onPressed: _verifying ? null : _verify,
          child: _verifying
              ? const SizedBox(
                  width: 18,
                  height: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Text('I have paid — check'),
        ),
        if (_error != null) ...[
          const SizedBox(height: NhSpace.s3),
          ErrorBanner.fromApi(_error!),
          if (_error!.code == 'payment_signature_invalid') ...[
            const SizedBox(height: NhSpace.s2),
            TextButton(
              onPressed: _verifying ? null : _verify,
              child: const Text('Ask the provider again'),
            ),
          ],
        ],
        const SizedBox(height: NhSpace.s3),
        Text(
          'A return on its own settles nothing: the payment is confirmed by asking the '
          'provider directly, so a hand-edited address decides nothing.',
          style: theme.textTheme.bodySmall?.copyWith(color: colors.onSurfaceVariant),
        ),
      ],
    );
  }

  Widget _result(ThemeData theme, Payment payment) {
    final colors = theme.colorScheme;
    final settled = payment.isSettled;
    final failed = payment.failed;
    final title = settled
        ? 'Payment confirmed'
        : failed
            ? 'The payment did not go through'
            : 'Still waiting for the provider';
    final description = failed
        ? '${payment.description} — ${npr(payment.amountNpr)}. Nothing was taken for this one.'
        : settled
            ? '${payment.description} — ${npr(payment.amountNpr)}. '
                '${paymentStatusLabel(payment.status)}.'
            : 'Nothing has been settled yet. Asking again is the same question rather '
                'than a second payment.';
    final color = failed ? colors.error : colors.primary;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Icon(
          failed ? Icons.error_outline_rounded : Icons.check_circle_outline_rounded,
          size: 56,
          color: color,
        ),
        const SizedBox(height: NhSpace.s3),
        Text(
          title,
          textAlign: TextAlign.center,
          style: theme.textTheme.titleLarge?.copyWith(fontWeight: FontWeight.w800),
        ),
        const SizedBox(height: NhSpace.s2),
        Text(
          description,
          textAlign: TextAlign.center,
          style: theme.textTheme.bodyMedium?.copyWith(color: colors.onSurfaceVariant),
        ),
        const SizedBox(height: NhSpace.s2),
        Text(
          'Reference ${payment.reference}',
          textAlign: TextAlign.center,
          style: theme.textTheme.bodySmall?.copyWith(color: colors.onSurfaceVariant),
        ),
        const SizedBox(height: NhSpace.s5),
        if (payment.isPending)
          OutlinedButton(
            onPressed: _verifying ? null : _verify,
            child: _verifying
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Text('Check again'),
          ),
        if (_error != null) ...[
          const SizedBox(height: NhSpace.s3),
          ErrorBanner.fromApi(_error!),
        ],
        const SizedBox(height: NhSpace.s3),
        FilledButton(onPressed: _done, child: const Text('Done')),
      ],
    );
  }
}
