#!/bin/bash
set -e

# =======================
# Настройки
# =======================
APP_NAME="UnderMineLauncher"
VERSION="1.0.0"
ARCH="amd64"
PROJECT_PATH="./UnderMine/UnderMine.csproj"
PUBLISH_DIR="./publish"
DEB_ROOT="${APP_NAME}_${VERSION}"
DEB_DIR="${DEB_ROOT}/DEBIAN"
DEB_BIN_DIR="${DEB_ROOT}/usr/bin"
DEB_SHARE_DIR="${DEB_ROOT}/usr/share/${APP_NAME}"
DEB_APP_DIR="${DEB_ROOT}/usr/share/applications"
OUTPUT_DEB="${APP_NAME}_${VERSION}_${ARCH}.deb"

# =======================
# Очистка
# =======================
rm -rf "$PUBLISH_DIR" "$DEB_ROOT" "$OUTPUT_DEB"

# =======================
# Сборка проекта
# =======================
echo "🔨 Публикуем проект..."
dotnet publish "$PROJECT_PATH" \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishTrimmed=true \
  -p:TrimMode=link \
  -o "$PUBLISH_DIR"

# =======================
# Поиск исполняемого файла
# =======================
EXEC_FILE=$(find "$PUBLISH_DIR" -maxdepth 1 -type f -executable | head -n 1)
if [ -z "$EXEC_FILE" ]; then
    echo "❌ Исполняемый файл не найден!"
    exit 1
fi
EXEC_BASENAME=$(basename "$EXEC_FILE")
echo "✅ Исполняемый файл: $EXEC_BASENAME"

# =======================
# Подготовка структуры DEB
# =======================
mkdir -p "$DEB_DIR" "$DEB_BIN_DIR" "$DEB_SHARE_DIR" "$DEB_APP_DIR"

# DEBIAN/control
cat > "$DEB_DIR/control" <<EOF
Package: undermine-launcher
Version: ${VERSION}
Section: games
Priority: optional
Architecture: ${ARCH}
Maintainer: SKUF @minecraft_urfu_underground_bot
Description: UnderMine Launcher for Linux
 A custom Minecraft launcher built with .NET.
EOF

# Копируем только нужные файлы
cp "$EXEC_FILE" "$DEB_SHARE_DIR/"
cp "$PUBLISH_DIR/UnderMine.dll" "$DEB_SHARE_DIR/" 2>/dev/null || true
cp "$PUBLISH_DIR/UnderMine.runtimeconfig.json" "$DEB_SHARE_DIR/"
cp "$PUBLISH_DIR/UnderMine.deps.json" "$DEB_SHARE_DIR/"

# Симлинк в /usr/bin
ln -sf "/usr/share/${APP_NAME}/${EXEC_BASENAME}" "$DEB_BIN_DIR/$APP_NAME"

# Desktop файл
cat > "$DEB_APP_DIR/${APP_NAME}.desktop" <<EOF
[Desktop Entry]
Name=UnderMine Launcher
Exec=${APP_NAME}
Icon=${APP_NAME}
Type=Application
Categories=Game;
StartupNotify=true
EOF

# =======================
# Сборка DEB пакета
# =======================
dpkg-deb --build "$DEB_ROOT" "$OUTPUT_DEB"
echo "✅ DEB пакет создан: $OUTPUT_DEB"

# =======================
# Подписание артефакта Cosign (GitHub OIDC)
# =======================
echo "🔏 Подписание DEB пакета..."
export COSIGN_EXPERIMENTAL="true"

cosign sign-blob "$OUTPUT_DEB" \
  --yes \
  --bundle "$OUTPUT_DEB.bundle.json" \
  --output-signature "$OUTPUT_DEB.sig"

echo "✅ Подписание завершено:"
echo "  - $OUTPUT_DEB.sig"
echo "  - $OUTPUT_DEB.bundle.json"

