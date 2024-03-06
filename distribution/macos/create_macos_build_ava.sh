#!/bin/bash

set -e

if [ "$#" -lt 7 ]; then
    echo "usage <BASE_DIR> <TEMP_DIRECTORY> <OUTPUT_DIRECTORY> <ENTITLEMENTS_FILE_PATH> <VERSION> <SOURCE_REVISION_ID> <CONFIGURATION> <EXTRA_ARGS>"
    exit 1
fi

mkdir -p "$1"
mkdir -p "$2"
mkdir -p "$3"

BASE_DIR=$(readlink -f "$1")
TEMP_DIRECTORY=$(readlink -f "$2")
OUTPUT_DIRECTORY=$(readlink -f "$3")
ENTITLEMENTS_FILE_PATH=$(readlink -f "$4")
VERSION=$5
SOURCE_REVISION_ID=$6
CONFIGURATION=$7
EXTRA_ARGS=$8

if [ "$VERSION" == "1.1.0" ];
then
  RELEASE_TAR_FILE_NAME=ryujinx-$CONFIGURATION-$VERSION+$SOURCE_REVISION_ID-macos_universal.app.tar
else
  RELEASE_TAR_FILE_NAME=ryujinx-$VERSION-macos_universal.app.tar
fi

ARM64_APP_BUNDLE="$TEMP_DIRECTORY/output_arm64/Ryujinx.app"
X64_APP_BUNDLE="$TEMP_DIRECTORY/output_x64/Ryujinx.app"
UNIVERSAL_APP_BUNDLE="$OUTPUT_DIRECTORY/Ryujinx.app"
EXECUTABLE_SUB_PATH=Contents/MacOS/Ryujinx

rm -rf "$TEMP_DIRECTORY"
mkdir -p "$TEMP_DIRECTORY"

DOTNET_COMMON_ARGS=(-p:DebugType=embedded -p:Version="$VERSION" -p:SourceRevisionId="$SOURCE_REVISION_ID" --self-contained true $EXTRA_ARGS)

dotnet restore
dotnet build -c "$CONFIGURATION" src/Ryujinx
dotnet publish -c "$CONFIGURATION" -r osx-arm64 -o "$TEMP_DIRECTORY/publish_arm64" "${DOTNET_COMMON_ARGS[@]}" src/Ryujinx
dotnet publish -c "$CONFIGURATION" -r osx-x64 -o "$TEMP_DIRECTORY/publish_x64" "${DOTNET_COMMON_ARGS[@]}" src/Ryujinx

# Get rid of the support library for ARMeilleure for x64 (that's only for arm64)
rm -rf "$TEMP_DIRECTORY/publish_x64/libarmeilleure-jitsupport.dylib"

# Get rid of libsoundio from arm64 builds as we don't have a arm64 variant
# TODO: remove this once done
rm -rf "$TEMP_DIRECTORY/publish_arm64/libsoundio.dylib"

pushd "$BASE_DIR/distribution/macos"
./create_app_bundle.sh "$TEMP_DIRECTORY/publish_x64" "$TEMP_DIRECTORY/output_x64" "$ENTITLEMENTS_FILE_PATH"
./create_app_bundle.sh "$TEMP_DIRECTORY/publish_arm64" "$TEMP_DIRECTORY/output_arm64" "$ENTITLEMENTS_FILE_PATH"
popd

rm -rf "$UNIVERSAL_APP_BUNDLE"
mkdir -p "$OUTPUT_DIRECTORY"

# Let's copy one of the two different app bundle and remove the executable
cp -R "$ARM64_APP_BUNDLE" "$UNIVERSAL_APP_BUNDLE"
rm "$UNIVERSAL_APP_BUNDLE/$EXECUTABLE_SUB_PATH"

# Make it libraries universal
python3 "$BASE_DIR/distribution/macos/construct_universal_dylib.py" "$ARM64_APP_BUNDLE" "$X64_APP_BUNDLE" "$UNIVERSAL_APP_BUNDLE" "**/*.dylib"

if ! [ -x "$(command -v lipo)" ];
then
    if ! [ -x "$(command -v llvm-lipo-14)" ];
    then
        LIPO=llvm-lipo
    else
        LIPO=llvm-lipo-14
    fi
else
    LIPO=lipo
fi

# Make the executable universal
$LIPO "$ARM64_APP_BUNDLE/$EXECUTABLE_SUB_PATH" "$X64_APP_BUNDLE/$EXECUTABLE_SUB_PATH" -output "$UNIVERSAL_APP_BUNDLE/$EXECUTABLE_SUB_PATH" -create

# Patch up the Info.plist to have appropriate version
sed -r -i.bck "s/\%\%RYUJINX_BUILD_VERSION\%\%/$VERSION/g;" "$UNIVERSAL_APP_BUNDLE/Contents/Info.plist"
sed -r -i.bck "s/\%\%RYUJINX_BUILD_GIT_HASH\%\%/$SOURCE_REVISION_ID/g;" "$UNIVERSAL_APP_BUNDLE/Contents/Info.plist"
rm "$UNIVERSAL_APP_BUNDLE/Contents/Info.plist.bck"

# Now sign it
if ! [ -x "$(command -v codesign)" ];
then
    if ! [ -x "$(command -v rcodesign)" ];
    then
        echo "Cannot find rcodesign on your system, please install rcodesign."
        exit 1
    fi

    # NOTE: Currently require https://github.com/indygreg/apple-platform-rs/pull/44 to work on other OSes.
    # cargo install --git "https://github.com/marysaka/apple-platform-rs" --branch "fix/adhoc-app-bundle" apple-codesign --bin "rcodesign"
    echo "Using rcodesign for ad-hoc signing"
    rcodesign sign --entitlements-xml-path "$ENTITLEMENTS_FILE_PATH" "$UNIVERSAL_APP_BUNDLE"
else
    echo "Using codesign for ad-hoc signing"
    codesign --entitlements "$ENTITLEMENTS_FILE_PATH" -f --deep -s - "$UNIVERSAL_APP_BUNDLE"
fi

echo "Creating archive"
pushd "$OUTPUT_DIRECTORY"
tar --exclude "Ryujinx.app/Contents/MacOS/Ryujinx" -cvf "$RELEASE_TAR_FILE_NAME" Ryujinx.app 1> /dev/null
python3 "$BASE_DIR/distribution/misc/add_tar_exec.py" "$RELEASE_TAR_FILE_NAME" "Ryujinx.app/Contents/MacOS/Ryujinx" "Ryujinx.app/Contents/MacOS/Ryujinx"
gzip -9 < "$RELEASE_TAR_FILE_NAME" > "$RELEASE_TAR_FILE_NAME.gz"
rm "$RELEASE_TAR_FILE_NAME"

# Create legacy update package for Avalonia to not left behind old testers.
if [ "$VERSION" != "1.1.0" ];
then
    cp $RELEASE_TAR_FILE_NAME.gz test-ava-ryujinx-$VERSION-macos_universal.app.tar.gz
fi

popd

echo "Done"