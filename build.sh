#!/bin/bash

set -e

# Configuration
CONFIGURATION="${1:-Release}"
OUTPUT_DIR="dist"
PROJECT_DIR="Jellyfin.Plugin.EpisodePosterGenerator"
PROJECT_FILE="$PROJECT_DIR/Jellyfin.Plugin.EpisodePosterGenerator.csproj"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

# MARK: Get version from Directory.Build.props
get_plugin_version() {
    local props_file="Directory.Build.props"
    if [[ -f "$props_file" ]]; then
        local version=$(grep '<Version>' "$props_file" | sed 's/.*<Version>\(.*\)<\/Version>.*/\1/' | tr -d ' ')
        if [[ -n "$version" ]]; then
            echo "$version"
            return
        fi
    fi
    echo "1.0.0"
}

# MARK: Get plugin info from build.yaml
get_plugin_info() {
    local build_file="build.yaml"
    local name="Episode Poster Generator"
    local guid="b8715e44-6b77-4c88-9c74-2b6f4c7b9a1e"
    
    if [[ -f "$build_file" ]]; then
        local extracted_name=$(grep '^name:' "$build_file" | sed 's/name: *"\?\([^"]*\)"\?.*/\1/' | tr -d '"')
        local extracted_guid=$(grep '^guid:' "$build_file" | sed 's/guid: *"\?\([^"]*\)"\?.*/\1/' | tr -d '"')
        
        [[ -n "$extracted_name" ]] && name="$extracted_name"
        [[ -n "$extracted_guid" ]] && guid="$extracted_guid"
    fi
    
    echo "$name|$guid"
}

# MARK: Main build process
echo -e "${GREEN}🚀 Building Episode Poster Generator Plugin...${NC}"

VERSION=$(get_plugin_version)
PLUGIN_INFO=$(get_plugin_info)
PLUGIN_NAME=$(echo "$PLUGIN_INFO" | cut -d'|' -f1)
PLUGIN_GUID=$(echo "$PLUGIN_INFO" | cut -d'|' -f2)

echo -e "${YELLOW}📦 Version: $VERSION${NC}"
echo -e "${YELLOW}📁 Project: $PROJECT_FILE${NC}"

# Check if project file exists
if [[ ! -f "$PROJECT_FILE" ]]; then
    echo -e "${RED}❌ Project file not found: $PROJECT_FILE${NC}"
    exit 1
fi

# Clean previous builds
if [[ "$2" == "--clean" ]] || [[ -d "$OUTPUT_DIR" ]]; then
    echo -e "${CYAN}🧹 Cleaning previous builds...${NC}"
    rm -rf "$OUTPUT_DIR"
    dotnet clean "$PROJECT_FILE" --configuration "$CONFIGURATION" --verbosity quiet
fi

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Restore packages
echo -e "${CYAN}📦 Restoring packages...${NC}"
dotnet restore "$PROJECT_FILE" --verbosity quiet

# Build the project
echo -e "${CYAN}🔨 Building project...${NC}"
if ! dotnet build "$PROJECT_FILE" --configuration "$CONFIGURATION" --no-restore --verbosity quiet; then
    echo -e "${RED}❌ Build failed!${NC}"
    exit 1
fi

# Find the built DLL
DLL_PATTERN="$PROJECT_DIR/bin/$CONFIGURATION/net8.0/Jellyfin.Plugin.EpisodePosterGenerator.dll"
if [[ ! -f "$DLL_PATTERN" ]]; then
    echo -e "${RED}❌ Could not find built DLL at: $DLL_PATTERN${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Built successfully: $DLL_PATTERN${NC}"

# Create ZIP package
ZIP_NAME="jellyfin-plugin-episodepostergenerator-$VERSION.zip"
ZIP_PATH="$OUTPUT_DIR/$ZIP_NAME"

echo -e "${CYAN}📦 Creating package: $ZIP_NAME${NC}"

# Create temporary directory for packaging
TEMP_DIR="$OUTPUT_DIR/temp"
mkdir -p "$TEMP_DIR"

# Copy DLL to temp directory
cp "$DLL_PATTERN" "$TEMP_DIR/"
cp "$PROJECT_DIR/Logo.png" "$TEMP_DIR/"

# Create ZIP
if command -v zip >/dev/null 2>&1; then
    (cd "$TEMP_DIR" && zip -q "../$ZIP_NAME" *.dll)
elif command -v python3 >/dev/null 2>&1; then
    python3 -c "
import zipfile
import os
with zipfile.ZipFile('$ZIP_PATH', 'w') as zf:
    for file in os.listdir('$TEMP_DIR'):
        zf.write(os.path.join('$TEMP_DIR', file), file)
"
else
    echo -e "${RED}❌ No zip utility found (zip or python3 required)${NC}"
    exit 1
fi

# Clean up temp directory
rm -rf "$TEMP_DIR"

if [[ ! -f "$ZIP_PATH" ]]; then
    echo -e "${RED}❌ Failed to create ZIP package${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Package created: $ZIP_PATH${NC}"

# Calculate MD5 checksum
echo -e "${CYAN}🔐 Calculating checksum...${NC}"
if command -v md5sum >/dev/null 2>&1; then
    MD5_HASH=$(md5sum "$ZIP_PATH" | cut -d' ' -f1)
elif command -v md5 >/dev/null 2>&1; then
    MD5_HASH=$(md5 -q "$ZIP_PATH")
else
    echo -e "${YELLOW}⚠️  MD5 utility not found, skipping checksum${NC}"
    MD5_HASH="N/A"
fi

if [[ "$MD5_HASH" != "N/A" ]]; then
    CHECKSUM_FILE="$OUTPUT_DIR/$ZIP_NAME.md5"
    echo "$MD5_HASH" > "$CHECKSUM_FILE"
fi

# Get file size
FILE_SIZE=$(du -h "$ZIP_PATH" | cut -f1)

# Display results
echo -e "\n${GREEN}📋 Build Summary:${NC}"
echo -e "  Plugin: ${PLUGIN_NAME}"
echo -e "  Version: ${VERSION}"
echo -e "  GUID: ${PLUGIN_GUID}"
echo -e "  Package: ${ZIP_PATH}"
echo -e "  Size: ${FILE_SIZE}"
echo -e "  MD5: ${MD5_HASH}"
[[ "$MD5_HASH" != "N/A" ]] && echo -e "  Checksum file: $CHECKSUM_FILE"

echo -e "\n${GREEN}🎉 Build completed successfully!${NC}"
echo -e "${YELLOW}📤 Ready for GitHub release upload${NC}"