import 'package:flutter/cupertino.dart';
import 'package:flutter/material.dart';

import 'tokens.dart';

class NhTheme {
  NhTheme._();

  static ThemeData light() => build(NhSemantic.light);
  static ThemeData dark() => build(NhSemantic.dark);

  static ThemeData build(NhSemantic c) {
    final scheme = ColorScheme(
      brightness: c.brightness,
      primary: c.primary,
      onPrimary: c.textInverse,
      secondary: c.accent,
      onSecondary: c.textInverse,
      error: c.danger,
      onError: c.textInverse,
      surface: c.surface,
      onSurface: c.text,
      onSurfaceVariant: c.textMuted,
      primaryContainer: c.primarySoft,
      onPrimaryContainer: c.primary,
      secondaryContainer: c.accentSoft,
      onSecondaryContainer: c.accent,
      errorContainer: c.dangerSoft,
      onErrorContainer: c.danger,
      outline: c.borderStrong,
      outlineVariant: c.border,
      shadow: c.shadowColor,
      inverseSurface: c.text,
      onInverseSurface: c.surface,
      inversePrimary: c.primary,
      surfaceContainerHighest: c.surfaceMuted,
      surfaceContainerHigh: c.surfaceMuted,
      surfaceContainer: c.surface,
      surfaceContainerLow: c.surface,
      surfaceContainerLowest: c.surface,
    );

    final base = ThemeData(
      useMaterial3: true,
      brightness: c.brightness,
      colorScheme: scheme,
      scaffoldBackgroundColor: c.bg,
      splashFactory: InkSparkle.splashFactory,
    );

    final textTheme = base.textTheme.apply(
      bodyColor: c.text,
      displayColor: c.text,
    );

    return base.copyWith(
      textTheme: textTheme,
      appBarTheme: AppBarTheme(
        backgroundColor: c.surface,
        foregroundColor: c.text,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        scrolledUnderElevation: 1,
        titleTextStyle: textTheme.titleLarge?.copyWith(
          color: c.text,
          fontSize: NhFont.xl,
          fontWeight: FontWeight.w700,
          height: 1.25,
        ),
      ),
      cardTheme: CardThemeData(
        color: c.surface,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        margin: EdgeInsets.zero,
        clipBehavior: Clip.antiAlias,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(NhRadius.md),
          side: BorderSide(color: c.border),
        ),
      ),
      dividerTheme: DividerThemeData(
        color: c.border,
        thickness: 1,
        space: 1,
      ),
      listTileTheme: ListTileThemeData(
        contentPadding: const EdgeInsets.symmetric(
          horizontal: NhSpace.s4,
          vertical: NhSpace.s1,
        ),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(NhRadius.md),
        ),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: c.surface,
        hintStyle: TextStyle(color: c.textMuted),
        labelStyle: TextStyle(color: c.textMuted),
        floatingLabelStyle: TextStyle(color: c.primary),
        contentPadding: const EdgeInsets.symmetric(
          horizontal: NhSpace.s3,
          vertical: NhSpace.s3,
        ),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(NhRadius.sm),
          borderSide: BorderSide(color: c.border),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(NhRadius.sm),
          borderSide: BorderSide(color: c.border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(NhRadius.sm),
          borderSide: BorderSide(color: c.focusRing, width: 2),
        ),
        errorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(NhRadius.sm),
          borderSide: BorderSide(color: c.danger),
        ),
        focusedErrorBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(NhRadius.sm),
          borderSide: BorderSide(color: c.danger, width: 2),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: c.primary,
          foregroundColor: c.textInverse,
          minimumSize: const Size(0, 44),
          padding: const EdgeInsets.symmetric(
            horizontal: NhSpace.s4,
            vertical: NhSpace.s3,
          ),
          textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: NhFont.md),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(NhRadius.sm),
          ),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: c.text,
          minimumSize: const Size(0, 44),
          padding: const EdgeInsets.symmetric(
            horizontal: NhSpace.s4,
            vertical: NhSpace.s3,
          ),
          side: BorderSide(color: c.borderStrong),
          textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: NhFont.md),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(NhRadius.sm),
          ),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: c.primary,
          textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: NhFont.md),
        ),
      ),
      navigationBarTheme: NavigationBarThemeData(
        backgroundColor: c.surface,
        surfaceTintColor: Colors.transparent,
        height: 64,
        indicatorColor: c.primarySoft,
        labelTextStyle: WidgetStateProperty.resolveWith(
          (states) => TextStyle(
            fontSize: NhFont.xs,
            fontWeight: states.contains(WidgetState.selected)
                ? FontWeight.w700
                : FontWeight.w500,
            color: states.contains(WidgetState.selected) ? c.primary : c.textMuted,
          ),
        ),
        iconTheme: WidgetStateProperty.resolveWith(
          (states) => IconThemeData(
            color: states.contains(WidgetState.selected) ? c.primary : c.textMuted,
          ),
        ),
      ),
      chipTheme: base.chipTheme.copyWith(
        backgroundColor: c.surfaceMuted,
        side: BorderSide(color: c.border),
        labelStyle: TextStyle(color: c.text, fontSize: NhFont.sm),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(NhRadius.pill),
        ),
      ),
      progressIndicatorTheme: ProgressIndicatorThemeData(color: c.primary),
      snackBarTheme: SnackBarThemeData(
        backgroundColor: c.surfaceMuted,
        contentTextStyle: TextStyle(color: c.text, fontSize: NhFont.sm),
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(NhRadius.sm),
        ),
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: c.surface,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(NhRadius.lg),
          side: BorderSide(color: c.border),
        ),
        titleTextStyle: textTheme.titleLarge?.copyWith(
          color: c.text,
          fontWeight: FontWeight.w700,
        ),
        contentTextStyle: textTheme.bodyMedium?.copyWith(color: c.text),
      ),
      tabBarTheme: TabBarThemeData(
        labelColor: c.primary,
        unselectedLabelColor: c.textMuted,
        indicatorColor: c.primary,
        labelStyle: const TextStyle(fontWeight: FontWeight.w700, fontSize: NhFont.sm),
        unselectedLabelStyle: const TextStyle(fontWeight: FontWeight.w500, fontSize: NhFont.sm),
      ),
      pageTransitionsTheme: const PageTransitionsTheme(
        builders: {
          TargetPlatform.android: CupertinoPageTransitionsBuilder(),
        },
      ),
    );
  }
}
