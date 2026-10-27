#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
version="$(tr -d '\r\n' < macOS/VERSION)"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo 'Invalid version'; exit 1; }
output="$PWD/artifacts/macOS"
mkdir -p "$output"
app="$output/NovaLand.app"
# Only generated build output, never a user installation.
if [[ -d "$app" ]]; then rm -rf "$app"; fi
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources" "$output/AppIcon.iconset"
sdk="$(xcrun --show-sdk-path --sdk macosx)"
for arch in arm64 x86_64; do
  xcrun swiftc -swift-version 5 -O -sdk "$sdk" -target "${arch}-apple-macosx13.0" \
    -framework AppKit -framework SwiftUI -framework ServiceManagement -framework IOBluetooth \
    macOS/Sources/*.swift -o "$output/NovaLand-$arch"
done
xcrun lipo -create "$output/NovaLand-arm64" "$output/NovaLand-x86_64" -output "$app/Contents/MacOS/NovaLand"
xcrun lipo "$app/Contents/MacOS/NovaLand" -verify_arch arm64 x86_64
cp macOS/Info.plist "$app/Contents/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $version" "$app/Contents/Info.plist"
cp src/NovaLand/Assets/Logo.png "$app/Contents/Resources/Logo.png"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" src/NovaLand/Assets/Logo.png --out "$output/AppIcon.iconset/icon_${size}x${size}.png" > /dev/null
  double=$((size * 2))
  sips -z "$double" "$double" src/NovaLand/Assets/Logo.png --out "$output/AppIcon.iconset/icon_${size}x${size}@2x.png" > /dev/null
done
iconutil -c icns "$output/AppIcon.iconset" -o "$app/Contents/Resources/AppIcon.icns"
plutil -lint "$app/Contents/Info.plist"
codesign --force --sign - --options runtime --entitlements macOS/Entitlements.plist "$app"
codesign --verify --deep --strict "$app"
xcrun swiftc -swift-version 5 macOS/Sources/Core.swift macOS/Tests/CoreTests.swift -o "$output/core-tests"
"$output/core-tests"
cp macOS/README.md "$output/ONCE_BENI_OKU.txt"
ditto -c -k --sequesterRsrc --keepParent "$app" "$output/NovaLand_v${version}_macOS_universal.zip"
(cd "$output" && shasum -a 256 "NovaLand_v${version}_macOS_universal.zip" > SHA256SUMS.txt)
echo "Universal macOS package ready: $output/NovaLand_v${version}_macOS_universal.zip"
