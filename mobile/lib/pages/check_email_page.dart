import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_exception.dart';
import '../core/auth/session_controller.dart';
import '../core/theme/tokens.dart';
import '../core/widgets/error_banner.dart';

/// What the visitor is waiting on: the confirmation link an account needs before it can
/// sign in, or the one-time link that sets a new password.
enum RecoveryMailPurpose { verification, passwordReset }

/// Carried by the route so the screen knows which endpoint "send it again" re-asks and
/// how to describe the mail that is (or is not) on its way.
class CheckEmailArgs {
  const CheckEmailArgs({required this.email, required this.purpose});

  final String email;
  final RecoveryMailPurpose purpose;
}

/// The screen both roads lead to. It never says whether the address belongs to an
/// account — the server's answer is carried here unchanged — and "send it again" spends
/// the same always-answers endpoint, so a stranger clicking it learns nothing a stranger
/// did not already know.
class CheckEmailPage extends StatefulWidget {
  const CheckEmailPage({super.key, required this.args});

  final CheckEmailArgs args;

  @override
  State<CheckEmailPage> createState() => _CheckEmailPageState();
}

class _CheckEmailPageState extends State<CheckEmailPage> {
  ApiException? _error;
  bool _sentAgain = false;
  bool _submitting = false;

  bool get _isVerification => widget.args.purpose == RecoveryMailPurpose.verification;

  Future<void> _sendAgain() async {
    final session = context.read<SessionController>();
    setState(() {
      _submitting = true;
      _error = null;
    });
    try {
      if (_isVerification) {
        await session.resendVerification(email: widget.args.email);
      } else {
        await session.forgotPassword(email: widget.args.email);
      }
      if (!mounted) {
        return;
      }
      setState(() => _sentAgain = true);
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }
      setState(() {
        _error = error;
        _submitting = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    if (widget.args.email.isEmpty) {
      // A visitor who lands here directly has nothing to check for; sign-in is the
      // nearest useful place to put them.
      return Scaffold(
        appBar: AppBar(title: const Text('Check your email')),
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(NhSpace.s5),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                const Text('Nothing to check for'),
                const SizedBox(height: NhSpace.s3),
                FilledButton(
                  onPressed: () => context.go('/login'),
                  child: const Text('Sign in'),
                ),
              ],
            ),
          ),
        ),
      );
    }

    return Scaffold(
      appBar: AppBar(
        title: Text(_isVerification ? 'Confirm your email' : 'Check your email'),
      ),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(NhSpace.s5),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 440),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  _isVerification ? 'Confirm your email' : 'Check your email',
                  style: theme.textTheme.headlineSmall
                      ?.copyWith(fontWeight: FontWeight.w800),
                ),
                const SizedBox(height: NhSpace.s1),
                Text(
                  _isVerification
                      ? 'A confirmation link is on its way to ${widget.args.email}. '
                          'Open it to finish creating your account, then sign in as usual.'
                      : 'If an account belongs to ${widget.args.email}, a password reset '
                          'link is on its way. It works once and expires in an hour.',
                  style: theme.textTheme.bodyMedium
                      ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                ),
                if (_error != null) ...[
                  const SizedBox(height: NhSpace.s4),
                  ErrorBanner.fromApi(_error!),
                ],
                const SizedBox(height: NhSpace.s4),
                if (_sentAgain)
                  Text(
                    'Another link is on its way. Give it a minute — and if it still has '
                    'not arrived, the address may not have an account on this site.',
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
                          : const Text('Send it again'),
                    ),
                  ),
                const SizedBox(height: NhSpace.s4),
                Wrap(
                  alignment: WrapAlignment.center,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    if (_isVerification) ...[
                      Text(
                        'Already confirmed?',
                        style: theme.textTheme.bodyMedium
                            ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                      ),
                      TextButton(
                        onPressed: () => context.go('/login'),
                        child: const Text('Sign in'),
                      ),
                    ] else ...[
                      TextButton(
                        onPressed: () => context.push('/forgot-password'),
                        child: const Text('Try another address'),
                      ),
                      TextButton(
                        onPressed: () => context.go('/login'),
                        child: const Text('Back to sign in'),
                      ),
                    ],
                  ],
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
