import 'package:flutter/material.dart';

class NhPalette {
  NhPalette._();

  static const Color crimson50 = Color(0xFFFEF2F3);
  static const Color crimson100 = Color(0xFFFDE4E6);
  static const Color crimson200 = Color(0xFFFBCBD0);
  static const Color crimson500 = Color(0xFFDC143C);
  static const Color crimson600 = Color(0xFFC01033);
  static const Color crimson700 = Color(0xFFA10C2A);

  static const Color navy50 = Color(0xFFF2F5FB);
  static const Color navy100 = Color(0xFFE2E9F6);
  static const Color navy200 = Color(0xFFC3D0E9);
  static const Color navy500 = Color(0xFF002472);
  static const Color navy600 = Color(0xFF001C58);

  static const Color green50 = Color(0xFFEDFDF3);
  static const Color green500 = Color(0xFF128A4C);
  static const Color green700 = Color(0xFF0B5C32);

  static const Color amber50 = Color(0xFFFFFAEB);
  static const Color amber500 = Color(0xFFB45309);

  static const Color slate0 = Color(0xFFFFFFFF);
  static const Color slate50 = Color(0xFFF7F8FA);
  static const Color slate100 = Color(0xFFEEF0F4);
  static const Color slate200 = Color(0xFFDFE3EA);
  static const Color slate400 = Color(0xFF9AA3B2);
  static const Color slate600 = Color(0xFF55606F);
  static const Color slate800 = Color(0xFF232A35);
  static const Color slate900 = Color(0xFF12171F);
  static const Color slate950 = Color(0xFF0B0E13);
}

class NhDarkPalette {
  NhDarkPalette._();

  static const Color bg = Color(0xFF0B0E13);
  static const Color surface = Color(0xFF12171F);
  static const Color surfaceMuted = Color(0xFF232A35);
  static const Color border = Color(0xFF2A323D);
  static const Color borderStrong = Color(0xFF3C4655);
  static const Color text = Color(0xFFEEF1F6);
  static const Color textMuted = Color(0xFFA3ADBD);
  static const Color primary = Color(0xFFFF5C78);
  static const Color primaryHover = Color(0xFFFF8095);
  static const Color primarySoft = Color(0xFF2B1119);
  static const Color accent = Color(0xFF8FA9E8);
  static const Color accentSoft = Color(0xFF131C30);
  static const Color success = Color(0xFF35C07A);
  static const Color successSoft = Color(0xFF0D2A1C);
  static const Color successText = Color(0xFF7EE2AB);
  static const Color warning = Color(0xFFEAB308);
  static const Color warningSoft = Color(0xFF2A1F08);
  static const Color danger = Color(0xFFFF6B83);
  static const Color dangerSoft = Color(0xFF2C1219);
  static const Color focusRing = Color(0xFF8FA9E8);
}

class NhSpace {
  NhSpace._();

  static const double s0 = 0;
  static const double s1 = 4;
  static const double s2 = 8;
  static const double s3 = 12;
  static const double s4 = 16;
  static const double s5 = 24;
  static const double s6 = 32;
  static const double s7 = 48;
  static const double s8 = 64;
}

class NhRadius {
  NhRadius._();

  static const double sm = 6;
  static const double md = 10;
  static const double lg = 16;
  static const double pill = 999;
}

class NhFont {
  NhFont._();

  static const double xs = 12;
  static const double sm = 14;
  static const double md = 16;
  static const double lg = 18;
  static const double xl = 22;
  static const double xxl = 28;
  static const double xxxl = 36;
}

class NhMotion {
  NhMotion._();

  static const Duration fast = Duration(milliseconds: 120);
  static const Duration base = Duration(milliseconds: 180);
}

@immutable
class NhSemantic {
  const NhSemantic({
    required this.brightness,
    required this.bg,
    required this.surface,
    required this.surfaceMuted,
    required this.border,
    required this.borderStrong,
    required this.text,
    required this.textMuted,
    required this.textInverse,
    required this.primary,
    required this.primaryHover,
    required this.primarySoft,
    required this.accent,
    required this.accentSoft,
    required this.success,
    required this.successSoft,
    required this.successText,
    required this.warning,
    required this.warningSoft,
    required this.danger,
    required this.dangerSoft,
    required this.focusRing,
    required this.shadowColor,
  });

  final Brightness brightness;
  final Color bg;
  final Color surface;
  final Color surfaceMuted;
  final Color border;
  final Color borderStrong;
  final Color text;
  final Color textMuted;
  final Color textInverse;
  final Color primary;
  final Color primaryHover;
  final Color primarySoft;
  final Color accent;
  final Color accentSoft;
  final Color success;
  final Color successSoft;
  final Color successText;
  final Color warning;
  final Color warningSoft;
  final Color danger;
  final Color dangerSoft;
  final Color focusRing;
  final Color shadowColor;

  static const NhSemantic light = NhSemantic(
    brightness: Brightness.light,
    bg: NhPalette.slate50,
    surface: NhPalette.slate0,
    surfaceMuted: NhPalette.slate100,
    border: NhPalette.slate200,
    borderStrong: NhPalette.slate400,
    text: NhPalette.slate900,
    textMuted: NhPalette.slate600,
    textInverse: NhPalette.slate0,
    primary: NhPalette.crimson500,
    primaryHover: NhPalette.crimson600,
    primarySoft: NhPalette.crimson50,
    accent: NhPalette.navy500,
    accentSoft: NhPalette.navy50,
    success: NhPalette.green500,
    successSoft: NhPalette.green50,
    successText: NhPalette.green700,
    warning: NhPalette.amber500,
    warningSoft: NhPalette.amber50,
    danger: NhPalette.crimson600,
    dangerSoft: NhPalette.crimson50,
    focusRing: NhPalette.navy500,
    shadowColor: Color(0x140B0E13),
  );

  static const NhSemantic dark = NhSemantic(
    brightness: Brightness.dark,
    bg: NhDarkPalette.bg,
    surface: NhDarkPalette.surface,
    surfaceMuted: NhDarkPalette.surfaceMuted,
    border: NhDarkPalette.border,
    borderStrong: NhDarkPalette.borderStrong,
    text: NhDarkPalette.text,
    textMuted: NhDarkPalette.textMuted,
    textInverse: NhPalette.slate950,
    primary: NhDarkPalette.primary,
    primaryHover: NhDarkPalette.primaryHover,
    primarySoft: NhDarkPalette.primarySoft,
    accent: NhDarkPalette.accent,
    accentSoft: NhDarkPalette.accentSoft,
    success: NhDarkPalette.success,
    successSoft: NhDarkPalette.successSoft,
    successText: NhDarkPalette.successText,
    warning: NhDarkPalette.warning,
    warningSoft: NhDarkPalette.warningSoft,
    danger: NhDarkPalette.danger,
    dangerSoft: NhDarkPalette.dangerSoft,
    focusRing: NhDarkPalette.focusRing,
    shadowColor: Color(0x66000000),
  );
}
