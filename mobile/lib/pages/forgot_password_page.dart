import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_exception.dart';
import '../core/auth/session_controller.dart';
import '../core/auth/validators.dart';
import '../core/theme/tokens.dart';
import '../core/widgets/error_banner.dart';
import 'check_email_page.dart';

/// Asks for a reset link without ever saying whether the address holds an account: the
/// server answers the same either way, and the visitor lands on the same "check your
/// email" screen for every address that was submitted.
class ForgotPasswordPage extends StatefulWidget {
  const ForgotPasswordPage({super.key});

  @override
  State<ForgotPasswordPage> createState() => _ForgotPasswordPageState();
}

class _ForgotPasswordPageState extends State<ForgotPasswordPage> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _email = TextEditingController();
  Map<String, String> _fieldErrors = const <String, String>{};
  ApiException? _error;
  bool _submitting = false;

  @override
  void dispose() {
    _email.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    final session = context.read<SessionController>();
    final submitted = _email.text.trim();
    setState(() {
      _submitting = true;
      _fieldErrors = const <String, String>{};
      _error = null;
    });
    try {
      await session.forgotPassword(email: submitted);
      if (!mounted) {
        return;
      }
      context.go(
        '/check-email',
        extra: CheckEmailArgs(
          email: submitted,
          purpose: RecoveryMailPurpose.passwordReset,
        ),
      );
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }
      setState(() {
        _submitting = false;
        _error = error;
        _fieldErrors = error.fieldMessages;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      appBar: AppBar(title: const Text('Forgot password')),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(NhSpace.s5),
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 440),
            child: Form(
              key: _formKey,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Forgot your password?',
                    style: theme.textTheme.headlineSmall
                        ?.copyWith(fontWeight: FontWeight.w800),
                  ),
                  const SizedBox(height: NhSpace.s1),
                  Text(
                    'Enter the address on the account and a one-time link to set a new '
                    'password will follow. The link works once and expires in an hour.',
                    style: theme.textTheme.bodyMedium
                        ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                  ),
                  if (_error != null) ...[
                    const SizedBox(height: NhSpace.s4),
                    ErrorBanner.fromApi(_error!),
                  ],
                  const SizedBox(height: NhSpace.s4),
                  TextFormField(
                    controller: _email,
                    keyboardType: TextInputType.emailAddress,
                    textInputAction: TextInputAction.done,
                    autofillHints: const <String>[AutofillHints.email],
                    decoration: const InputDecoration(labelText: 'Email'),
                    validator: (value) =>
                        _fieldErrors['email'] ?? emailValidator(value),
                    onFieldSubmitted: (_) => _submit(),
                  ),
                  const SizedBox(height: NhSpace.s4),
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton(
                      onPressed: _submitting ? null : _submit,
                      child: _submitting
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                          : const Text('Send a reset link'),
                    ),
                  ),
                  const SizedBox(height: NhSpace.s3),
                  Wrap(
                    alignment: WrapAlignment.center,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      Text(
                        'Remembered it?',
                        style: theme.textTheme.bodyMedium
                            ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                      ),
                      TextButton(
                        onPressed: _submitting ? null : () => context.go('/login'),
                        child: const Text('Sign in'),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
