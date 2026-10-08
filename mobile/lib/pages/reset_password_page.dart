import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_exception.dart';
import '../core/auth/session_controller.dart';
import '../core/auth/validators.dart';
import '../core/theme/tokens.dart';
import '../core/widgets/error_banner.dart';

/// Where the reset link lands: a one-time address plus token in the url and a fresh
/// password in the form. A refused link — expired, spent, minted for another purpose —
/// is shown as exactly that with the way to ask for a new one, because the link is the
/// only credential this page has and it has already failed. A password the policy
/// refuses leaves the link intact, so the form is simply shown the reason to fix.
class ResetPasswordPage extends StatefulWidget {
  const ResetPasswordPage({super.key, required this.email, required this.token});

  final String email;
  final String token;

  @override
  State<ResetPasswordPage> createState() => _ResetPasswordPageState();
}

class _ResetPasswordPageState extends State<ResetPasswordPage> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _password = TextEditingController();
  final TextEditingController _confirmation = TextEditingController();
  Map<String, String> _fieldErrors = const <String, String>{};
  ApiException? _error;
  bool _obscurePassword = true;
  bool _submitting = false;
  bool _linkRefused = false;
  bool _done = false;

  bool get _incomplete => widget.email.isEmpty || widget.token.isEmpty;

  @override
  void dispose() {
    _password.dispose();
    _confirmation.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    setState(() {
      _submitting = true;
      _fieldErrors = const <String, String>{};
      _error = null;
    });
    try {
      await context.read<SessionController>().resetPassword(
            email: widget.email,
            token: widget.token,
            password: _password.text,
          );
      if (mounted) {
        setState(() => _done = true);
      }
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }
      setState(() {
        _submitting = false;
        _error = error;
        if (error.code == ApiException.tokenInvalid) {
          _linkRefused = true;
        } else {
          _fieldErrors = error.fieldMessages;
        }
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    if (_incomplete || _linkRefused) {
      final refused = _incomplete;
      return Scaffold(
        appBar: AppBar(title: const Text('Reset password')),
        body: Center(
          child: Padding(
            padding: const EdgeInsets.all(NhSpace.s5),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 440),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    refused ? 'This link is incomplete' : 'That link no longer works',
                    style: theme.textTheme.headlineSmall
                        ?.copyWith(fontWeight: FontWeight.w800),
                  ),
                  const SizedBox(height: NhSpace.s1),
                  Text(
                    refused
                        ? 'The address and the token travel together in the link. Ask '
                            'for a new one to get both.'
                        : 'Reset links expire in an hour and can only be spent once. '
                            'Ask for a new one and it will arrive in a moment.',
                    style: theme.textTheme.bodyMedium
                        ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                  ),
                  const SizedBox(height: NhSpace.s4),
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton(
                      onPressed: () => context.go('/forgot-password'),
                      child: const Text('Ask for a new link'),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      );
    }

    if (_done) {
      return Scaffold(
        appBar: AppBar(title: const Text('Reset password')),
        body: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(NhSpace.s5),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 440),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Password changed',
                    style: theme.textTheme.headlineSmall
                        ?.copyWith(fontWeight: FontWeight.w800),
                  ),
                  const SizedBox(height: NhSpace.s1),
                  Text(
                    'The new password is in effect. Anywhere it was still signed in '
                    'has been signed out, so use this one from now on.',
                    style: theme.textTheme.bodyMedium
                        ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                  ),
                  const SizedBox(height: NhSpace.s4),
                  SizedBox(
                    width: double.infinity,
                    child: FilledButton(
                      onPressed: () => context.go('/login'),
                      child: const Text('Sign in with your new password'),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      );
    }

    return Scaffold(
      appBar: AppBar(title: const Text('Reset password')),
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
                    'Set a new password',
                    style: theme.textTheme.headlineSmall
                        ?.copyWith(fontWeight: FontWeight.w800),
                  ),
                  const SizedBox(height: NhSpace.s1),
                  Text(
                    'The link is good for one change. Pick something this account has '
                    'not used before.',
                    style: theme.textTheme.bodyMedium
                        ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                  ),
                  if (_error != null) ...[
                    const SizedBox(height: NhSpace.s4),
                    ErrorBanner.fromApi(_error!),
                  ],
                  const SizedBox(height: NhSpace.s4),
                  TextFormField(
                    controller: _password,
                    obscureText: _obscurePassword,
                    textInputAction: TextInputAction.next,
                    autofillHints: const <String>[AutofillHints.newPassword],
                    decoration: InputDecoration(
                      labelText: 'New password',
                      helperText:
                          'At least 8 characters with upper case, lower case, a digit and a symbol.',
                      suffixIcon: IconButton(
                        tooltip: _obscurePassword ? 'Show password' : 'Hide password',
                        onPressed: () =>
                            setState(() => _obscurePassword = !_obscurePassword),
                        icon: Icon(
                          _obscurePassword
                              ? Icons.visibility_rounded
                              : Icons.visibility_off_rounded,
                        ),
                      ),
                    ),
                    validator: (value) =>
                        _fieldErrors['password'] ?? passwordValidator(value),
                    onChanged: (_) {
                      if (_fieldErrors.containsKey('password')) {
                        setState(() {
                          _fieldErrors = Map<String, String>.from(_fieldErrors)
                            ..remove('password');
                        });
                      }
                    },
                  ),
                  const SizedBox(height: NhSpace.s3),
                  TextFormField(
                    controller: _confirmation,
                    obscureText: _obscurePassword,
                    textInputAction: TextInputAction.done,
                    decoration: const InputDecoration(labelText: 'Confirm new password'),
                    validator: (value) =>
                        confirmationValidator(value, _password.text),
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
                          : const Text('Set the new password'),
                    ),
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
