![Episode Poster Generator](Screenshots/Logo.png)

A Jellyfin plugin that automatically generates custom episode posters using smart frame analysis, black frame detection, letterbox detection, and configurable text styling. Perfect for filling in missing or generic episode artwork with clean, consistent visuals.

## Features

- **Automatic Frame Extraction**: Smart selection of representative frames from video files
- **Black Frame Detection**: Avoids extracting frames from black/transition scenes
- **Letterbox Detection**: Automatically detects and crops black letterbox/pillarbox borders from poster images
- **Multiple Poster Styles**: Choose from Standard, Cutout, Numeral, and Logo designs
- **Customizable Typography**: Full control over fonts, sizes, colors, and positioning
- **Flexible Layouts**: Support for various aspect ratios and fill strategies
- **Enhanced Logo Positioning**: Configurable logo alignment and positioning for Logo style posters
- **Cutout Text Borders**: Optional contrasting borders for improved cutout text visibility
- **Episode Information Display**: Show episode codes, titles, and season information

## Poster Styles

### Standard Style
Classic episode posters with overlay text and episode information.

| Example 1 | Example 2 |
|-----------|-----------|
| ![Standard 1](Screenshots/Standard%201.jpg) | ![Standard 2](Screenshots/Standard%202.jpg) |

**Features:**
- Episode screenshot as background
- Configurable text overlay with shadows
- Season and episode information
- Optional episode title display
- Customizable overlay tint

### Cutout Style  
Large episode numbers displayed as transparent cutouts revealing the screenshot beneath.

| Example 1 | Example 2 | Example 3 |
|-----------|-----------|-----------|
| ![Cutout 1](Screenshots/Cutout%201.jpg) | ![Cutout 2](Screenshots/Cutout%202.jpg) | ![Cutout 3](Screenshots/Cutout%203.jpg) |

**Cutout Types:**
- **Code**: Displays episode in format "S01E03" 
- **Text**: Displays episode number as words (e.g., "THREE")

**Features:**
- Transparent text cutout effect
- Background color overlay
- Multi-line text support for longer episode codes
- Automatic font scaling
- Optional contrasting borders for improved visibility

### Numeral Style
Roman numeral episode numbers with elegant typography.

| Example 1 | Example 2 | Example 3 |
|-----------|-----------|-----------|
| ![Numeral 1](Screenshots/Numeral%201.jpg) | ![Numeral 2](Screenshots/Numeral%202.jpg) | ![Numeral 3](Screenshots/Numeral%203.jpg) |

**Features:**
- Roman numeral conversion (1-3999)
- Large, centered numeral display
- Background color overlay
- Optional episode title

### Logo Style
Series logo-focused posters with episode information and clean typography.

| Example 1 | Example 2 | Example 3 |
|-----------|-----------|-----------|
| ![Logo 1](Screenshots/Logo%201.jpg) | ![Logo 2](Screenshots/Logo%202.jpg) | ![Logo 3](Screenshots/Logo%203.jpg) |

**Features:**
- Series logo image as primary visual element
- Solid background color for clean appearance
- Configurable logo positioning (Top, Center, Bottom)
- Configurable logo alignment (Left, Center, Right)
- Adjustable logo height percentage (1-100%)
- Episode code display in S##E## format with proper zero-padding
- Text fallback when series logo image unavailable
- Optional episode title display
- Bottom-aligned text elements with consistent spacing
- Logo image respects safe area margins for optimal positioning

**Logo Sources:**
- **Primary**: Series logo image (when available)
- **Fallback**: Series primary image
- **Text Fallback**: Series name with optimized font scaling

## Poster Architecture

The Episode Poster Generator uses a standardized 4-layer rendering pipeline to create consistent, high-quality posters across all styles:

### Layer 1: Canvas (Base Layer)
The foundation layer that provides the visual background for the poster.

**Options:**
- **Video Frame Extraction**: Automatically extracts a representative frame from the episode video file using smart brightness detection and configurable extraction windows
- **Transparent Background**: Creates a solid color or transparent canvas when video extraction is disabled

**Processing:**
- HDR brightening for HDR content
- Letterbox/pillarbox detection and cropping
- Aspect ratio adjustments and fill strategies

### Layer 2: Overlay (Color Tinting)
A semi-transparent color layer applied over the canvas to enhance text readability and create visual cohesion.

**Features:**
- Configurable ARGB hex colors with alpha transparency
- Applied uniformly across the entire poster surface
- Essential for ensuring text remains readable against varying background images

### Layer 3: Graphics (Static Images)
Optional static graphic overlays positioned above the canvas but below text elements.

**Capabilities:**
- User-configurable file path for custom graphics
- Automatic sizing and positioning within safe area boundaries
- Supports PNG, JPG, and WEBP formats
- Maintains aspect ratio while fitting within poster constraints

### Layer 4: Typography (Text and Logos)
The top layer containing all text elements, episode information, and series logos.

**Elements:**
- Episode numbers and season information
- Episode titles with automatic text wrapping
- Series logos with configurable positioning
- Style-specific typography (Roman numerals, cutout text, etc.)
- Drop shadows and contrasting borders for enhanced readability

### Rendering Pipeline
Each poster style (Standard, Cutout, Numeral, Logo) follows this exact 4-layer sequence, ensuring consistent output quality and predictable layering behavior. The modular approach allows for easy customization and troubleshooting while maintaining visual coherence across different poster types.

## Installation

### Step 1: Add Plugin Repository

* Open Jellyfin and navigate to Dashboard → Plugins → Repositories
* Click Add Repository
* Enter the following repository URL: `https://raw.githubusercontent.com/JPKribs/jellyfin-plugin-episodepostergenerator/master/manifest.json`
* Click Save

### Step 2: Install Plugin

* Go to the Catalog tab in the Plugins section
* Find Episode Poster Generator in the catalog
* Click Install
* Wait for installation to complete

### Step 3: Restart Jellyfin

* Restart your Jellyfin server completely
* Wait for Jellyfin to fully start up

### Verification Check

* After restart, navigate to Dashboard → Plugins → Episode Poster Generator to confirm the plugin configuration page loads properly.
