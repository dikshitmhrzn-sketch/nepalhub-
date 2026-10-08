import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_exception.dart';
import '../core/auth/session_controller.dart';
import '../core/theme/tokens.dart';
import '../core/widgets/error_banner.dart';
import '../core/widgets/state_message.dart';

enum _VerifyPhase { checking, verified, refused }

/// Where the confirmation link in the mail lands: the address and the token travel in
/// the url, and the POST fires as soon as the page opens — a confirmation is something a
/// person does by clicking, not by finding a button. An answer of "verified" is
/// idempotent on the server, so a duplicated link lands on the same screen. A refused
/// link gets a fresh one rather than a reason: the server refuses every reason identically.
class VerifyEmailPage extends StatefulWidget {
  const VerifyEmailPage({super.key, required this.email, required this.token});

  final String email;
  final String token;

  @override
  State<VerifyEmailPage> createState() => _VerifyEmailPageState();
}

class _VerifyEmailPageState extends State<VerifyEmailPage> {
  _VerifyPhase _phase = _VerifyPhase.checking;
  ApiException? _refusal;
  ApiException? _resendError;
  bool _sentAgain = false;
  bool _submitting = false;

  @override
  void initState() {
    super.initState();
    _verify();
  }

  Future<void> _verify() async {
    if (widget.email.isEmpty || widget.token.isEmpty) {
      setState(() => _phase = _VerifyPhase.refused);
      return;
    }
    setState(() => _phase = _VerifyPhase.checking);
    try {
      await context.read<SessionController>().verifyEmail(
            email: widget.email,
            token: widget.token,
          );
      if (mounted) {
        setState(() => _phase = _VerifyPhase.verified);
      }
    } on ApiException catch (error) {
      if (mounted) {
        setState(() {
          _refusal = error;
          _phase = _VerifyPhase.refused;
        });
      }
    }
  }

  Future<void> _sendAgain() async {
    setState(() {
      _submitting = true;
      _resendError = null;
    });
    try {
      await context.read<SessionController>().resendVerification(email: widget.email);
      if (mounted) {
        setState(() => _sentAgain = true);
      }
    } on ApiException catch (error) {
      if (mounted) {
        setState(() => _resendError = error);
      }
    } finally {
      if (mounted) {
        setState(() => _submitting = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: const Text('Confirm email')),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(NhSpace.s5),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 440),
            child: switch (_phase) {
              _VerifyPhase.checking => const StateMessage(
                  tone: StateMessageTone.loading,
                  title: 'Confirming your address…',
                ),
              _VerifyPhase.verified => Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Email confirmed',
                      style: theme.textTheme.headlineSmall
                          ?.copyWith(fontWeight: FontWeight.w800),
                    ),
                    const SizedBox(height: NhSpace.s1),
                    Text(
                      'The address on your account has been checked. You can sign in '
                      'now — everything you saved will be where you left it.',
                      style: theme.textTheme.bodyMedium
                          ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                    ),
                    const SizedBox(height: NhSpace.s4),
                    SizedBox(
                      width: double.infinity,
                      child: FilledButton(
                        onPressed: () => context.go('/login'),
                        child: const Text('Sign in to your account'),
                      ),
                    ),
                  ],
                ),
              _VerifyPhase.refused => Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'That link no longer works',
                      style: theme.textTheme.headlineSmall
                          ?.copyWith(fontWeight: FontWeight.w800),
                    ),
                    const SizedBox(height: NhSpace.s1),
                    Text(
                      'Confirmation links expire and can only be spent once. Ask for a '
                      'new one and it will arrive in a moment.',
                      style: theme.textTheme.bodyMedium
                          ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                    ),
                    if (_refusal != null) ...[
                      const SizedBox(height: NhSpace.s4),
                      ErrorBanner.fromApi(_refusal!),
                    ],
                    if (_resendError != null) ...[
                      const SizedBox(height: NhSpace.s4),
                      ErrorBanner.fromApi(_resendError!),
                    ],
                    const SizedBox(height: NhSpace.s4),
                    if (widget.email.isEmpty)
                      SizedBox(
                        width: double.infinity,
                        child: OutlinedButton(
                          onPressed: () => context.go('/register'),
                          child: const Text('Create an account'),
                        ),
                      )
                    else if (_sentAgain)
                      Text(
                        'A new link is on its way to ${widget.email}. Open it to '
                        'confirm your address.',
                        style: theme.textTheme.bodyMedium,
                      )
                    else
                      SizedBox(
                        width: double.infinity,
                        child: OutlinedButton(
                          onPressed: _submitting ? null : _sendAgain,
                          child: _submitting
                              ? const SizedBox(
                                  width: 18,
                                  height: 18,
                                  child: CircularProgressIndicator(strokeWidth: 2),
                                )
                              : const Text('Send a new link'),
                        ),
                      ),
                    const SizedBox(height: NhSpace.s3),
                    TextButton(
                      onPressed: () => context.go('/login'),
                      child: const Text('Back to sign in'),
                    ),
                  ],
                ),
            },
          ),
        ),
      ),
    );
  }
}
