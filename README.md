# Episode Poster Generator

A Jellyfin plugin that automatically generates custom episode posters using smart frame analysis, black frame detection, and configurable text styling. Perfect for filling in missing or generic episode artwork with clean, consistent visuals.

## Features

- **Automatic Frame Extraction**: Smart selection of representative frames from video files
- **Black Frame Detection**: Avoids extracting frames from black/transition scenes
- **Multiple Poster Styles**: Choose from Standard, Cutout, and Numeral designs
- **Customizable Typography**: Full control over fonts, sizes, colors, and positioning
- **Flexible Layouts**: Support for various aspect ratios and fill strategies
- **Episode Information Display**: Show episode codes, titles, and season information

## Poster Styles

### Standard Style
Classic episode posters with overlay text and episode information.

| Example | Description |
|---------|-------------|
| ![Standard 1](Screenshots/Standard%201.jpg) | Episode screenshot with title and season/episode info overlaid at the bottom |
| ![Standard 2](Screenshots/Standard%202.jpg) | Clean layout with episode title prominently displayed and season/episode info in corners |

**Features:**
- Episode screenshot as background
- Configurable text overlay with shadows
- Season and episode information
- Optional episode title display
- Customizable overlay tint

### Cutout Style  
Large episode numbers displayed as transparent cutouts revealing the screenshot beneath.

| Example | Description |
|---------|-------------|
| ![Cutout - Opacity 1](Screenshots/Cutout%20-%20Opacity%201.jpg) | Large "03" cutout number with episode title below |
| ![Cutout - Opacity 2](Screenshots/Cutout%20-%20Opacity%202.jpg) | Bold episode number cutout with dramatic visual impact |

**Cutout Types:**
- **Code**: Displays episode in format "S01E03" 
- **Text**: Displays episode number as words (e.g., "THREE")

**Features:**
- Transparent text cutout effect
- Background color overlay
- Multi-line text support for longer episode codes
- Automatic font scaling

### Numeral Style
Roman numeral episode numbers with elegant typography.

| Example | Description |
|---------|-------------|
| ![Numeral 1](Screenshots/Numeral%201.jpg) | Large "VI" Roman numeral with episode title |
| ![Numeral 2](Screenshots/Numeral%202.jpg) | Clean Roman numeral display with background overlay |

**Features:**
- Roman numeral conversion (1-3999)
- Large, centered numeral display
- Background color overlay
- Optional episode title

## Configuration Options

### Plugin Settings
- **Active**: Enable/disable the plugin
- **Style**: Choose between Standard, Cutout, or Numeral poster styles

### Poster Settings
- **Fill Strategy**: 
  - *Original*: Preserve original screenshot dimensions
  - *Fill*: Expand to fill target aspect ratio (may stretch)
  - *Fit*: Crop to fit target aspect ratio
- **Aspect Ratio**: Set poster dimensions (e.g., "16:9", "3:2", "4:3")

### Episode Information
- **Font**: Choose from extensive font library (Arial, Impact, Helvetica, etc.)
- **Font Style**: Normal, Bold, Italic, Bold Italic
- **Font Size**: Percentage of poster height (1-100%)
- **Font Color**: Hex color code (e.g., #FFFFFF)

### Episode Title
- **Show Title**: Toggle episode title display
- **Font**: Independent font selection for titles
- **Font Style**: Normal, Bold, Italic, Bold Italic  
- **Font Size**: Percentage of poster height (1-100%)
- **Font Color**: Hex color code

### Visual Effects
- **Background Color**: ARGB overlay color for Cutout/Numeral styles (e.g., #66000000)
- **Overlay Tint**: ARGB tint for Standard style images (e.g., #33000000)

## Installation

1. Download the plugin DLL from the releases page
2. Place `Jellyfin.Plugin.EpisodePosterGenerator.dll` in your Jellyfin plugins directory
3. Restart Jellyfin
4. Navigate to Dashboard → Plugins → Episode Poster Generator to configure

## Requirements

- Jellyfin 10.10.7 or later
- .NET 8.0 runtime
- FFmpeg (for frame extraction and black scene detection)
- SkiaSharp (included with plugin)

## Technical Details

### Frame Selection Process
1. Analyze video duration using FFprobe
2. Detect black scenes with configurable thresholds
3. Select optimal timestamp candidates (25%, 50%, 75% of duration)
4. Choose first candidate that avoids black intervals
5. Extract frame using FFmpeg with high quality settings

### Text Rendering
- Smart text wrapping for long titles (max 2 lines)
- Automatic font scaling to fit available space
- Shadow effects for improved readability
- Safe area margins (5% of image dimensions)

### Supported Formats
- **Input**: Any video format supported by FFmpeg
- **Output**: JPEG images with 95% quality
- **Fonts**: System fonts via SkiaSharp font manager

## Configuration Examples

### Minimal Setup (Standard Style)
```
Style: Standard
Font: Arial Bold
Episode Font Size: 7%
Title Font Size: 10%
Background Color: #66000000
```

### Dramatic Cutout Setup  
```
Style: Cutout
Cutout Type: Text
Font: Impact Bold
Episode Font Size: 15%
Background Color: #80000000
```

### Elegant Numeral Setup
```
Style: Numeral  
Font: Garamond Bold
Episode Font Size: 12%
Background Color: #4D000000
Show Title: true
```

## Troubleshooting

**Plugin not generating posters:**
- Verify FFmpeg is properly configured in Jellyfin
- Check that video files are accessible
- Ensure plugin is enabled in configuration

**Poor frame selection:**
- Adjust black detection thresholds if available
- Verify video files have sufficient content

**Text rendering issues:**
- Confirm selected fonts are available on system
- Adjust font sizes for better fit
- Check color contrast settings

## License

This project is licensed under the GNU General Public License v3.0. See [LICENSE](LICENSE) for details.

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for bugs and feature requests.