import 'dart:convert';

import 'package:shared_preferences/shared_preferences.dart';

class ReferenceCache {
  const ReferenceCache();

  static const String _prefix = 'reference.';

  static SharedPreferences? _prefs;

  static Future<void> open() async {
    try {
      _prefs = await SharedPreferences.getInstance();
    } catch (_) {
      _prefs = null;
    }
  }

  Future<bool> write(String key, Object? value) async {
    final prefs = _prefs;
    if (prefs == null) {
      return false;
    }
    try {
      return await prefs.setString('$_prefix$key', jsonEncode(value));
    } catch (_) {
      return false;
    }
  }

  Future<Object?> read(String key) async {
    final prefs = _prefs;
    if (prefs == null) {
      return null;
    }
    try {
      final raw = prefs.getString('$_prefix$key');
      if (raw == null) {
        return null;
      }
      return jsonDecode(raw);
    } catch (_) {
      return null;
    }
  }

  Future<bool> remove(String key) async {
    final prefs = _prefs;
    if (prefs == null) {
      return false;
    }
    try {
      return await prefs.remove('$_prefix$key');
    } catch (_) {
      return false;
    }
  }
}
