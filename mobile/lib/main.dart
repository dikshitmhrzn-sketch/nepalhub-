import 'package:cookie_jar/cookie_jar.dart';
import 'package:flutter/material.dart';

import 'app.dart';
import 'core/api/api_client.dart';
import 'core/api/secure_cookie_storage.dart';
import 'core/config/api_environment.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  final CookieJar cookieJar = PersistCookieJar(
    storage: SecureCookieStorage(),
    persistSession: true,
  );

  final ApiClient client = ApiClient(
    baseUrl: ApiEnvironment.apiBaseUrl,
    cookieJar: cookieJar,
  );

  runApp(NepalHubApp(client: client));
}
