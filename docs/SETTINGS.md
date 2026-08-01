# Episode Poster Generator Settings

Settings apply to the active configuration. Any series without its own configuration uses the default. Labels below match the configuration page.

## Plugin

* **Enable Poster Generation**: generate posters for episodes, both automatically during a metadata refresh when an episode has no image and on demand from an episode's Edit Images dialog. Turning this off disables both. Default on.
* **Choices In Edit Images**: how many alternate posters to offer when replacing an episode's poster from the Edit Images dialog, 1 to 10. Each one is rendered from a different frame and costs its own ffmpeg extraction, so higher values make the dialog slower to open. An episode with no poster yet is only offered one, because that request comes from an automatic refresh that keeps a single image. Default 3.

## Configurations

* **Active Configuration**: the configuration being viewed and edited. The default one applies to every series not assigned elsewhere.
* **New, Rename, Delete**: manage named configurations. The default configuration cannot be renamed or deleted.
* **Export, Import**: save a configuration to JSON, or load one as a new configuration.
* **Assigned Series**: the series that use the active configuration. A series belongs to one configuration at a time.

## Canvas

* **Canvas Background**: the poster's base image. Extract Frame from Episode, Use Series Backdrop, or No Background. Default Extract Frame.
* **Save Extracted Frame as Episode Backdrop**: also upload the cropped frame as the episode backdrop. Default off.
* **Extraction Start (%)**: earliest point to pull a frame from, as a percent of runtime. Default 20.
* **Extraction End (%)**: latest point to pull a frame from, as a percent of runtime. Default 80.
* **Brighten HDR (%)**: percent to brighten frames pulled from HDR sources. Default 25.

## Letterbox

* **Enable Letterbox Detection**: crop black bars off an extracted frame. Default on.
* **Black Threshold**: brightness below which a pixel counts as black, 0 to 255. Default 25.
* **Detection Confidence (%)**: confidence required before cropping detected bars. Default 85.

## Poster

* **Style**: the layout. Standard, Brush, Cutout, Fade, Frame, Frosted Glass, Logo, Numeral, Split, Striped, or Timeline. Default Standard.
  * Timeline draws a season progress bar. It needs the season's episode count, so the bar renders full when the count is unknown, and it does not refresh on its own as more episodes are added to an airing season.
  * Striped draws its sash from the overlay colors. The main band uses Overlay Color and the pinstripes use Secondary Overlay Color.
* **Fill Strategy**: how the canvas fits the poster. Original, Fill, or Fit. Default Original.
* **Aspect Ratio**: output aspect ratio. Default 16:9.
* **Safe Area**: margin kept clear around all edges. The percent applies to the poster height and the same pixel amount is used on all four sides. Default 5.
* **Element Spacing**: gap kept between stacked elements such as the logo, episode code, and title, as a percent of the poster height. Every style resolves its spacing through this one value, so raising it pushes elements further apart everywhere. Default 2.

## Outline (Style is Cutout or Brush)

* **Enable Outline**: draw a contrasting outline around the cut-out shape — the episode code for Cutout, the brush stroke for Brush. Default on.

## Cutout (Style is Cutout)

* **Type**: what the cutout shows. Code such as S01E05, or Text spelled out. Default Code.

## Logo (Style is Logo)

* **Logo Position**: vertical placement. Top, Center, or Bottom. Default Center.
* **Logo Alignment**: horizontal placement. Left, Center, or Right. Default Center.
* **Logo Height**: logo height as a percent of the poster. Default 30.

## Episode Text

* **Show Episode**: draw the episode code or number. Default on.
* **Font**: font family for the episode text. Default Arial.
* **Use Custom Font**: use a font file instead of a family. Default off.
* **Font Path**: path to the custom font file.
* **Font Style**: weight or style such as Bold. Default Bold.
* **Font Size**: text size as a percent of the poster. Default 7.
* **Font Color**: text color as ARGB hex. Default #FFFFFFFF.

## Title Text

* **Show Title**: draw the episode title. Default on.
* **Long Titles**: what to do when a title does not fit. Ellipsis trims it. Abbreviate shortens it in stages: first the text before a divider, then the first sentence, then the first letter of every word with periods such as L.O.T.R., keeping dividers and skipping middle initials if still too wide. Drop Name hides it. Default Ellipsis.
* **Font**: font family for the title. Default Arial.
* **Use Custom Font**: use a font file instead of a family. Default off.
* **Font Path**: path to the custom font file.
* **Font Style**: weight or style such as Bold. Default Bold.
* **Font Size**: text size as a percent of the poster. Default 10.
* **Font Color**: text color as ARGB hex. Default #FFFFFFFF.

## Overlay

* **Palette-Derived Colors**: replace the overlay color channels with the dominant color sampled from each episode's image. The secondary color becomes a darker shade of it and the alpha values below still apply. Default off.
* **Overlay Color**: color drawn over the canvas as ARGB hex. Default #66000000.
* **Overlay Gradient**: gradient direction. None, Left To Right, Bottom To Top, or a diagonal corner. Default None.
* **Secondary Overlay Color**: the gradient's second color as ARGB hex. Default #66000000.

## Graphic

* **Graphic File Path**: path to an image drawn on the poster.
* **Graphic Width (%)**: graphic width as a percent of the poster. Default 25.
* **Graphic Height (%)**: graphic height as a percent of the poster. Default 25.
* **Graphic Position**: vertical placement. Top, Center, or Bottom. Default Center.
* **Graphic Alignment**: horizontal placement. Left, Center, or Right. Default Center.
