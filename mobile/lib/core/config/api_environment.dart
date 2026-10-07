class ApiEnvironment {
  ApiEnvironment._();

  static const String apiBaseUrl = String.fromEnvironment(
    'API_BASE_URL',
    defaultValue: 'http://10.0.2.2:5080/api',
  );

  static const String appName = 'Nepal Hub';

  static const Duration requestTimeout = Duration(seconds: 15);
}
