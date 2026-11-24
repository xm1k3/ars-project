#!/usr/bin/env bash
set -euo pipefail

publish_dir="$1"
version="$2"
output_dir="$3"

root="$(cd "$(dirname "$0")/../.." && pwd)"
app="$output_dir/ARS Studio.app"
iconset="$output_dir/AppIcon.iconset"

rm -rf "$app" "$iconset"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources" "$iconset"

cp -R "$publish_dir/." "$app/Contents/MacOS/"
sed "s/__VERSION__/$version/g" "$root/packaging/macos/Info.plist" > "$app/Contents/Info.plist"

square="$output_dir/icon-square.png"
sips -p 220 220 "$root/src/ArsStudio/Assets/Images/icon.png" --out "$square" > /dev/null
for size in 16 32 128 256 512; do
    sips -z "$size" "$size" "$square" --out "$iconset/icon_${size}x${size}.png" > /dev/null
    sips -z "$((size * 2))" "$((size * 2))" "$square" --out "$iconset/icon_${size}x${size}@2x.png" > /dev/null
done
iconutil -c icns "$iconset" -o "$app/Contents/Resources/AppIcon.icns"
rm -rf "$iconset" "$square"

chmod +x "$app/Contents/MacOS/ArsStudio"
codesign --force --deep --sign - "$app"
