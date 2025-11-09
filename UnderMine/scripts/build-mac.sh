#!/bin/bash
set -e

APP_NAME="UnderMineLauncher"
PUBLISH_DIR="publish"
APP_DIR="$PUBLISH_DIR/$APP_NAME.app"

# Очистка предыдущих сборок
rm -rf "$PUBLISH_DIR"
mkdir -p "$PUBLISH_DIR"

# Сборка приложения под macOS
dotnet publish -c Release -r osx-x64 --self-contained true -o "$APP_DIR"

# Создаем директорию Contents, если её нет
mkdir -p "$APP_DIR/Contents"

# Создаем Info.plist для macOS
cat > "$APP_DIR/Contents/Info.plist" <<EOL
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundleIdentifier</key>
    <string>com.yourname.$APP_NAME</string>
    <key>CFBundleVersion</key>
    <string>1.0</string>
    <key>CFBundleExecutable</key>
    <string>$APP_NAME</string>
</dict>
</plist>
EOL

# Создаем директорию для DMG
DMG_DIR="$PUBLISH_DIR/dmg"
mkdir -p "$DMG_DIR"

# Путь к DMG
DMG_PATH="$DMG_DIR/$APP_NAME.dmg"

# Убедимся, что create-dmg установлен
brew list create-dmg >/dev/null 2>&1 || brew install create-dmg

# Создаем DMG
create-dmg "$DMG_PATH" "$APP_DIR" \
  --volname "$APP_NAME" \
  --window-pos 200 120 \
  --window-size 500 300 \
  --icon-size 100 \
  --app-drop-link 250 150
  
echo "📦 Проверяем содержимое директории публикации:"
ls -R "$PUBLISH_DIR" || echo "❌ Папка $PUBLISH_DIR не найдена"

echo "📦 Проверяем, где может лежать DMG:"
find . -type f -name "*.dmg" || echo "❌ DMG-файл не найден"


echo "✅ Сборка завершена: $DMG_PATH"



