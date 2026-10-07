import 'package:intl/intl.dart';

final RegExp _camelBoundary = RegExp(r'(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])');

DateTime wallClock(String value) {
  final trimmed = value.trim();
  final head = trimmed.length > 19 ? trimmed.substring(0, 19) : trimmed;
  return DateTime.tryParse(head) ?? DateTime.tryParse(trimmed) ?? DateTime(1970);
}

String formatDay(String value) => DateFormat('d MMM y').format(wallClock(value));

String formatDayShort(String value) => DateFormat('d MMM').format(wallClock(value));

String formatDateTime(String value) => DateFormat('d MMM y, h:mm a').format(wallClock(value));

String formatTime(String value) => DateFormat('h:mm a').format(wallClock(value));

String formatRange(String from, String to) {
  final start = wallClock(from);
  final end = wallClock(to);
  if (start.year == end.year && start.month == end.month && start.day == end.day) {
    return '${formatDay(from)}, ${formatTime(from)} - ${formatTime(to)}';
  }
  return '${formatDateTime(from)} - ${formatDateTime(to)}';
}

String humanize(String value) => value.replaceAll(_camelBoundary, ' ');

String npr(num value) => 'Rs ${NumberFormat('#,###').format(value)}';
