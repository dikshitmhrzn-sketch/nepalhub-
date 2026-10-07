final RegExp _emailPattern = RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$');
final RegExp _phonePattern = RegExp(r'^\+?[0-9][0-9\s\-()]{5,19}$');
final RegExp _passwordPattern =
    RegExp(r'^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,100}$');

String? emailValidator(String? value) {
  final email = (value ?? '').trim();
  if (email.isEmpty) {
    return 'Email is required.';
  }
  if (!_emailPattern.hasMatch(email)) {
    return 'Enter a valid email address.';
  }
  return null;
}

String? passwordValidator(String? value) {
  final password = value ?? '';
  if (password.isEmpty) {
    return 'Password is required.';
  }
  if (!_passwordPattern.hasMatch(password)) {
    return 'Use at least 8 characters with upper case, lower case, a digit and a symbol.';
  }
  return null;
}

String? signInPasswordValidator(String? value) {
  if ((value ?? '').isEmpty) {
    return 'Password is required.';
  }
  return null;
}

String? fullNameValidator(String? value) {
  final name = (value ?? '').trim();
  if (name.isEmpty) {
    return 'Full name is required.';
  }
  if (name.length < 2) {
    return 'Full name must be at least 2 characters.';
  }
  if (name.length > 120) {
    return 'Full name must be at most 120 characters.';
  }
  return null;
}

String? phoneNumberValidator(String? value) {
  final phone = (value ?? '').trim();
  if (phone.isEmpty) {
    return null;
  }
  if (!_phonePattern.hasMatch(phone)) {
    return 'Enter a valid phone number.';
  }
  return null;
}

String? confirmationValidator(String? value, String password) {
  if ((value ?? '') != password) {
    return 'Passwords do not match.';
  }
  return null;
}
