#!/bin/bash

set -e

echo "Creating DMG installer..."

APP_NAME="UnderMine"
APP_DIR="$1/$APP_NAME.app"
DMG_NAME="$2"
VOLUME_NAME="${APP_NAME} Installer"

if [ -z "$APP_DIR" ] || [ -z "$DMG_NAME" ]; then
    echo "Usage: $0 <app_directory> <dmg_name>"
    exit 1
fi

if [ ! -d "$APP_DIR" ]; then
    echo "Error: App directory not found: $APP_DIR"
    exit 1
fi

# Create DMG using hdiutil
echo "Creating DMG file..."
hdiutil create \
    -volname "$VOLUME_NAME" \
    -srcfolder "$APP_DIR" \
    -ov \
    -format UDZO \
    "$DMG_NAME"

echo "✅ DMG created: $DMG_NAME"
echo "💾 DMG size: $(du -h "$DMG_NAME" | cut -f1)"