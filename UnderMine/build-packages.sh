#!/bin/bash
set -e

# === Настройки ===
APP_NAME="UnderMineLauncher"
VERSION="1.0.0.0"
ARCH="amd64"

# Определяем каталог публикации
if [ -d "./publish" ]; then
    PUBLISH_DIR="./publish"
elif [ -d "./UnderMine/bin/Release/net9.0/linux-x64/publish" ]; then
    PUBLISH_DIR="./UnderMine/bin/Release/net9.0/linux-x64/publish"
else
    echo "❌ Ошибка: не найден каталог публикации (publish)."
    echo "   Запусти: dotnet publish -c Release -r linux-x64 --self-contained true"
    exit 1
fi

APPDIR="./AppDir"
DEB_ROOT="./${APP_NAME}_${VERSION}"
DEB_DIR="${DEB_ROOT}/DEBIAN"
DEB_BIN_DIR="${DEB_ROOT}/usr/bin"
DEB_SHARE_DIR="${DEB_ROOT}/usr/share/${APP_NAME}"
DEB_APP_DIR="${DEB_ROOT}/usr/share/applications"
OUTPUT_APPIMAGE="${APP_NAME}-${VERSION}-${ARCH}.AppImage"
OUTPUT_DEB="${APP_NAME}_${VERSION}_${ARCH}.deb"

# === Очистка предыдущих сборок ===
rm -rf "${APPDIR}" "${DEB_ROOT}" "${OUTPUT_APPIMAGE}" "${OUTPUT_DEB}"

echo "🔍 Используется папка публикации: ${PUBLISH_DIR}"
ls -la "${PUBLISH_DIR}"

# === Поиск исполняемого файла ===
EXEC_FILE=$(find "${PUBLISH_DIR}" -maxdepth 1 -type f -perm /u+x,g+x,o+x | head -n 1)
if [ -z "$EXEC_FILE" ]; then
    echo "❌ Ошибка: исполняемый файл не найден в ${PUBLISH_DIR}"
    exit 1
fi
EXEC_BASENAME=$(basename "$EXEC_FILE")
echo "✅ Исполняемый файл: $EXEC_BASENAME"

# === Подготовка AppDir ===
echo "🔧 Подготовка AppDir..."
mkdir -p "${APPDIR}/usr/bin"
rsync -a --exclude '*.pdb' "${PUBLISH_DIR}/" "${APPDIR}/usr/bin/"
chmod +x "${APPDIR}/usr/bin/${EXEC_BASENAME}"

cat > "${APPDIR}/AppRun" <<EOF
#!/bin/bash
HERE=\$(dirname "\$(readlink -f "\$0")")
exec "\$HERE/usr/bin/${EXEC_BASENAME}" "\$@"
EOF
chmod +x "${APPDIR}/AppRun"

# === .desktop файл ===
cat > "${APPDIR}/${APP_NAME}.desktop" <<EOF
[Desktop Entry]
Name=UnderMine Launcher
Exec=${APP_NAME}
Icon=${APP_NAME}
Type=Application
Categories=Game;
StartupNotify=true
EOF

convert -size 1x1 xc:none "${APPDIR}/${APP_NAME}.png" 2>/dev/null || touch "${APPDIR}/${APP_NAME}.png"

# === Сборка AppImage ===
echo "📦 Сборка AppImage..."
ARCH=x86_64 appimagetool "${APPDIR}" "${OUTPUT_APPIMAGE}"

# === Сборка DEB ===
echo "📦 Сборка DEB..."
mkdir -p "${DEB_DIR}" "${DEB_BIN_DIR}" "${DEB_SHARE_DIR}" "${DEB_APP_DIR}"

cat > "${DEB_DIR}/control" <<EOF
Package: undermine-launcher
Version: ${VERSION}
Section: games
Priority: optional
Architecture: ${ARCH}
Maintainer: Skuf
Description: UnderMine Launcher for Linux
 A custom Minecraft launcher built with .NET.
EOF

rsync -a --exclude '*.pdb' "${PUBLISH_DIR}/" "${DEB_SHARE_DIR}/"
ln -sf "/usr/share/${APP_NAME}/${EXEC_BASENAME}" "${DEB_BIN_DIR}/${APP_NAME}"

cat > "${DEB_APP_DIR}/${APP_NAME}.desktop" <<EOF
[Desktop Entry]
Name=UnderMine Launcher
Exec=${APP_NAME}
Icon=${APP_NAME}
Type=Application
Categories=Game;
StartupNotify=true
EOF

dpkg-deb --build "${DEB_ROOT}" "${OUTPUT_DEB}"

echo "✅ Сборка завершена!"
echo "  - ${OUTPUT_APPIMAGE}"
echo "  - ${OUTPUT_DEB}"

