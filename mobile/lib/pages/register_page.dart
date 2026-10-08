import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../core/api/api_exception.dart';
import '../core/auth/session_controller.dart';
import '../core/auth/validators.dart';
import '../core/theme/tokens.dart';
import '../core/widgets/error_banner.dart';
import 'check_email_page.dart';

class RegisterPage extends StatefulWidget {
  const RegisterPage({super.key});

  @override
  State<RegisterPage> createState() => _RegisterPageState();
}

class _RegisterPageState extends State<RegisterPage> {
  final GlobalKey<FormState> _formKey = GlobalKey<FormState>();
  final TextEditingController _fullName = TextEditingController();
  final TextEditingController _email = TextEditingController();
  final TextEditingController _phoneNumber = TextEditingController();
  final TextEditingController _password = TextEditingController();
  final TextEditingController _confirmation = TextEditingController();
  Map<String, String> _fieldErrors = const <String, String>{};
  ApiException? _error;
  bool _obscurePassword = true;
  bool _submitting = false;

  @override
  void dispose() {
    _fullName.dispose();
    _email.dispose();
    _phoneNumber.dispose();
    _password.dispose();
    _confirmation.dispose();
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
    final result = await session.signUp(
      fullName: _fullName.text.trim(),
      email: _email.text.trim(),
      password: _password.text,
      phoneNumber: _phoneNumber.text.trim(),
    );
    if (!mounted) {
      return;
    }
    if (result.user != null) {
      // An address the server wants confirmed has no session yet: the visitor waits on
      // the link instead of landing on a page that would ask the server who they are.
      if (result.user!.isEmailConfirmed) {
        context.go('/account');
      } else {
        context.go(
          '/check-email',
          extra: CheckEmailArgs(
            email: result.user!.email,
            purpose: RecoveryMailPurpose.verification,
          ),
        );
      }
      return;
    }
    final error = result.error!;
    setState(() {
      _submitting = false;
      _error = error;
      _fieldErrors = error.fieldMessages;
    });
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Scaffold(
      appBar: AppBar(title: const Text('Create account')),
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
                    'Join Nepal Hub',
                    style: theme.textTheme.headlineSmall
                        ?.copyWith(fontWeight: FontWeight.w800),
                  ),
                  const SizedBox(height: NhSpace.s1),
                  Text(
                    'One account for listings, resources, quizzes and rewards.',
                    style: theme.textTheme.bodyMedium
                        ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                  ),
                  if (_error != null) ...[
                    const SizedBox(height: NhSpace.s4),
                    ErrorBanner.fromApi(_error!),
                  ],
                  const SizedBox(height: NhSpace.s4),
                  TextFormField(
                    controller: _fullName,
                    textInputAction: TextInputAction.next,
                    textCapitalization: TextCapitalization.words,
                    autofillHints: const <String>[AutofillHints.name],
                    decoration: const InputDecoration(labelText: 'Full name'),
                    validator: (value) =>
                        _fieldErrors['fullName'] ?? fullNameValidator(value),
                    onChanged: (_) => _clearFieldError('fullName'),
                  ),
                  const SizedBox(height: NhSpace.s3),
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
                    controller: _phoneNumber,
                    keyboardType: TextInputType.phone,
                    textInputAction: TextInputAction.next,
                    autofillHints: const <String>[AutofillHints.telephoneNumber],
                    decoration: const InputDecoration(
                      labelText: 'Phone (optional)',
                      hintText: '98XXXXXXXX',
                    ),
                    validator: (value) =>
                        _fieldErrors['phoneNumber'] ?? phoneNumberValidator(value),
                    onChanged: (_) => _clearFieldError('phoneNumber'),
                  ),
                  const SizedBox(height: NhSpace.s3),
                  TextFormField(
                    controller: _password,
                    obscureText: _obscurePassword,
                    textInputAction: TextInputAction.next,
                    autofillHints: const <String>[AutofillHints.newPassword],
                    decoration: InputDecoration(
                      labelText: 'Password',
                      helperText:
                          'At least 8 characters with upper case, lower case, a digit and a symbol.',
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
                        _fieldErrors['password'] ?? passwordValidator(value),
                    onChanged: (_) => _clearFieldError('password'),
                  ),
                  const SizedBox(height: NhSpace.s3),
                  TextFormField(
                    controller: _confirmation,
                    obscureText: _obscurePassword,
                    textInputAction: TextInputAction.done,
                    decoration: const InputDecoration(labelText: 'Confirm password'),
                    validator: (value) =>
                        confirmationValidator(value, _password.text),
                    onFieldSubmitted: (_) => _submit(),
                  ),
                  const SizedBox(height: NhSpace.s3),
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
                          : const Text('Create account'),
                    ),
                  ),
                  const SizedBox(height: NhSpace.s3),
                  Wrap(
                    alignment: WrapAlignment.center,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      Text(
                        'Already have an account?',
                        style: theme.textTheme.bodyMedium
                            ?.copyWith(color: theme.colorScheme.onSurfaceVariant),
                      ),
                      TextButton(
                        onPressed:
                            _submitting ? null : () => context.push('/login'),
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
