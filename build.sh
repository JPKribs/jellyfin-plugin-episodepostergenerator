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

# MARK: Log function with consistent formatting
log() {
    local level="$1"
    local message="$2"
    case "$level" in
        "INFO")  echo -e "${CYAN}[INFO]${NC} $message" ;;
        "WARN")  echo -e "${YELLOW}[WARN]${NC} $message" ;;
        "ERROR") echo -e "${RED}[ERROR]${NC} $message" ;;
        "SUCCESS") echo -e "${GREEN}[SUCCESS]${NC} $message" ;;
        *)       echo -e "$message" ;;
    esac
}

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
    
    echo "10.10.X"
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

# MARK: Validate embedded resources exist
validate_resources() {
    log "INFO" "Validating embedded resources exist"
    
    local missing_files=()
    local config_files=(
        "$PROJECT_DIR/Configuration/configPage.html"
        "$PROJECT_DIR/Configuration/configPage.js"
        "$PROJECT_DIR/Configuration/configPage.css"
        "$PROJECT_DIR/Logo.png"
    )
    
    for file in "${config_files[@]}"; do
        if [[ ! -f "$file" ]]; then
            missing_files+=("$file")
        else
            log "SUCCESS" "Found: $file"
        fi
    done
    
    if [[ ${#missing_files[@]} -gt 0 ]]; then
        log "ERROR" "Missing embedded resource files:"
        for file in "${missing_files[@]}"; do
            log "ERROR" "  - $file"
        done
        log "ERROR" "These files are required for the configuration page to work"
        return 1
    fi
    
    log "SUCCESS" "All embedded resources found"
    return 0
}

# MARK: Main build process
main() {
    log "INFO" "Starting Episode Poster Generator Plugin build"
    
    # Get version and plugin info once at the start
    log "INFO" "Reading version from Directory.Build.props"
    VERSION=$(get_plugin_version)
    log "SUCCESS" "Version: $VERSION"
    
    log "INFO" "Reading plugin info from build.yaml"
    PLUGIN_INFO=$(get_plugin_info)
    PLUGIN_NAME=$(echo "$PLUGIN_INFO" | cut -d'|' -f1)
    PLUGIN_GUID=$(echo "$PLUGIN_INFO" | cut -d'|' -f2)
    log "SUCCESS" "Plugin: $PLUGIN_NAME"
    log "SUCCESS" "GUID: $PLUGIN_GUID"
    
    log "INFO" "Build configuration: $CONFIGURATION"
    log "INFO" "Project file: $PROJECT_FILE"
    
    # Check if project file exists
    if [[ ! -f "$PROJECT_FILE" ]]; then
        log "ERROR" "Project file not found: $PROJECT_FILE"
        exit 1
    fi
    
    # Validate embedded resources
    if ! validate_resources; then
        exit 1
    fi
    
    # Clean previous builds
    if [[ "$2" == "--clean" ]] || [[ -d "$OUTPUT_DIR" ]]; then
        log "INFO" "Cleaning previous builds"
        rm -rf "$OUTPUT_DIR"
        log "INFO" "Running dotnet clean"
        dotnet clean "$PROJECT_FILE" --configuration "$CONFIGURATION" --verbosity quiet
    fi
    
    # Create output directory
    log "INFO" "Creating output directory: $OUTPUT_DIR"
    mkdir -p "$OUTPUT_DIR"
    
    # Restore packages
    log "INFO" "Restoring NuGet packages"
    if ! dotnet restore "$PROJECT_FILE" --verbosity minimal; then
        log "ERROR" "Package restore failed"
        exit 1
    fi
    log "SUCCESS" "Package restore completed"
    
    # Build the project
    log "INFO" "Building project with configuration: $CONFIGURATION"
    if ! dotnet build "$PROJECT_FILE" --configuration "$CONFIGURATION" --no-restore --verbosity minimal; then
        log "ERROR" "Build failed"
        exit 1
    fi
    
    # Find the built DLL
    local dll_path="$PROJECT_DIR/bin/$CONFIGURATION/net8.0/Jellyfin.Plugin.EpisodePosterGenerator.dll"
    if [[ ! -f "$dll_path" ]]; then
        log "ERROR" "Could not find built DLL at: $dll_path"
        exit 1
    fi
    
    log "SUCCESS" "Build completed: $dll_path"
    
    # Verify embedded resources in DLL
    log "INFO" "Verifying embedded resources in built DLL"
    local resource_count
    if command -v dotnet >/dev/null 2>&1; then
        # Use reflection to count embedded resources
        resource_count=$(dotnet run --project - <<'EOF' 2>/dev/null || echo "0"
using System;
using System.Reflection;
using System.Linq;

var assembly = Assembly.LoadFrom(args[0]);
var resources = assembly.GetManifestResourceNames();
Console.WriteLine(resources.Length);
foreach (var resource in resources.OrderBy(r => r))
{
    Console.WriteLine($"  {resource}");
}
EOF
)
        
        if [[ "$resource_count" =~ ^[0-9]+$ ]] && [[ "$resource_count" -gt 0 ]]; then
            log "SUCCESS" "Found $resource_count embedded resources in DLL"
        else
            log "WARN" "Could not verify embedded resources (reflection check failed)"
        fi
    fi
    
    # Create ZIP package
    local zip_name="jellyfin-plugin-episodepostergenerator-$VERSION.zip"
    local zip_path="$OUTPUT_DIR/$zip_name"
    
    log "INFO" "Creating package: $zip_name"
    
    # Create temporary directory for packaging
    local temp_dir="$OUTPUT_DIR/temp"
    log "INFO" "Creating temporary directory: $temp_dir"
    mkdir -p "$temp_dir"
    
    # Copy DLL to temp directory
    log "INFO" "Copying DLL to package directory"
    cp "$dll_path" "$temp_dir/"
    
    # Create ZIP with all files in temp directory
    log "INFO" "Creating ZIP archive"
    if command -v zip >/dev/null 2>&1; then
        (cd "$temp_dir" && zip -q "../$zip_name" *)
    elif command -v python3 >/dev/null 2>&1; then
        python3 -c "
import zipfile
import os
with zipfile.ZipFile('$zip_path', 'w') as zf:
    for file in os.listdir('$temp_dir'):
        zf.write(os.path.join('$temp_dir', file), file)
"
    else
        log "ERROR" "No zip utility found (zip or python3 required)"
        exit 1
    fi
    
    # Clean up temp directory
    log "INFO" "Cleaning up temporary directory"
    rm -rf "$temp_dir"
    
    if [[ ! -f "$zip_path" ]]; then
        log "ERROR" "Failed to create ZIP package"
        exit 1
    fi
    
    log "SUCCESS" "Package created: $zip_path"
    
    # Calculate MD5 checksum
    log "INFO" "Calculating MD5 checksum"
    local md5_hash
    if command -v md5sum >/dev/null 2>&1; then
        md5_hash=$(md5sum "$zip_path" | cut -d' ' -f1)
    elif command -v md5 >/dev/null 2>&1; then
        md5_hash=$(md5 -q "$zip_path")
    else
        log "WARN" "MD5 utility not found, skipping checksum"
        md5_hash="N/A"
    fi
    
    if [[ "$md5_hash" != "N/A" ]]; then
        local checksum_file="$OUTPUT_DIR/$zip_name.md5"
        echo "$md5_hash" > "$checksum_file"
        log "SUCCESS" "Checksum file created: $checksum_file"
    fi
    
    # Get file size
    local file_size
    if command -v du >/dev/null 2>&1; then
        file_size=$(du -h "$zip_path" | cut -f1)
    else
        file_size="Unknown"
    fi
    
    # Display final results
    echo
    log "SUCCESS" "Build Summary:"
    echo "  Plugin Name: $PLUGIN_NAME"
    echo "  Version: $VERSION" 
    echo "  GUID: $PLUGIN_GUID"
    echo "  Package: $zip_path"
    echo "  Size: $file_size"
    echo "  MD5: $md5_hash"
    [[ "$md5_hash" != "N/A" ]] && echo "  Checksum: $checksum_file"
    
    echo
    log "SUCCESS" "Build completed successfully!"
    log "INFO" "Package ready for Jellyfin plugin installation"
}

# Run main function with all arguments
main "$@"