#!/bin/bash
set -e

# === Настройки ===
APP_NAME="UnderMineLauncher"
VERSION="1.0.0.0"
ARCH="amd64"
PUBLISH_DIR="publish"

APPDIR="./AppDir"
DEB_ROOT="./${APP_NAME}_${VERSION}"
DEB_DIR="${DEB_ROOT}/DEBIAN"
DEB_BIN_DIR="${DEB_ROOT}/usr/bin"
DEB_SHARE_DIR="${DEB_ROOT}/usr/share/${APP_NAME}"
DEB_APP_DIR="${DEB_ROOT}/usr/share/applications"
DEB_ICON_DIR="${DEB_ROOT}/usr/share/icons/hicolor/256x256/apps"
OUTPUT_APPIMAGE="${APP_NAME}-${VERSION}-${ARCH}.AppImage"
OUTPUT_DEB="${APP_NAME}_${VERSION}_${ARCH}.deb"

# === Очистка предыдущих сборок ===
rm -rf "${APPDIR}" "${DEB_ROOT}" "${OUTPUT_APPIMAGE}" "${OUTPUT_DEB}"

echo "🔍 Содержимое папки publish:"
ls -l "${PUBLISH_DIR}"

# Найдём реальный исполняемый файл
EXEC_FILE=$(find "${PUBLISH_DIR}" -maxdepth 1 -type f -executable | head -n 1)
if [ -z "$EXEC_FILE" ]; then
    echo "❌ Ошибка: исполняемый файл не найден в ${PUBLISH_DIR}"
    exit 1
fi
EXEC_BASENAME=$(basename "$EXEC_FILE")
echo "✅ Используем исполняемый файл: $EXEC_BASENAME"

# === Подготовка AppDir ===
echo "🔧 Подготовка AppDir..."
mkdir -p "${APPDIR}/usr/bin"
cp -r "${PUBLISH_DIR}/"* "${APPDIR}/usr/bin/"
chmod +x "${APPDIR}/usr/bin/${EXEC_BASENAME}"

cat > "${APPDIR}/AppRun" <<EOF
#!/bin/bash
HERE=\$(dirname "\$(readlink -f "\$0")")
exec "\$HERE/usr/bin/${EXEC_BASENAME}" "\$@"
EOF
chmod +x "${APPDIR}/AppRun"

cat > "${APPDIR}/${APP_NAME}.desktop" <<EOF
[Desktop Entry]
Name=UnderMine Launcher
Exec=${APP_NAME}
Icon=${APP_NAME}
Type=Application
Categories=Game;Utility;
StartupNotify=true
EOF

if [ -f "${PUBLISH_DIR}/icon.png" ]; then
    cp "${PUBLISH_DIR}/icon.png" "${APPDIR}/${APP_NAME}.png"
fi

# === Сборка AppImage ===
echo "📦 Сборка AppImage..."
ARCH=x86_64 appimagetool "${APPDIR}" "${OUTPUT_APPIMAGE}"

# === Сборка DEB ===
echo "📦 Сборка DEB..."
mkdir -p "${DEB_DIR}" "${DEB_BIN_DIR}" "${DEB_SHARE_DIR}" "${DEB_APP_DIR}" "${DEB_ICON_DIR}"

cat > "${DEB_DIR}/control" <<EOF
Package: undermine-launcher
Version: ${VERSION}
Section: games
Priority: optional
Architecture: ${ARCH}
Maintainer: Skuf <you@example.com>
Description: UnderMine Launcher for Linux
 A custom Minecraft launcher built with .NET.
EOF

cp -r "${PUBLISH_DIR}/"* "${DEB_SHARE_DIR}/"
ln -sf "/usr/share/${APP_NAME}/${EXEC_BASENAME}" "${DEB_BIN_DIR}/${APP_NAME}"

cat > "${DEB_APP_DIR}/${APP_NAME}.desktop" <<EOF
[Desktop Entry]
Name=UnderMine Launcher
Exec=${APP_NAME}
Icon=${APP_NAME}
Type=Application
Categories=Game;Utility;
StartupNotify=true
EOF

# Копируем иконку в AppDir с правильным именем для AppImage
if [ -f "UnderMine/icon.png" ]; then
    cp "UnderMine/icon.png" "${APPDIR}/${APP_NAME}.png"
fi


dpkg-deb --build "${DEB_ROOT}" "${OUTPUT_DEB}"

echo "✅ Сборка завершена!"
echo "  - ${OUTPUT_APPIMAGE}"
echo "  - ${OUTPUT_DEB}"

