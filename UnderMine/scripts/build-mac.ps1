# build-mac.ps1 - Локальная сборка для тестирования

Write-Host "Building UnderMine for macOS..." -ForegroundColor Green

# Очистка предыдущих сборок
if (Test-Path "./publish") {
    Remove-Item -Recurse -Force "./publish"
}
if (Test-Path "./dist") {
    Remove-Item -Recurse -Force "./dist"
}

# Публикация приложения
Write-Host "Publishing application..." -ForegroundColor Yellow
dotnet publish -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:UseAppHost=true -o ./publish/osx-x64

Write-Host "Creating .app bundle..." -ForegroundColor Yellow

# Создание структуры .app вручную
$AppName = "UnderMine"
$AppDir = "./dist/$AppName.app"
$ContentsDir = "$AppDir/Contents"
$MacOSDir = "$ContentsDir/MacOS"
$ResourcesDir = "$ContentsDir/Resources"

# Создание директорий
New-Item -ItemType Directory -Force -Path $MacOSDir
New-Item -ItemType Directory -Force -Path $ResourcesDir

# Копирование файлов
Copy-Item "./publish/osx-x64/*" -Destination $MacOSDir -Recurse

# Создание Info.plist
$InfoPlist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$AppName</string>
    <key>CFBundleDisplayName</key>
    <string>$AppName Launcher</string>
    <key>CFBundleIdentifier</key>
    <string>com.UndergroundUrFU.undermine</string>
    <key>CFBundleVersion</key>
    <string>1.0.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>CFBundleExecutable</key>
    <string>$AppName</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>NSHighResolutionCapable</key>
    <true/>
</dict>
</plist>
"@

$InfoPlist | Out-File -FilePath "$ContentsDir/Info.plist" -Encoding UTF8

Write-Host "✅ macOS .app bundle created: $AppDir" -ForegroundColor Green
Write-Host "📁 Location: ./dist/$AppName.app" -ForegroundColor Cyan
Write-Host "⚠️ Note: Run 'chmod +x UnderMine.app/Contents/MacOS/UnderMine' on macOS" -ForegroundColor Yellow