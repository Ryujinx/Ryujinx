#!/bin/bash

set -e

PUBLISH_DIRECTORY=$1
OUTPUT_DIRECTORY=$2
ENTITLEMENTS_FILE_PATH=$3

APP_BUNDLE_DIRECTORY="$OUTPUT_DIRECTORY/Ryujinx.app"

rm -rf "$APP_BUNDLE_DIRECTORY"
mkdir -p "$APP_BUNDLE_DIRECTORY/Contents"
mkdir "$APP_BUNDLE_DIRECTORY/Contents/Frameworks"
mkdir "$APP_BUNDLE_DIRECTORY/Contents/MacOS"
mkdir "$APP_BUNDLE_DIRECTORY/Contents/Resources"

# Copy executable and nsure executable can be executed
cp "$PUBLISH_DIRECTORY/Ryujinx" "$APP_BUNDLE_DIRECTORY/Contents/MacOS/Ryujinx"
chmod u+x "$APP_BUNDLE_DIRECTORY/Contents/MacOS/Ryujinx"

# Then all libraries
cp "$PUBLISH_DIRECTORY"/*.dylib "$APP_BUNDLE_DIRECTORY/Contents/Frameworks"

# Then resources
cp Info.plist "$APP_BUNDLE_DIRECTORY/Contents"
cp Ryujinx.icns "$APP_BUNDLE_DIRECTORY/Contents/Resources/Ryujinx.icns"
cp updater.sh "$APP_BUNDLE_DIRECTORY/Contents/Resources/updater.sh"
cp -r "$PUBLISH_DIRECTORY/THIRDPARTY.md" "$APP_BUNDLE_DIRECTORY/Contents/Resources"

echo -n "APPL????" > "$APP_BUNDLE_DIRECTORY/Contents/PkgInfo"

# Fixup libraries and executable
python3 bundle_fix_up.py "$APP_BUNDLE_DIRECTORY" MacOS/Ryujinx

# Now sign it
if ! [ -x "$(command -v codesign)" ];
then
    if ! [ -x "$(command -v rcodesign)" ];
    then
        echo "Cannot find rcodesign on your system, please install rcodesign."
        exit 1
    fi

    # cargo install apple-codesign
    echo "Usign rcodesign for ad-hoc signing"
    rcodesign sign --entitlements-xml-path "$ENTITLEMENTS_FILE_PATH" "$APP_BUNDLE_DIRECTORY"
else
    echo "Usign codesign for ad-hoc signing"
    codesign --entitlements "$ENTITLEMENTS_FILE_PATH" -f --deep -s - "$APP_BUNDLE_DIRECTORY"
fi