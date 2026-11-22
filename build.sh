#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")" && pwd)"
APP_DIR="$ROOT_DIR/app"
SWIFT_SRC="$APP_DIR/AntigravityPet.swift"
APP_BUNDLE="$APP_DIR/AntigravityPet.app"
MACOS_DIR="$APP_BUNDLE/Contents/MacOS"
BIN_PATH="$MACOS_DIR/AntigravityPet"
PLIST_PATH="$APP_BUNDLE/Contents/Info.plist"

if ! command -v swiftc >/dev/null 2>&1; then
  echo "[Antigravity Pet] Error: swiftc not found. Please install Xcode Command Line Tools:"
  echo "  xcode-select --install"
  exit 1
fi

echo "[1/3] Checking running AntigravityPet instances..."
pkill -f "AntigravityPet.app/Contents/MacOS/AntigravityPet" >/dev/null 2>&1 || true
sleep 0.3

echo "[2/3] Compiling native macOS AppKit app (AntigravityPet.swift)..."
mkdir -p "$MACOS_DIR"
swiftc -O "$SWIFT_SRC" -framework AppKit -framework Carbon -o "$BIN_PATH"
chmod +x "$BIN_PATH"

cat > "$PLIST_PATH" <<'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key>
  <string>AntigravityPet</string>
  <key>CFBundleIdentifier</key>
  <string>com.antigravity.pet</string>
  <key>CFBundleName</key>
  <string>AntigravityPet</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>3.1.0</string>
  <key>CFBundleVersion</key>
  <string>3.1.0</string>
  <key>LSUIElement</key>
  <true/>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
EOF

echo "[3/3] Build succeeded: $APP_BUNDLE"

if [[ "${1:-}" == "--launch" || "${1:-}" == "-Launch" ]]; then
  echo "Launching AntigravityPet.app..."
  open -g -a "$APP_BUNDLE"
fi
