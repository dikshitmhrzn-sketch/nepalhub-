# nepal_hub

A new Flutter project.

## Getting Started

This project is a starting point for a Flutter application.

A few resources to get you started if this is your first Flutter project:

- [Learn Flutter](https://docs.flutter.dev/get-started/learn-flutter)
- [Write your first Flutter app](https://docs.flutter.dev/get-started/codelab)
- [Flutter learning resources](https://docs.flutter.dev/reference/learning-resources)

For help getting started with Flutter development, view the
[online documentation](https://docs.flutter.dev/), which offers tutorials,
samples, guidance on mobile development, and a full API reference.

## Building for Android

### Where the app points

`ApiEnvironment.apiBaseUrl` is a compile-time constant fed by the `API_BASE_URL`
`--dart-define`, defaulting to `http://10.0.2.2:5080/api` - the emulator's alias for the
host machine, which is what `flutter run` on an emulator wants. Anything else (a real
device, a build handed to someone else, the Play Store) is told the public origin of the
deployed API at build time:

```bash
flutter build appbundle --release --dart-define=API_BASE_URL=https://nepalhub.example.com/api
```

The value is baked in; pointing a shipped build at another server means rebuilding it.

### Signing the release

Release builds are signed from `android/key.properties` when that file exists, and fall
back to the debug key when it does not - so a fresh clone can still `flutter run --release`,
but such a build is not something the Play Store will accept.

1. Generate the keystore once, and keep it and its passwords: they cannot be recovered,
   only reset through Google Play's key-reset process.

   ```bash
   keytool -genkeypair -v -keystore ~/upload-keystore.jks -keyalg RSA \
     -keysize 2048 -validity 10000 -alias upload
   ```

2. Copy `android/key.properties.example` to `android/key.properties` (already git-ignored)
   and fill it in; `storeFile` is absolute, or relative to `android/app`.

3. Build:

   ```bash
   flutter build appbundle --release --dart-define=API_BASE_URL=https://nepalhub.example.com/api
   ```

The bundle lands at `build/app/outputs/bundle/release/app-release.aab` for upload to the
Play Console; `flutter build apk --release ...` instead produces
`build/app/outputs/flutter-apk/app-release.apk` for direct installs.
