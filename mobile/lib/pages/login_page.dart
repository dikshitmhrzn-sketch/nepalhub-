import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_exception.dart';
import '../core/auth/session_controller.dart';
import '../core/auth/validators.dart';
import '../core/theme/tokens.dart';
import '../core/widgets/error_banner.dart';
import 'check_email_page.dart';

class LoginPage extends StatefulWidget {
  const LoginPage({super.key});

  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _email = TextEditingController();
  final TextEditingController _password = TextEditingController();
  Map<String, String> _fieldErrors = const <String, String>{};
  ApiException? _error;
  bool _rememberMe = true;
  bool _obscurePassword = true;
  bool _submitting = false;
  bool _needsConfirmation = false;
  bool _resending = false;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  void _clearFieldError(String field) {
    if (_fieldErrors.containsKey(field)) {
      setState(() {
        _fieldErrors = Map<String, String>.from(_fieldErrors)..remove(field);
      });
    }
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }
    final session = context.read<SessionController>();
    setState(() {
      _submitting = true;
      _fieldErrors = const <String, String>{};
      _error = null;
    });
    final result = await session.signIn(
      email: _email.text.trim(),
      password: _password.text,
      rememberMe: _rememberMe,
    );
    if (!mounted) {
      return;
    }
    if (result.user != null) {
      context.go('/account');
      return;
    }
    final error = result.error!;
    setState(() {
      _submitting = false;
      _error = error;
      _fieldErrors = error.fieldMessages;
      // The password was right — the server checked it before saying this — so the only
      // useful reply is the link the person is missing, not another password attempt.
      _needsConfirmation = error.isEmailNotConfirmed;
    });
  }

  Future<void> _resendConfirmation() async {
    final session = context.read<SessionController>();
    final submitted = _email.text.trim();
    setState(() {
      _resending = true;
      _error = null;
    });
    try {
      await session.resendVerification(email: submitted);
      if (!mounted) {
        return;
      }
      context.go(
        '/check-email',
        extra: CheckEmailArgs(
          email: submitted,
          purpose: RecoveryMailPurpose.verification,
        ),
      );
    } on ApiException catch (error) {
      if (!mounted) {
        return;
      }
      setState(() {
        _resending = false;
        _error = error;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      appBar: AppBar(title: const Text('Sign in')),
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
                    'Welcome back',
                    style: theme.textTheme.headlineSmall
                        ?.copyWith(fontWeight: FontWeight.w800),
                  ),
                  const SizedBox(height: NhSpace.s1),
                  Text(
                    'Sign in to keep your XP, badges, saved listings and payments.',
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
                    textInputAction: TextInputAction.next,
                    autofillHints: const <String>[AutofillHints.email],
                    decoration: const InputDecoration(labelText: 'Email'),
                    validator: (value) =>
                        _fieldErrors['email'] ?? emailValidator(value),
                    onChanged: (_) => _clearFieldError('email'),
                  ),
                  const SizedBox(height: NhSpace.s3),
                  TextFormField(
                    controller: _password,
                    obscureText: _obscurePassword,
                    textInputAction: TextInputAction.done,
                    autofillHints: const <String>[AutofillHints.password],
                    decoration: InputDecoration(
                      labelText: 'Password',
                      suffixIcon: IconButton(
                        tooltip: _obscurePassword ? 'Show password' : 'Hide password',
                        onPressed: () => setState(
                          () => _obscurePassword = !_obscurePassword,
                        ),
                        icon: Icon(
                          _obscurePassword
                              ? Icons.visibility_rounded
                              : Icons.visibility_off_rounded,
                        ),
                      ),
                    ),
                    validator: (value) =>
                        _fieldErrors['password'] ?? signInPasswordValidator(value),
                    onChanged: (_) => _clearFieldError('password'),
                    onFieldSubmitted: (_) => _submit(),
                  ),
                  Align(
                    alignment: Alignment.centerLeft,
                    child: TextButton(
                      onPressed:
                          _submitting ? null : () => context.push('/forgot-password'),
                      child: const Text('Forgot your password?'),
                    ),
                  ),
                  CheckboxListTile(
                    value: _rememberMe,
                    onChanged: _submitting
                        ? null
                        : (value) => setState(() => _rememberMe = value ?? true),
                    contentPadding: EdgeInsets.zero,
                    controlAffinity: ListTileControlAffinity.leading,
                    title: const Text('Keep me signed in'),
                  ),
                  const SizedBox(height: NhSpace.s2),
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
                          : const Text('Sign in'),
                    ),
                  ),
                  if (_needsConfirmation) ...[
                    const SizedBox(height: NhSpace.s2),
                    SizedBox(
                      width: double.infinity,
                      child: OutlinedButton(
                        onPressed: _resending ? null : _resendConfirmation,
                        child: _resending
                            ? const SizedBox(
                                width: 18,
                                height: 18,
                                child: CircularProgressIndicator(strokeWidth: 2),
                              )
                            : const Text('Email me the confirmation link again'),
                      ),
                    ),
                  ],
                  const SizedBox(height: NhSpace.s3),
                  Wrap(
                    alignment: WrapAlignment.center,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      Text(
                        'New to Nepal Hub?',
                        style: theme.textTheme.bodyMedium
                            ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                      ),
                      TextButton(
                        onPressed: _submitting
                            ? null
                            : () => context.push('/register'),
                        child: const Text('Create an account'),
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
