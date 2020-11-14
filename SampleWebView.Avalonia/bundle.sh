create_app_structure() {
    local APPNAME=$1
    local APPDIR="$2/$APPNAME.app/Contents"
    local APPICONS="/System/Library/CoreServices/CoreTypes.bundle/Contents/Resources/GenericApplicationIcon.icns"

    if [ ! -d "$APPDIR" ]; then
        echo "creating app structure $APPDIR"

        mkdir -vp "$APPDIR"/{MacOS,Resources,Frameworks}
        cp -v "$APPICONS" "$APPDIR/Resources/$APPNAME.icns"
    fi
}

emit_plist() {
    local PLIST_APPNAME=$1
    local PLIST_PATH="$2/Info.plist" 
    if [ ! -f "$PLIST_PATH" ]; then
        echo "emiting $PLIST_PATH"
        cat <<EOF > "$PLIST_PATH"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>$PLIST_APPNAME</string>
    <key>CFBundleGetInfoString</key>
    <string>$PLIST_APPNAME</string>
    <key>CFBundleIconFile</key>
    <string>$PLIST_APPNAME</string>
    <key>CFBundleName</key>
    <string>$PLIST_APPNAME</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleSignature</key>
    <string>4242</string>
</dict>
</plist>
EOF
    fi
}

TARGET="tmp"
if [ ! -d "$TARGET" ]; then
    mkdir "$TARGET"
fi

cd "$TARGET"

CEFZIP="cef75.tar.bz2"
CEFBINARIES="cef_binaries"
if [ ! -f "$CEFZIP" ]; then
    echo "downloading cef binaries"
    curl -o "$CEFZIP" "http://opensource.spotify.com/cefbuilds/cef_binary_75.1.14%2Bgc81164e%2Bchromium-75.0.3770.100_macosx64_minimal.tar.bz2"
fi

if [ ! -d "$CEFBINARIES" ]; then
    echo "unzipping cef binaries"
    mkdir "$CEFBINARIES"
    tar -jxvf "$CEFZIP" -C "./$CEFBINARIES"
fi

CEFFRAMEWORK_DIR="$(find $CEFBINARIES -name "Release")"

APPNAME="SampleWebView.Avalonia"
APPDIR="$APPNAME.app/Contents"
APPFRAMEWORKSDIR="$APPDIR/Frameworks"
APPHELPERNAME="$APPNAME Helper"
APPHELPERDIR="$APPFRAMEWORKSDIR/$APPHELPERNAME.app/Contents"
CONFIG="Debug"
NETTARGET="netcoreapp3.1"

create_app_structure "$APPNAME" .
emit_plist "$APPNAME" "$APPDIR"

create_app_structure "$APPHELPERNAME" "$APPFRAMEWORKSDIR"
emit_plist "$APPHELPERNAME" "$APPHELPERDIR"

echo "Copy CEF binaries"
cp -R "$CEFFRAMEWORK_DIR/Chromium Embedded Framework.framework" "$APPFRAMEWORKSDIR"
cp "$APPFRAMEWORKSDIR/Chromium Embedded Framework.framework/Chromium Embedded Framework" "$APPDIR/MacOS/libcef"
cp -R "$CEFFRAMEWORK_DIR/Chromium Embedded Framework.framework" "$APPHELPERDIR/Frameworks"
cp "$APPFRAMEWORKSDIR/Chromium Embedded Framework.framework/Chromium Embedded Framework" "$APPHELPERDIR/MacOS/libcef"

echo "Copy App binaries"
rsync -a "../bin/$CONFIG/$NETTARGET/" "$APPDIR/MacOS/"
cp "$APPDIR/MacOS/x64/Xilium"*.dll "$APPDIR/MacOS/"
cp "$APPDIR/MacOS/x64/Xilium.CefGlue.BrowserProcess.exe" "$APPHELPERDIR/MacOS/Xilium.CefGlue.BrowserProcess.dll"
rm -rf "$APPDIR/MacOS/x64"
cp -R "$APPDIR/MacOS/" "$APPHELPERDIR/MacOS/"
cp -R "../resources/" "$APPHELPERDIR/MacOS/"


chmod +x "$APPHELPERDIR/MacOS/$APPHELPERNAME"






