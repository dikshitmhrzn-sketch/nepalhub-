import '../api/api_exception.dart';
import 'reference_cache.dart';

Future<T> cachedLoad<T>({
  required ReferenceCache cache,
  required String key,
  required Future<T> Function() fetch,
  required Object? Function(T value) encode,
  required T Function(Object? raw) decode,
}) async {
  try {
    final fresh = await fetch();
    await cache.write(key, encode(fresh));
    return fresh;
  } on ApiException catch (error) {
    final stored = await cache.read(key);
    if (stored == null) {
      rethrow;
    }
    try {
      return decode(stored);
    } catch (_) {
      throw error;
    }
  }
}
