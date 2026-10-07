import 'package:flutter/foundation.dart';

@immutable
class SessionUser {
  const SessionUser({
    required this.id,
    required this.email,
    required this.fullName,
    required this.isEmailConfirmed,
    required this.level,
    required this.xp,
    required this.roles,
    this.avatarUrl,
  });

  final String id;
  final String email;
  final String fullName;
  final String? avatarUrl;
  final bool isEmailConfirmed;
  final int level;
  final int xp;
  final List<String> roles;

  bool get isAdmin => roles.contains('admin');
  bool get isStaff => isAdmin || roles.contains('moderator');
  bool get ownsBusinesses => isStaff || roles.contains('business');

  factory SessionUser.fromJson(Map<String, dynamic> json) => SessionUser(
        id: '${json['id'] ?? ''}',
        email: '${json['email'] ?? ''}',
        fullName: '${json['fullName'] ?? ''}',
        avatarUrl: json['avatarUrl'] as String?,
        isEmailConfirmed: json['isEmailConfirmed'] as bool? ?? false,
        level: (json['level'] as num?)?.toInt() ?? 1,
        xp: (json['xp'] as num?)?.toInt() ?? 0,
        roles: json['roles'] is List
            ? (json['roles'] as List).map((role) => '$role').toList()
            : const <String>[],
      );
}

@immutable
class SessionResponse {
  const SessionResponse({required this.authenticated, this.user});

  final bool authenticated;
  final SessionUser? user;

  factory SessionResponse.fromJson(Map<String, dynamic> json) => SessionResponse(
        authenticated: json['authenticated'] as bool? ?? false,
        user: json['user'] is Map<String, dynamic>
            ? SessionUser.fromJson(json['user'] as Map<String, dynamic>)
            : null,
      );
}
