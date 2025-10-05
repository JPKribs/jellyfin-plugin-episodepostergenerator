# Episode Poster Generator Settings

This document provides a comprehensive guide to all configuration settings available in the Episode Poster Generator plugin. Settings are organized into Plugin Settings (global) and Poster Settings (per-configuration).

---

## Plugin Settings

These settings apply globally to the entire plugin.

### Enable Provider
**Type:** Checkbox  
**Default:** Enabled  
**Effect:** Controls whether the plugin functions as a metadata provider for episode posters. When enabled, the plugin will automatically generate posters when Jellyfin requests episode images. When disabled, the plugin will not respond to image provider requests.

### Enable Scheduled Task
**Type:** Checkbox  
**Default:** Enabled  
**Effect:** Controls whether the plugin appears as a scheduled task in Jellyfin's task management. When enabled, you can run batch poster generation through the scheduled tasks interface. When disabled, the scheduled task will not be available.

---

## Poster Configuration Management

The plugin supports multiple poster configurations, allowing different series to use different poster styles and settings.

### Active Configuration
**Type:** Dropdown  
**Effect:** Selects which poster configuration to view and edit. The dropdown displays all available configurations by name. The first configuration is always labeled "Default" and applies to all series not explicitly assigned to other configurations.

### Configuration Actions

#### + New
**Type:** Button  
**Effect:** Creates a new poster configuration. Prompts for a configuration name (required and must be unique). The new configuration is created with default settings and becomes the active configuration. New configurations must be assigned to specific series to be used.

#### Rename
**Type:** Button  
**Effect:** Renames the currently active configuration. Only available for non-default configurations. Prompts for a new name (required and must be unique). The default configuration cannot be renamed.

#### Delete
**Type:** Button  
**Effect:** Deletes the currently active configuration. Only available for non-default configurations. Requires confirmation before deletion. Series assigned to deleted configurations will fall back to the default configuration. The default configuration cannot be deleted.

### Assigned Series
**Visibility:** Hidden for default configuration  
**Effect:** Displays which series are assigned to use the currently active configuration. Each series appears as a card showing the series poster thumbnail and name. Click the × button to remove a series assignment.

#### + Add Series
**Type:** Button  
**Visibility:** Only visible for non-default configurations  
**Effect:** Opens a modal dialog to assign series to the current configuration. Shows all available series with search functionality. Series already assigned to other configurations are disabled and marked as "already assigned". A series can only be assigned to one configuration at a time.

---

## Poster Settings

These settings are configured per poster configuration. Each configuration can have completely different settings.

### Poster Extraction

#### Enable Poster Episode
**Type:** Checkbox  
**Default:** Enabled  
**Effect:** Controls whether the plugin extracts frames from video files to use as poster backgrounds. When enabled, the plugin will analyze video content and extract representative frames. When disabled, the plugin will create posters with transparent or solid color backgrounds only.

#### Extraction Start (%)
**Type:** Number (0-100)  
**Default:** 20  
**Requires:** Enable Poster Episode  
**Effect:** Sets the starting point for frame extraction as a percentage of the episode duration. The plugin will skip the first X% of the episode to avoid opening credits and intro sequences.

#### Extraction End (%)
**Type:** Number (0-100)  
**Default:** 80  
**Requires:** Enable Poster Episode  
**Effect:** Sets the ending point for frame extraction as a percentage of the episode duration. The plugin will stop extracting frames at X% of the episode to avoid end credits and outro sequences.

#### Brighten HDR (%)
**Type:** Number (0-100)  
**Default:** 25  
**Requires:** Enable Poster Episode  
**Effect:** Applies brightness adjustment specifically to HDR content as a percentage increase. This helps HDR frames appear properly exposed in poster images, as HDR content can appear dark when converted to standard images.

#### Enable Hardware Accelerated Decoding
**Type:** Checkbox  
**Default:** Disabled  
**Requires:** Enable Poster Episode  
**Effect:** Uses Jellyfin's hardware acceleration settings for video decoding during frame extraction. When enabled, the plugin will attempt to use GPU-accelerated decoding for faster processing. Falls back to software decoding if hardware acceleration fails.

### Letterbox Detection

#### Enable Letterbox Detection
**Type:** Checkbox  
**Default:** Enabled  
**Requires:** Enable Poster Episode  
**Effect:** Automatically detects and removes black letterbox (horizontal) and pillarbox (vertical) borders from extracted poster images. When enabled, the plugin analyzes pixel brightness to identify and crop away black borders.

#### Black Threshold
**Type:** Number (0-255)  
**Default:** 25  
**Requires:** Enable Letterbox Detection  
**Effect:** Sets the pixel brightness threshold for black detection. Pixels with brightness values at or below this threshold are considered "black" for border detection purposes. Lower values detect only very dark pixels; higher values include darker gray pixels.

#### Detection Confidence (%)
**Type:** Number (50-100)  
**Default:** 85  
**Requires:** Enable Letterbox Detection  
**Effect:** Sets the percentage of pixels that must be black for a row or column to be considered letterboxing. Higher values require more pixels to be black before cropping occurs, making detection more conservative.

### Poster Style

#### Style
**Type:** Dropdown  
**Options:** Standard, Cutout, Frame, Logo, Numeral  
**Default:** Standard  
**Effect:** Determines the overall poster design and layout approach:
- **Standard:** Classic poster with background image and bottom-aligned text
- **Cutout:** Large text with transparent cutout effect revealing background
- **Frame:** Decorative border frame with episode title
- **Logo:** Series logo-focused design with clean typography
- **Numeral:** Roman numeral episode numbers with elegant styling

#### Enable Cutout Text Border
**Type:** Checkbox  
**Default:** Enabled  
**Visibility:** Cutout style only  
**Effect:** Adds a contrasting border around cutout text for better visibility against varied backgrounds. The border color is automatically calculated to contrast with the overlay color.

#### Type
**Type:** Dropdown  
**Options:** Code, Text  
**Default:** Code  
**Visibility:** Cutout style only  
**Effect:** Controls the format of cutout text display:
- **Code:** Shows episode in S01E01 format
- **Text:** Shows episode number as words (e.g., "THREE")

#### Logo Alignment
**Type:** Dropdown  
**Options:** Left, Center, Right  
**Default:** Center  
**Visibility:** Logo style only  
**Effect:** Controls horizontal positioning of the series logo within the poster.

#### Logo Position
**Type:** Dropdown  
**Options:** Top, Center, Bottom  
**Default:** Center  
**Visibility:** Logo style only  
**Effect:** Controls vertical positioning of the series logo within the poster safe area.

#### Logo Height
**Type:** Number (1-100)  
**Default:** 30  
**Visibility:** Logo style only  
**Effect:** Sets the logo height as a percentage of the total poster height. Logo width is automatically calculated to maintain aspect ratio.

### Poster Dimensions

#### Fill Strategy
**Type:** Dropdown  
**Options:** Original, Fill, Fit  
**Default:** Original  
**Effect:** Controls how extracted video frames are resized to create poster dimensions:
- **Original:** Preserves the exact frame dimensions
- **Fill:** Stretches the frame to match the target aspect ratio
- **Fit:** Crops the frame to fit the target aspect ratio

#### Aspect Ratio
**Type:** Text  
**Default:** 16:9  
**Visibility:** Fill and Fit strategies only  
**Effect:** Defines the target aspect ratio for poster dimensions. Format should be width:height (e.g., 16:9, 3:2, 4:3).

#### Safe Area
**Type:** Number (1-100)  
**Default:** 5  
**Effect:** Sets the percentage of vertical and horizontal space preserved around poster edges as a safe area. Text and graphic elements are positioned within this safe area to ensure they don't appear too close to edges.

#### File Type
**Type:** Dropdown  
**Options:** JPEG, PNG, WEBP  
**Default:** WEBP  
**Effect:** Determines the file format for generated poster images:
- **JPEG:** Good compression, widely supported, no transparency
- **PNG:** Lossless quality, transparency support, larger files
- **WEBP:** Excellent compression, modern format, good quality

### Episode Information

#### Show Episode
**Type:** Checkbox  
**Default:** Enabled  
**Effect:** Controls whether season and episode information (e.g., "S01E05") is displayed on the poster. Always enabled for Cutout and Numeral styles as episode information is central to their design.

#### Font
**Type:** Dropdown  
**Default:** Arial  
**Requires:** Show Episode  
**Effect:** Sets the font family for episode number and season information text. Available fonts include Arial, Helvetica, Times New Roman, and many others.

#### Font Style
**Type:** Dropdown  
**Options:** Normal, Bold, Italic, Bold Italic  
**Default:** Bold  
**Requires:** Show Episode  
**Effect:** Controls the weight and style of episode information text.

#### Font Size
**Type:** Number (1-100)  
**Default:** 7  
**Requires:** Show Episode  
**Visibility:** Not available for Cutout and Numeral styles  
**Effect:** Sets the font size for episode information as a percentage of poster height.

#### Font Color
**Type:** Color picker with alpha  
**Default:** #FFFFFFFF (white)  
**Requires:** Show Episode  
**Visibility:** Not available for Cutout style  
**Effect:** Sets the ARGB hex color for episode number text. Format is #AARRGGBB where AA=alpha, RR=red, GG=green, BB=blue.

### Episode Title

#### Show Title
**Type:** Checkbox  
**Default:** Enabled  
**Visibility:** Not available for Frame style  
**Effect:** Controls whether the episode title text is displayed on the poster.

#### Font
**Type:** Dropdown  
**Default:** Arial  
**Requires:** Show Title  
**Effect:** Sets the font family for episode title text.

#### Font Style
**Type:** Dropdown  
**Options:** Normal, Bold, Italic, Bold Italic  
**Default:** Bold  
**Requires:** Show Title  
**Effect:** Controls the weight and style of episode title text.

#### Font Size
**Type:** Number (1-100)  
**Default:** 10  
**Requires:** Show Title  
**Effect:** Sets the font size for episode title as a percentage of poster height.

#### Font Color
**Type:** Color picker with alpha  
**Default:** #FFFFFFFF (white)  
**Requires:** Show Title  
**Effect:** Sets the ARGB hex color for episode title text.

### Overlay

#### Overlay Color
**Type:** Color picker with alpha  
**Default:** #66000000 (semi-transparent black)  
**Effect:** Sets the primary ARGB hex color for background overlay tinting applied over the extracted frame. Creates a semi-transparent layer that improves text readability.

#### Overlay Gradient
**Type:** Dropdown  
**Options:** None, Left to Right, Bottom to Top, Top Left Corner to Bottom Right Corner, Top Right Corner to Bottom Left Corner  
**Default:** None  
**Effect:** Controls the direction and type of gradient overlay effect:
- **None:** Solid color overlay using Overlay Color
- Other options: Creates gradient from primary to secondary color in the specified direction

#### Secondary Overlay Color
**Type:** Color picker with alpha  
**Default:** #66000000 (semi-transparent black)  
**Requires:** Overlay Gradient (non-None)  
**Effect:** Sets the end color for gradient overlays.

### Static Graphic

#### Graphic File Path
**Type:** Text input  
**Default:** Empty  
**Effect:** Specifies the absolute file path to a static graphic image that will be overlaid on all generated posters. The graphic is positioned above the background image but below text elements. Supports PNG, JPG, and WEBP formats. Leave empty to disable.

#### Graphic Position
**Type:** Dropdown  
**Options:** Top, Center, Bottom  
**Default:** Center  
**Requires:** Graphic File Path (non-empty)  
**Effect:** Controls the vertical placement of the static graphic within the poster safe area.

#### Graphic Alignment
**Type:** Dropdown  
**Options:** Left, Center, Right  
**Default:** Center  
**Requires:** Graphic File Path (non-empty)  
**Effect:** Controls the horizontal placement of the static graphic within the poster safe area.

#### Graphic Width (%)
**Type:** Number (1-100)  
**Default:** 25  
**Requires:** Graphic File Path (non-empty)  
**Effect:** Sets the graphic width as a percentage of the poster width. Height is calculated automatically to maintain aspect ratio.

#### Graphic Height (%)
**Type:** Number (1-100)  
**Default:** 25  
**Requires:** Graphic File Path (non-empty)  
**Effect:** Sets the maximum graphic height as a percentage of the poster height. The final size respects both width and height constraints while maintaining aspect ratio.

---

## Database Management

### Reset History
**Type:** Button  
**Effect:** Clears all episode processing history from the plugin's database. After resetting, all episodes will be considered unprocessed and will be regenerated on the next run. This action cannot be undone and may result in lengthy processing times for large libraries. Displays count of cleared records upon completion.

---

## Configuration Workflow

### How Configurations Work

1. **Default Configuration:** The first configuration is always the default. It applies to all series that are not explicitly assigned to other configurations. It cannot be deleted or renamed.

2. **Custom Configurations:** Create additional configurations with the "+ New" button. Each configuration has a unique name and independent settings.

3. **Series Assignment:** Assign series to specific configurations using the "+ Add Series" button. A series can only be assigned to one configuration at a time. Unassigned series use the default configuration.

4. **Configuration Priority:** When generating a poster:
   - Plugin checks if the series is assigned to a specific configuration
   - If yes, uses that configuration's settings
   - If no, uses the default configuration's settings

### Best Practices

- **Test configurations** on a single series before assigning many series
- **Use descriptive names** for configurations (e.g., "Anime Style", "Dark Theme", "Minimal")
- **Group similar series** together with shared configurations
- **Keep the default configuration** as a safe fallback with universally acceptable settings
- **Document custom configurations** if sharing plugin setup across team members