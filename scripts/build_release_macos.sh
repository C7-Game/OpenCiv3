#!/usr/bin/env bash
# build_release_macos.sh — Build, sign, notarize, and release OpenCiv3 for macOS
#
# Usage:
#   ./scripts/build_release_macos.sh [options]
#
# Options:
#   --identity  IDENTITY     Code signing identity (default: auto-detect "Developer ID Application")
#   --profile   PROFILE      Notarytool keychain profile name (default: notarytool-profile)
#   --tag       TAG          Git tag for the release (default: none, skip release)
#   --skip-build             Skip .NET build step
#   --skip-import            Skip Godot resource import step
#   --skip-notarize          Skip notarization (sign only)
#   --skip-release           Skip GitHub release even if --tag is set
#   --entitlements FILE      Custom entitlements plist (default: auto-generate .NET/CoreCLR entitlements)
#   -h, --help               Show this help
#
# Prerequisites:
#   - .NET 8.0 SDK:          brew install dotnet@8
#   - Godot 4.4.1 Mono:      Download .NET build from godotengine.org/download/archive
#   - Export templates:       Download .tpz, extract to ~/Library/Application Support/Godot/export_templates/4.4.1.stable.mono/
#   - Signing identity:       Apple Developer ID certificate in keychain
#   - Notarization creds:     xcrun notarytool store-credentials <profile>
#   - gh CLI (for release):   brew install gh
#
# Pitfalls this script handles for you:
#   - Uses full Godot path (symlinks break .NET assembly resolution)
#   - Uses 'timeout 60 godot --editor' instead of '--headless --import' (hangs on macOS)
#   - Disables Godot's built-in codesign during export (reverts automatically)
#   - Signs ALL Mach-O binaries (not just dylibs — createdump executables too)
#   - Adds .NET/CoreCLR JIT entitlements (hardened runtime blocks JIT without them)
#   - Uses ditto (not zip) for app bundles to avoid AppleDouble files breaking signatures

set -euo pipefail

# ---------------------------------------------------------------------------
# Defaults
# ---------------------------------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
C7_DIR="$PROJECT_DIR/C7"
EXPORTS_DIR="$PROJECT_DIR/Exports/MacOS"
SIGNING_DIR="$EXPORTS_DIR/signing"
GODOT="/Applications/Godot_mono.app/Contents/MacOS/Godot"
APP_NAME="OpenCiv3"

IDENTITY=""
NOTARY_PROFILE="notarytool-profile"
TAG=""
ENTITLEMENTS=""
SKIP_BUILD=false
SKIP_IMPORT=false
SKIP_NOTARIZE=false
SKIP_RELEASE=false

# ---------------------------------------------------------------------------
# Parse arguments
# ---------------------------------------------------------------------------
while [[ $# -gt 0 ]]; do
    case "$1" in
        --identity)   IDENTITY="$2"; shift 2 ;;
        --profile)    NOTARY_PROFILE="$2"; shift 2 ;;
        --tag)        TAG="$2"; shift 2 ;;
        --entitlements) ENTITLEMENTS="$2"; shift 2 ;;
        --skip-build)     SKIP_BUILD=true; shift ;;
        --skip-import)    SKIP_IMPORT=true; shift ;;
        --skip-notarize)  SKIP_NOTARIZE=true; shift ;;
        --skip-release)   SKIP_RELEASE=true; shift ;;
        -h|--help)
            sed -n '2,/^$/s/^# \?//p' "$0"
            exit 0 ;;
        *) echo "Unknown option: $1"; exit 1 ;;
    esac
done

# ---------------------------------------------------------------------------
# Environment — .NET 8.0 is keg-only on Homebrew
# ---------------------------------------------------------------------------
export PATH="/opt/homebrew/opt/dotnet@8/bin:$PATH"
export DOTNET_ROOT="/opt/homebrew/opt/dotnet@8/libexec"

# ---------------------------------------------------------------------------
# Prerequisite checks
# ---------------------------------------------------------------------------
echo "==> Checking prerequisites..."

fail=false
command -v dotnet >/dev/null 2>&1 || { echo "ERROR: dotnet not found. Run: brew install dotnet@8"; fail=true; }
[ -x "$GODOT" ] || { echo "ERROR: Godot Mono not found at $GODOT"; fail=true; }

TEMPLATES_DIR="$HOME/Library/Application Support/Godot/export_templates"
if ! ls "$TEMPLATES_DIR"/*/macos.zip >/dev/null 2>&1; then
    echo "ERROR: No Godot export templates found in $TEMPLATES_DIR"
    fail=true
fi

# Auto-detect signing identity if not provided
if [ -z "$IDENTITY" ]; then
    IDENTITY=$(security find-identity -v -p codesigning | grep "Developer ID Application" | head -1 | sed 's/.*"\(.*\)"/\1/')
    if [ -z "$IDENTITY" ]; then
        echo "ERROR: No 'Developer ID Application' identity found in keychain"
        fail=true
    else
        echo "    Using identity: $IDENTITY"
    fi
fi

if [ "$SKIP_NOTARIZE" = false ]; then
    xcrun notarytool history --keychain-profile "$NOTARY_PROFILE" >/dev/null 2>&1 || {
        echo "ERROR: Notarization profile '$NOTARY_PROFILE' not found. Run: xcrun notarytool store-credentials $NOTARY_PROFILE"
        fail=true
    }
fi

if [ "$fail" = true ]; then
    echo "Fix the above errors and try again."
    exit 1
fi
echo "    All prerequisites OK"

# ---------------------------------------------------------------------------
# Step 1: Build .NET assemblies
# ---------------------------------------------------------------------------
if [ "$SKIP_BUILD" = false ]; then
    echo ""
    echo "==> Building .NET assemblies..."
    dotnet build "$C7_DIR/C7.sln" -c Release
else
    echo ""
    echo "==> Skipping .NET build (--skip-build)"
fi

# ---------------------------------------------------------------------------
# Step 2: Import resources (editor must run once to populate .godot/imported/)
# ---------------------------------------------------------------------------
if [ "$SKIP_IMPORT" = false ]; then
    if [ -d "$C7_DIR/.godot/imported" ] && [ "$(ls "$C7_DIR/.godot/imported/" 2>/dev/null | wc -l)" -gt 0 ]; then
        echo ""
        echo "==> Resources already imported ($(ls "$C7_DIR/.godot/imported/" | wc -l | tr -d ' ') files), skipping"
    else
        echo ""
        echo "==> Importing resources (opening editor briefly)..."
        echo "    Close the editor window when it appears, or it will auto-close in 60s."
        timeout 60 "$GODOT" --editor --path "$C7_DIR" || true
    fi
else
    echo ""
    echo "==> Skipping resource import (--skip-import)"
fi

# ---------------------------------------------------------------------------
# Step 3: Export macOS build
# ---------------------------------------------------------------------------
echo ""
echo "==> Exporting macOS build..."

EXPORT_PRESETS="$C7_DIR/export_presets.cfg"

# Disable Godot's built-in codesign (we sign manually for full control)
if grep -q 'codesign/codesign=1' "$EXPORT_PRESETS"; then
    sed -i '' 's/codesign\/codesign=1/codesign\/codesign=0/' "$EXPORT_PRESETS"
    CODESIGN_WAS_ENABLED=true
else
    CODESIGN_WAS_ENABLED=false
fi

mkdir -p "$EXPORTS_DIR"
rm -f "$EXPORTS_DIR/${APP_NAME}.zip"

"$GODOT" --headless --export-release "macOS" "$EXPORTS_DIR/${APP_NAME}.zip" --path "$C7_DIR"

# Revert codesign setting
if [ "$CODESIGN_WAS_ENABLED" = true ]; then
    sed -i '' 's/codesign\/codesign=0/codesign\/codesign=1/' "$EXPORT_PRESETS"
    echo "    Reverted codesign/codesign=1 in export_presets.cfg"
fi

# ---------------------------------------------------------------------------
# Step 4: Extract app bundle
# ---------------------------------------------------------------------------
echo ""
echo "==> Extracting app bundle..."
rm -rf "$SIGNING_DIR/${APP_NAME}.app"
mkdir -p "$SIGNING_DIR"
ditto -x -k "$EXPORTS_DIR/${APP_NAME}.zip" "$SIGNING_DIR"

# ---------------------------------------------------------------------------
# Step 5: Create entitlements (if not provided)
# ---------------------------------------------------------------------------
if [ -z "$ENTITLEMENTS" ]; then
    ENTITLEMENTS="$SIGNING_DIR/entitlements.plist"
    cat > "$ENTITLEMENTS" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.security.cs.allow-jit</key>
    <true/>
    <key>com.apple.security.cs.allow-unsigned-executable-memory</key>
    <true/>
    <key>com.apple.security.cs.disable-library-validation</key>
    <true/>
</dict>
</plist>
PLIST
    echo "==> Created .NET/CoreCLR entitlements (JIT, unsigned memory, no lib validation)"
fi

# ---------------------------------------------------------------------------
# Step 6: Sign all Mach-O binaries (inside-out)
# ---------------------------------------------------------------------------
echo ""
echo "==> Finding all Mach-O binaries..."
APP_PATH="$SIGNING_DIR/${APP_NAME}.app"
BINARIES=()
while IFS= read -r -d '' file; do
    if file "$file" | grep -q "Mach-O"; then
        BINARIES+=("$file")
    fi
done < <(find "$APP_PATH" -type f -print0)
echo "    Found ${#BINARIES[@]} Mach-O binaries"

echo "==> Signing nested binaries..."
for bin in "${BINARIES[@]}"; do
    echo "    Signing: ${bin#$SIGNING_DIR/}"
    codesign --force --options runtime --timestamp --entitlements "$ENTITLEMENTS" --sign "$IDENTITY" "$bin"
done

echo "==> Signing app bundle..."
codesign --deep --force --options runtime --timestamp --entitlements "$ENTITLEMENTS" --sign "$IDENTITY" "$APP_PATH"

echo "==> Verifying signature..."
codesign --verify --verbose=4 "$APP_PATH"

# ---------------------------------------------------------------------------
# Step 7: Notarize
# ---------------------------------------------------------------------------
if [ "$SKIP_NOTARIZE" = false ]; then
    echo ""
    echo "==> Submitting for notarization (this may take 5-15 minutes)..."
    NOTARIZE_ZIP="$SIGNING_DIR/${APP_NAME}-notarize.zip"
    rm -f "$NOTARIZE_ZIP"
    ditto -c -k --keepParent "$APP_PATH" "$NOTARIZE_ZIP"

    xcrun notarytool submit "$NOTARIZE_ZIP" --keychain-profile "$NOTARY_PROFILE" --wait

    echo "==> Stapling notarization ticket..."
    xcrun stapler staple "$APP_PATH"
    xcrun stapler validate "$APP_PATH"

    echo "==> Gatekeeper assessment..."
    spctl --assess --type exec --verbose=4 "$APP_PATH"

    rm -f "$NOTARIZE_ZIP"
else
    echo ""
    echo "==> Skipping notarization (--skip-notarize)"
fi

# ---------------------------------------------------------------------------
# Step 8: Package release zip
# ---------------------------------------------------------------------------
echo ""
echo "==> Packaging release zip..."
RELEASE_ZIP="$EXPORTS_DIR/${APP_NAME}-signed.zip"
rm -f "$RELEASE_ZIP"

# Use ditto for app bundle (avoids AppleDouble/resource fork files)
ditto -c -k --keepParent --norsrc --noextattr "$APP_PATH" "$RELEASE_ZIP"

# Add static data files alongside the app
cd "$C7_DIR"
COPYFILE_DISABLE=1 zip -r "$RELEASE_ZIP" Assets Text Lua

echo "    Created: $RELEASE_ZIP"
ls -lh "$RELEASE_ZIP"

# ---------------------------------------------------------------------------
# Step 9: GitHub release (optional)
# ---------------------------------------------------------------------------
if [ -n "$TAG" ] && [ "$SKIP_RELEASE" = false ]; then
    echo ""
    echo "==> Creating GitHub release: $TAG"

    command -v gh >/dev/null 2>&1 || { echo "ERROR: gh CLI not found. Run: brew install gh"; exit 1; }

    cd "$PROJECT_DIR"
    git tag -a "$TAG" -m "macOS signed release"
    git push origin "$TAG"

    gh release create "$TAG" \
        --title "${APP_NAME} ${TAG}" \
        --notes "$(cat <<EOF
## ${APP_NAME} - macOS Signed Build

Signed and notarized macOS universal binary (Apple Silicon + Intel).

### Installation
1. Download and unzip
2. Keep \`${APP_NAME}.app\`, \`Assets/\`, \`Text/\`, \`Lua/\` in the same folder
3. Double-click \`${APP_NAME}.app\` to launch
EOF
)" \
        "$RELEASE_ZIP#${APP_NAME}-macOS.zip"

    echo "    Release created: $(gh release view "$TAG" --json url -q .url)"
elif [ -n "$TAG" ]; then
    echo ""
    echo "==> Skipping GitHub release (--skip-release)"
fi

echo ""
echo "==> Done! Release zip: $RELEASE_ZIP"
