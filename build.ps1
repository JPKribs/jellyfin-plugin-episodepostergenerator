#!/usr/bin/env pwsh

param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "dist",
    [switch]$Clean = $false
)

# MARK: Get version from Directory.Build.props
function Get-PluginVersion {
    $propsFile = "Directory.Build.props"
    if (Test-Path $propsFile) {
        [xml]$props = Get-Content $propsFile
        $version = $props.Project.PropertyGroup.Version
        if ($version) {
            return $version
        }
    }
    return "1.0.0.0"
}

# MARK: Get plugin info from build.yaml
function Get-PluginInfo {
    $buildFile = "build.yaml"
    if (Test-Path $buildFile) {
        $content = Get-Content $buildFile -Raw
        $name = ($content | Select-String -Pattern 'name:\s*"([^"]*)"').Matches[0].Groups[1].Value
        $guid = ($content | Select-String -Pattern 'guid:\s*"([^"]*)"').Matches[0].Groups[1].Value
        return @{
            Name = $name
            Guid = $guid
        }
    }
    return @{
        Name = "Episode Poster Generator"
        Guid = "b8715e44-6b77-4c88-9c74-2b6f4c7b9a1e"
    }
}

# MARK: Main build process
Write-Host "🚀 Building Episode Poster Generator Plugin..." -ForegroundColor Green

$projectDir = "Jellyfin.Plugin.EpisodePosterGenerator"
$projectFile = "$projectDir/Jellyfin.Plugin.EpisodePosterGenerator.csproj"
$version = Get-PluginVersion
$pluginInfo = Get-PluginInfo

Write-Host "📦 Version: $version" -ForegroundColor Yellow
Write-Host "📁 Project: $projectFile" -ForegroundColor Yellow

# Clean previous builds
if ($Clean -or (Test-Path $OutputDir)) {
    Write-Host "🧹 Cleaning previous builds..." -ForegroundColor Cyan
    Remove-Item -Path $OutputDir -Recurse -Force -ErrorAction SilentlyContinue
    dotnet clean $projectFile --configuration $Configuration --verbosity quiet
}

# Create output directory
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# Build the project
Write-Host "🔨 Building project..." -ForegroundColor Cyan
$buildResult = dotnet build $projectFile --configuration $Configuration --no-restore --verbosity quiet

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}

# Find the built DLL
$dllPattern = "$projectDir/bin/$Configuration/net8.0/Jellyfin.Plugin.EpisodePosterGenerator.dll"
$dllFile = Get-ChildItem -Path $dllPattern -ErrorAction SilentlyContinue | Select-Object -First 1

if (-not $dllFile) {
    Write-Host "❌ Could not find built DLL at: $dllPattern" -ForegroundColor Red
    exit 1
}

Write-Host "✅ Built successfully: $($dllFile.FullName)" -ForegroundColor Green

# Create ZIP package
$zipName = "jellyfin-plugin-episodepostergenerator-$version.zip"
$zipPath = Join-Path $OutputDir $zipName

Write-Host "📦 Creating package: $zipName" -ForegroundColor Cyan

# Create temporary directory for packaging
$tempDir = Join-Path $OutputDir "temp"
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

# Copy DLL to temp directory
Copy-Item -Path $dllFile.FullName -Destination $tempDir

# Create ZIP
try {
    Compress-Archive -Path "$tempDir/*" -DestinationPath $zipPath -Force
    Write-Host "✅ Package created: $zipPath" -ForegroundColor Green
} catch {
    Write-Host "❌ Failed to create ZIP: $_" -ForegroundColor Red
    exit 1
} finally {
    # Clean up temp directory
    Remove-Item -Path $tempDir -Recurse -Force -ErrorAction SilentlyContinue
}

# Calculate MD5 checksum
Write-Host "🔐 Calculating checksum..." -ForegroundColor Cyan
$md5Hash = (Get-FileHash -Path $zipPath -Algorithm MD5).Hash.ToLower()
$checksumFile = Join-Path $OutputDir "$zipName.md5"
$md5Hash | Out-File -FilePath $checksumFile -Encoding ASCII

# Display results
Write-Host "`n📋 Build Summary:" -ForegroundColor Green
Write-Host "  Plugin: $($pluginInfo.Name)" -ForegroundColor White
Write-Host "  Version: $version" -ForegroundColor White
Write-Host "  GUID: $($pluginInfo.Guid)" -ForegroundColor White
Write-Host "  Package: $zipPath" -ForegroundColor White
Write-Host "  Size: $([math]::Round((Get-Item $zipPath).Length / 1KB, 2)) KB" -ForegroundColor White
Write-Host "  MD5: $md5Hash" -ForegroundColor White
Write-Host "  Checksum file: $checksumFile" -ForegroundColor White

Write-Host "`n🎉 Build completed successfully!" -ForegroundColor Green
Write-Host "📤 Ready for GitHub release upload" -ForegroundColor Yellow