#!/bin/bash

set -e

echo "Creating macOS .app bundle..."

APP_NAME="UnderMine"
PUBLISH_DIR="$1"
OUTPUT_DIR="$2"

if [ -z "$PUBLISH_DIR" ] || [ -z "$OUTPUT_DIR" ]; then
    echo "Usage: $0 <publish_dir> <output_dir>"
    exit 1
fi

APP_DIR="$OUTPUT_DIR/$APP_NAME.app"
CONTENTS_DIR="$APP_DIR/Contents"
MACOS_DIR="$CONTENTS_DIR/MacOS"
RESOURCES_DIR="$CONTENTS_DIR/Resources"

# Create directory structure
echo "Creating directory structure..."
rm -rf "$APP_DIR"
mkdir -p "$MACOS_DIR"
mkdir -p "$RESOURCES_DIR"

# Copy published files
echo "Copying published files..."
cp -r "$PUBLISH_DIR"/* "$MACOS_DIR/"

# Create Info.plist
echo "Creating Info.plist..."
cat > "$CONTENTS_DIR/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundleDisplayName</key>
    <string>$APP_NAME Launcher</string>
    <key>CFBundleIdentifier</key>
    <string>com.UndergroundUrFU.undermine</string>
    <key>CFBundleVersion</key>
    <string>1.0.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>CFBundleExecutable</key>
    <string>$APP_NAME</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
EOF

# Make executable
chmod +x "$MACOS_DIR/$APP_NAME"

echo "✅ Created $APP_NAME.app in $OUTPUT_DIR"
echo "📁 App location: $APP_DIR"



