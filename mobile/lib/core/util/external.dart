import 'package:flutter/material.dart';
import 'package:url_launcher/url_launcher.dart';

Future<void> openExternal(BuildContext context, String url) async {
  final messenger = ScaffoldMessenger.of(context);
  final uri = Uri.tryParse(url.trim());
  if (uri == null || !uri.hasScheme) {
    messenger.showSnackBar(SnackBar(content: Text('This link is not available: $url')));
    return;
  }
  try {
    final opened = await launchUrl(uri, mode: LaunchMode.externalApplication);
    if (!opened) {
      messenger.showSnackBar(SnackBar(content: Text('Could not open $url')));
    }
  } catch (_) {
    messenger.showSnackBar(SnackBar(content: Text('Could not open $url')));
  }
}
