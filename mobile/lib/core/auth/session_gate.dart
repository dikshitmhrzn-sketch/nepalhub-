import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import 'session_controller.dart';

Future<bool> ensureSignedIn(BuildContext context) async {
  final session = context.read<SessionController>();
  if (session.isAuthenticated) {
    return true;
  }
  await context.push('/login');
  return session.isAuthenticated;
}
