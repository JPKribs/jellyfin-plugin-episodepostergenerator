# Episode Poster Generator Settings

This document provides a comprehensive guide to all configuration settings available in the Episode Poster Generator plugin. Settings are organized by their sections as they appear in the configuration interface.

## Plugin

### Enable Provider
**Type:** Checkbox  
**Default:** Enabled  
**Effect:** Controls whether the plugin functions as a metadata provider for episode posters. When enabled, the plugin will automatically generate posters when Jellyfin requests episode images. When disabled, the plugin will not respond to image provider requests.

### Enable Scheduled Task
**Type:** Checkbox  
**Default:** Enabled  
**Effect:** Controls whether the plugin appears as a scheduled task in Jellyfin's task management. When enabled, you can run batch poster generation through the scheduled tasks interface. When disabled, the scheduled task will not be available.

## Poster

### Enable Poster Episode
**Type:** Checkbox  
**Default:** Enabled  
**Effect:** Controls whether the plugin extracts frames from video files to use as poster backgrounds. When enabled, the plugin will analyze video content and extract representative frames. When disabled, the plugin will create posters with transparent or solid color backgrounds only.

#### Extraction Start (%)
**Type:** Number (0-100)  
**Default:** 20  
**Effect:** Sets the starting point for frame extraction as a percentage of the episode duration. The plugin will skip the first X% of the episode to avoid opening credits and intro sequences. For example, 20% means extraction begins 20% into the episode runtime.

#### Extraction End (%)
**Type:** Number (0-100)  
**Default:** 80  
**Effect:** Sets the ending point for frame extraction as a percentage of the episode duration. The plugin will stop extracting frames at X% of the episode to avoid end credits and outro sequences. For example, 80% means extraction stops at 80% of the episode runtime.

#### Brighten HDR (%)
**Type:** Number (0-100)  
**Default:** 25  
**Effect:** Applies brightness adjustment specifically to HDR content as a percentage increase. This helps HDR frames appear properly exposed in poster images, as HDR content can appear dark when converted to standard images. Higher values increase brightness more significantly.

#### Enable Hardware Accelerated Decoding
**Type:** Checkbox  
**Default:** Disabled  
**Effect:** Uses Jellyfin's hardware acceleration settings for video decoding during frame extraction. When enabled, the plugin will attempt to use GPU-accelerated decoding for faster processing. Falls back to software decoding if hardware acceleration fails.

### Enable Letterbox Detection
**Type:** Checkbox  
**Default:** Enabled  
**Effect:** Automatically detects and removes black letterbox (horizontal) and pillarbox (vertical) borders from extracted poster images. When enabled, the plugin analyzes pixel brightness to identify and crop away black borders, resulting in posters that use the full image area.

#### Black Threshold
**Type:** Number (0-255)  
**Default:** 25  
**Effect:** Sets the pixel brightness threshold for black detection during letterbox analysis. Pixels with brightness values at or below this threshold are considered "black" for border detection purposes. Lower values detect only very dark pixels; higher values include darker gray pixels.

#### Detection Confidence (%)
**Type:** Number (50-100)  
**Default:** 85  
**Effect:** Sets the percentage of pixels that must be black for a row or column to be considered letterboxing. Higher values require more pixels to be black before cropping occurs, making detection more conservative. Lower values are more aggressive in detecting borders.

### Style
**Type:** Dropdown  
**Options:** Standard, Cutout, Logo, Numeral  
**Default:** Standard  
**Effect:** Determines the overall poster design and layout approach:
- **Standard:** Classic poster with background image and bottom-aligned text
- **Cutout:** Large text with transparent cutout effect revealing background
- **Logo:** Series logo-focused design with clean typography
- **Numeral:** Roman numeral episode numbers with elegant styling

#### Enable Cutout Text Border
**Type:** Checkbox *(Cutout style only)*  
**Default:** Enabled  
**Effect:** Adds a contrasting border around cutout text for better visibility against varied backgrounds. The border color is automatically calculated to contrast with the overlay color.

#### Type
**Type:** Dropdown *(Cutout style only)*  
**Options:** Code, Text  
**Default:** Code  
**Effect:** Controls the format of cutout text display:
- **Code:** Shows episode in S01E01 format
- **Text:** Shows episode number as words (e.g., "THREE")

#### Logo Alignment
**Type:** Dropdown *(Logo style only)*  
**Options:** Left, Center, Right  
**Default:** Center  
**Effect:** Controls horizontal positioning of the series logo within the poster. Affects both logo images and text fallbacks when no logo image is available.

#### Logo Position
**Type:** Dropdown *(Logo style only)*  
**Options:** Top, Center, Bottom  
**Default:** Center  
**Effect:** Controls vertical positioning of the series logo within the poster safe area. Combined with Logo Alignment to determine final logo placement.

#### Logo Height
**Type:** Number (1-100) *(Logo style only)*  
**Default:** 30  
**Effect:** Sets the logo height as a percentage of the total poster height. Larger values make the logo more prominent; smaller values make it more subtle. Logo width is automatically calculated to maintain aspect ratio.

### Fill Strategy
**Type:** Dropdown  
**Options:** Original, Fill, Fit  
**Default:** Original  
**Effect:** Controls how extracted video frames are resized to create poster dimensions:
- **Original:** Preserves the exact frame dimensions (may result in non-standard poster sizes)
- **Fill:** Stretches the frame to match the target aspect ratio (may distort the image)
- **Fit:** Crops the frame to fit the target aspect ratio (may cut off parts of the image)

#### Aspect Ratio
**Type:** Text *(Fill and Fit strategies only)*  
**Default:** 16:9  
**Effect:** Defines the target aspect ratio for poster dimensions when using Fill or Fit strategies. Format should be width:height (e.g., 16:9, 3:2, 4:3). This ratio determines the final poster proportions.

### Safe Area
**Type:** Number (1-100)  
**Default:** 5  
**Effect:** Sets the percentage of vertical and horizontal space preserved around poster edges as a safe area. Text and graphic elements are positioned within this safe area to ensure they don't appear too close to edges. Higher values create more padding.

### File Type
**Type:** Dropdown  
**Options:** JPEG, PNG, WEBP
**Default:** WEBP  
**Effect:** Determines the file format for generated poster images. Each format has different characteristics:
- **JPEG:** Good compression, widely supported, no transparency
- **PNG:** Lossless quality, transparency support, larger files
- **WEBP:** Excellent compression, modern format, good quality

## Episode Information

### Show Episode
**Type:** Checkbox  
**Default:** Enabled  
**Effect:** Controls whether season and episode information (e.g., "S01E05") is displayed on the poster. When disabled, only episode titles (if enabled) will be shown. Note: Always enabled for Cutout and Numeral styles as episode information is central to their design.

#### Font
**Type:** Dropdown  
**Options:** Various font families  
**Default:** Arial  
**Effect:** Sets the font family for episode number and season information text. Affects the visual style and readability of episode codes. Available fonts include system fonts like Arial, Helvetica, Times New Roman, and others.

#### Font Style
**Type:** Dropdown  
**Options:** Normal, Bold, Italic, Bold Italic  
**Default:** Bold  
**Effect:** Controls the weight and style of episode information text. Bold text is more prominent against backgrounds; italic text appears more stylized.

#### Font Size
**Type:** Number (1-100) *(Not available for Cutout and Numeral styles)*  
**Default:** 7  
**Effect:** Sets the font size for episode information as a percentage of poster height. Larger values make text more prominent; smaller values make it more subtle. Cutout and Numeral styles calculate font size automatically.

#### Font Color
**Type:** Color picker with alpha *(Not available for Cutout style)*  
**Default:** #FFFFFFFF (white)  
**Effect:** Sets the ARGB hex color for episode number text. Includes alpha channel for transparency control. Format is #AARRGGBB where AA=alpha, RR=red, GG=green, BB=blue. Cutout style uses transparent cutouts instead of colored text.

## Episode Title

### Show Title
**Type:** Checkbox  
**Default:** Enabled  
**Effect:** Controls whether the episode title text is displayed on the poster. When enabled, the episode's name will be rendered using the configured title styling options.

#### Font
**Type:** Dropdown  
**Options:** Various font families  
**Default:** Arial  
**Effect:** Sets the font family for episode title text. Should complement the episode information font choice for visual consistency.

#### Font Style
**Type:** Dropdown  
**Options:** Normal, Bold, Italic, Bold Italic  
**Default:** Bold  
**Effect:** Controls the weight and style of episode title text. Title text is typically larger than episode information, so style choice significantly impacts visual hierarchy.

#### Font Size
**Type:** Number (1-100)  
**Default:** 10  
**Effect:** Sets the font size for episode title as a percentage of poster height. Generally larger than episode information font size to create visual hierarchy and improve readability.

#### Font Color
**Type:** Color picker with alpha  
**Default:** #FFFFFFFF (white)  
**Effect:** Sets the ARGB hex color for episode title text. Includes alpha channel for transparency effects. Should provide sufficient contrast against the background for readability.

## Overlay

### Overlay Color
**Type:** Color picker with alpha  
**Default:** #66000000 (semi-transparent black)  
**Effect:** Sets the primary ARGB hex color for background overlay tinting applied over the extracted frame. Creates a semi-transparent layer that improves text readability and visual cohesion. Alpha channel controls transparency level.

### Overlay Gradient
**Type:** Dropdown  
**Options:** None, Left to Right, Bottom to Top, Top Left Corner to Bottom Right Corner, Top Right Corner to Bottom Left Corner  
**Default:** None  
**Effect:** Controls the direction and type of gradient overlay effect:
- **None:** Solid color overlay using Overlay Color
- **Left to Right:** Horizontal gradient from primary to secondary color
- **Bottom to Top:** Vertical gradient from primary to secondary color
- **Diagonal options:** Creates corner-to-corner gradient effects

#### Secondary Overlay Color
**Type:** Color picker with alpha *(Gradient styles only)*  
**Default:** #66000000 (semi-transparent black)  
**Effect:** Sets the end color for gradient overlays. When a gradient is selected, the overlay transitions from the primary Overlay Color to this Secondary Overlay Color in the specified direction.

## Static Graphic

### Graphic File Path
**Type:** Text input  
**Default:** Empty  
**Effect:** Specifies the absolute file path to a static graphic image that will be overlaid on all generated posters. The graphic is positioned above the background image but below text elements. Supports PNG, JPG, and WEBP formats. Leave empty to disable static graphics.

#### Graphic Position
**Type:** Dropdown *(When graphic path is specified)*  
**Options:** Top, Center, Bottom  
**Default:** Center  
**Effect:** Controls the vertical placement of the static graphic within the poster safe area. Combined with Graphic Alignment to determine final positioning.

#### Graphic Alignment
**Type:** Dropdown *(When graphic path is specified)*  
**Options:** Left, Center, Right  
**Default:** Center  
**Effect:** Controls the horizontal placement of the static graphic within the poster safe area. Works together with Graphic Position for precise placement control.

#### Graphic Width (%)
**Type:** Number (1-100) *(When graphic path is specified)*  
**Default:** 25  
**Effect:** Sets the graphic width as a percentage of the poster width. The graphic maintains its aspect ratio, so height is calculated automatically. Larger values make the graphic more prominent.

#### Graphic Height (%)
**Type:** Number (1-100) *(When graphic path is specified)*  
**Default:** 25  
**Effect:** Sets the maximum graphic height as a percentage of the poster height. The final size respects both width and height constraints while maintaining aspect ratio, using whichever constraint is more restrictive.

## Database Management

### Reset History
**Type:** Button  
**Effect:** Clears all episode processing history from the plugin's database. After resetting, all episodes will be considered unprocessed and will be regenerated on the next run of the scheduled task or provider request. This action cannot be undone and may result in lengthy processing times for large libraries.

## Configuration Interactions

### Style-Specific Visibility
Many settings are only visible or functional with specific poster styles:
- **Cutout options** (border, type) only appear when Cutout style is selected
- **Logo options** (alignment, position, height) only appear when Logo style is selected
- **Font Color** is not available for Cutout style (uses transparent cutouts)
- **Font Size** for episode information is not configurable for Cutout and Numeral styles

### Dependent Settings
Some settings depend on others being enabled:
- **Extraction window** and **brightness** settings require "Enable Poster Episode" to be enabled
- **Letterbox detection** settings require "Enable Letterbox Detection" to be enabled
- **Gradient settings** only appear when a gradient style is selected
- **Graphic positioning** settings only appear when a graphic file path is specified

### Safe Area Impact
The Safe Area setting affects the positioning of:
- All text elements (episode information and titles)
- Series logos in Logo style
- Static graphics
- Letterbox detection boundaries

Text and graphics are positioned within the safe area boundaries to ensure they don't appear too close to poster edges.
