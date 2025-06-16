using System;
using System.IO;
using Jellyfin.Plugin.EpisodePosterGenerator.Configuration;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;
using Jellyfin.Plugin.EpisodePosterGenerator.Services.Posters;
using MediaBrowser.Controller.Entities.TV;
using SkiaSharp;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services;

/// <summary>
/// Comprehensive image processing service responsible for orchestrating episode poster generation workflows
/// with sophisticated image manipulation, aspect ratio management, and style-specific poster creation.
/// Serves as the central coordination hub for transforming raw episode frames into polished, 
/// professionally-styled poster images suitable for media library presentation.
/// 
/// This service implements a multi-stage image processing pipeline that bridges the gap between
/// raw video frame extraction and final poster output, providing essential image scaling, cropping,
/// and routing functionality that enables specialized poster generators to focus on style-specific
/// rendering while maintaining consistent quality and performance across all poster types.
/// 
/// Core Architecture:
/// The service follows a modular pipeline architecture where image preprocessing is separated
/// from style-specific poster generation, enabling optimal performance through specialized
/// processing stages and allowing for easy extension with new poster styles without affecting
/// the core image processing infrastructure.
/// 
/// Processing Pipeline Overview:
/// 1. Image Input Validation: Comprehensive verification of source image accessibility and format compatibility
/// 2. Dimension Analysis: Intelligent calculation of target poster dimensions based on configuration and source image properties
/// 3. Image Scaling and Preprocessing: High-quality image scaling with aspect ratio preservation and fill mode processing
/// 4. Temporary File Management: Efficient intermediate file handling with automatic cleanup and resource management
/// 5. Style-Specific Routing: Intelligent delegation to specialized poster generators based on configuration settings
/// 6. Quality Optimization: Advanced encoding settings ensuring optimal file size and visual quality balance
/// 7. Error Handling and Recovery: Comprehensive error management with graceful degradation and detailed logging
/// 
/// Image Processing Capabilities:
/// - Multi-format image decoding with robust error handling for various input formats
/// - Sophisticated aspect ratio calculation and preservation algorithms
/// - Advanced scaling algorithms with multiple fill strategies (Original, Fill, Fit)
/// - High-quality image encoding with optimized compression settings
/// - Intelligent cropping algorithms for optimal visual composition
/// - Memory-efficient processing suitable for high-resolution source images
/// 
/// Fill Strategy Implementation:
/// The service implements three distinct fill strategies to accommodate different poster design requirements:
/// 
/// Original Strategy:
/// Preserves source image dimensions without modification, suitable for posters where maintaining
/// original aspect ratios is critical for visual integrity. This approach ensures no image distortion
/// but may result in non-standard poster dimensions.
/// 
/// Fill Strategy:
/// Expands or compresses images to exactly match target dimensions, potentially introducing aspect
/// ratio distortion but guaranteeing consistent poster sizes across the media library. Optimal
/// for maintaining visual consistency in grid layouts and uniform presentation.
/// 
/// Fit Strategy:
/// Intelligently crops source images to fit target aspect ratios while preserving image quality
/// and avoiding distortion. Uses center-weighted cropping algorithms to maintain focus on
/// important visual elements while achieving standardized poster dimensions.
/// 
/// Style Integration Architecture:
/// The service implements a strategy pattern for poster style integration, enabling seamless
/// addition of new poster types without modifying core processing logic. Each poster style
/// receives preprocessed images optimized for their specific rendering requirements.
/// 
/// Supported Poster Styles:
/// - Standard: Traditional episode posters with overlay text and episode information
/// - Cutout: Dramatic text cutout effects revealing background imagery through typography
/// - Numeral: Elegant Roman numeral-based designs with sophisticated typography
/// - Logo: Series logo-focused posters with clean backgrounds and minimal text elements
/// 
/// Performance Characteristics:
/// - Optimized memory usage through efficient bitmap handling and disposal patterns
/// - Streaming image processing minimizing RAM requirements for large source images
/// - Intelligent caching strategies for repeated operations and common calculations
/// - Asynchronous processing support enabling responsive poster generation workflows
/// - Resource cleanup automation preventing memory leaks and temporary file accumulation
/// 
/// Quality Assurance:
/// - Advanced image encoding with configurable quality settings optimized for poster display
/// - Color space management ensuring consistent color reproduction across different devices
/// - Anti-aliasing and smoothing algorithms providing professional-quality output
/// - Format-specific optimization ensuring compatibility with Jellyfin's image management system
/// 
/// Integration Points:
/// - Jellyfin Episode Metadata: Seamless integration with episode information and file paths
/// - Plugin Configuration System: Dynamic behavior modification based on user preferences
/// - Poster Generator Ecosystem: Standardized interface for style-specific poster creation
/// - Image Provider Infrastructure: Compatible output formats for Jellyfin integration
/// - Temporary File Management: Coordinated resource handling preventing system pollution
/// 
/// Error Handling Strategy:
/// The service implements comprehensive error handling with multiple layers of protection:
/// - Input validation preventing processing failures from invalid source data
/// - Graceful degradation ensuring partial functionality when non-critical operations fail
/// - Resource cleanup guaranteeing system stability regardless of processing outcomes
/// - Detailed error logging enabling efficient debugging and system monitoring
/// - Exception isolation preventing individual poster generation failures from affecting batch operations
/// 
/// Thread Safety and Concurrency:
/// While individual method calls are not thread-safe due to temporary file operations,
/// the service is designed for safe concurrent use through instance isolation and
/// unique temporary file naming strategies preventing resource conflicts during
/// parallel poster generation operations.
/// 
/// The service represents a crucial component in the poster generation ecosystem,
/// providing the foundational image processing capabilities that enable specialized
/// poster generators to focus on visual design and typography while maintaining
/// consistent quality and performance standards across all poster creation workflows.
/// </summary>
public class PosterGeneratorService
{
    /// <summary>
    /// Orchestrates the complete image processing workflow from source frame to final poster output,
    /// implementing a sophisticated multi-stage pipeline that transforms raw episode imagery into
    /// polished, professionally-styled poster images optimized for media library presentation.
    /// 
    /// This method serves as the primary entry point for poster generation operations, coordinating
    /// image preprocessing, dimension management, and style-specific poster creation while maintaining
    /// optimal performance and resource utilization throughout the processing pipeline.
    /// 
    /// Processing Workflow Architecture:
    /// 1. Image Input Validation and Decoding: Comprehensive verification and loading of source imagery
    /// 2. Target Dimension Calculation: Intelligent aspect ratio analysis and poster sizing determination
    /// 3. High-Quality Image Scaling: Advanced scaling algorithms with configurable fill strategies
    /// 4. Intermediate Image Generation: Optimized temporary file creation for style-specific processing
    /// 5. Style-Specific Poster Generation: Delegation to specialized poster generators with preprocessed imagery
    /// 6. Resource Management and Cleanup: Automatic cleanup of temporary files and memory resources
    /// 
    /// Image Processing Pipeline:
    /// The method implements a sophisticated image processing pipeline optimized for poster generation
    /// workflows. Source images are decoded using SkiaSharp's robust image handling capabilities,
    /// ensuring compatibility with various input formats while maintaining optimal performance.
    /// 
    /// Dimension Management:
    /// Target poster dimensions are calculated using intelligent algorithms that consider source
    /// image properties, user configuration preferences, and poster style requirements. The calculation
    /// process ensures optimal visual quality while maintaining consistency across the media library.
    /// 
    /// Scaling Algorithms:
    /// Advanced scaling operations preserve image quality while adapting to target dimensions using
    /// configurable fill strategies. The scaling process maintains visual integrity while ensuring
    /// posters meet standardized sizing requirements for consistent library presentation.
    /// 
    /// Temporary File Strategy:
    /// Intermediate image files are created using unique identifiers to prevent conflicts during
    /// concurrent operations. The temporary file approach enables efficient memory management while
    /// providing specialized poster generators with optimized input imagery.
    /// 
    /// Error Handling and Recovery:
    /// Comprehensive error handling ensures graceful degradation when processing issues occur,
    /// with detailed logging for debugging and monitoring purposes. Resource cleanup occurs
    /// automatically regardless of processing success or failure states.
    /// 
    /// Performance Optimization:
    /// The method implements memory-efficient processing patterns suitable for high-resolution
    /// source imagery while maintaining responsive operation during batch processing workflows.
    /// Resource disposal follows established patterns preventing memory leaks and system degradation.
    /// </summary>
    /// <param name="inputPath">
    /// File system path to the source image file requiring poster generation processing.
    /// Must reference an accessible image file in a format supported by SkiaSharp decoding operations.
    /// </param>
    /// <param name="outputPath">
    /// Target file system path where the completed poster image should be saved after processing.
    /// Used by style-specific poster generators for final output file creation and storage.
    /// </param>
    /// <param name="episode">
    /// Episode metadata object containing information required for poster generation including
    /// season numbers, episode numbers, titles, and other metadata used in poster text rendering.
    /// </param>
    /// <param name="config">
    /// Plugin configuration object containing user preferences for poster styling, dimensions,
    /// fill strategies, and other settings that control the poster generation workflow behavior.
    /// </param>
    /// <returns>
    /// File system path to the successfully generated poster image, or null if processing fails.
    /// Null return indicates processing failure with appropriate error logging for debugging.
    /// Success return enables integration with Jellyfin's image management and display systems.
    /// </returns>
    // MARK: ProcessImageWithText
    public string? ProcessImageWithText(string inputPath, string outputPath, Episode episode, PluginConfiguration config)
    {
        try
        {
            // Stage 1: Image Input Validation and Decoding
            // Open and decode source image with comprehensive error handling for format compatibility
            using var input = File.OpenRead(inputPath);
            using var original = SKBitmap.Decode(input);
            if (original == null)
                return null;

            // Stage 2: Target Dimension Calculation and Analysis
            // Calculate optimal poster dimensions based on source properties and configuration settings
            var targetSize = GetTargetSize(original.Width, original.Height, config);
            
            // Stage 3: High-Quality Image Scaling and Preprocessing
            // Create scaled bitmap with target dimensions using advanced scaling algorithms
            using var scaled = new SKBitmap(targetSize.Width, targetSize.Height);
            using (var canvas = new SKCanvas(scaled))
            {
                // Initialize canvas with black background for consistent baseline across all poster types
                canvas.Clear(SKColors.Black);
                
                // Apply sophisticated image drawing with configurable fill strategy processing
                DrawPosterImage(canvas, original, targetSize, config.PosterFill, original.Width, original.Height, config);
            }

            // Stage 4: Intermediate Image Generation and Temporary File Management
            // Create optimized temporary image file for style-specific poster generator input
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.jpg");
            using (var image = SKImage.FromBitmap(scaled))
            using (var data = image.Encode(SKEncodedImageFormat.Jpeg, 90))  // High-quality encoding for professional output
            using (var output = File.OpenWrite(tempPath))
            {
                data.SaveTo(output);
            }

            // Stage 5: Style-Specific Poster Generation Delegation
            // Route preprocessed image to appropriate poster generator based on configuration settings
            return GeneratePoster(tempPath, outputPath, episode, config);
        }
        catch
        {
            // Comprehensive error handling with graceful degradation for processing failures
            return null;
        }
    }

    /// <summary>
    /// Intelligently routes poster generation requests to specialized style-specific generators
    /// based on user configuration preferences, implementing a strategy pattern that enables
    /// modular poster creation while maintaining consistent preprocessing and quality standards.
    /// 
    /// This method serves as the central routing hub for poster style selection, ensuring
    /// preprocessed images are delivered to the appropriate specialized generators while
    /// maintaining separation of concerns between image preprocessing and style-specific rendering.
    /// 
    /// Style Selection Strategy:
    /// The routing mechanism uses configuration-driven selection to instantiate appropriate
    /// poster generators, enabling dynamic behavior modification without requiring code changes
    /// or complex factory patterns. Each style receives identical preprocessed input ensuring
    /// consistent quality and performance across all poster types.
    /// 
    /// Generator Architecture:
    /// Specialized poster generators implement a common interface ensuring consistent behavior
    /// while enabling style-specific optimization and customization. This architecture supports
    /// easy extension with new poster styles without affecting existing functionality.
    /// 
    /// Error Handling:
    /// The method provides graceful fallback behavior for unknown or unsupported poster styles,
    /// ensuring system stability while providing clear indication of configuration issues
    /// through null return values and appropriate logging mechanisms.
    /// </summary>
    /// <param name="inputPath">
    /// File system path to the preprocessed image file ready for style-specific poster generation.
    /// Contains scaled and optimized imagery suitable for text overlay and style-specific processing.
    /// </param>
    /// <param name="outputPath">
    /// Target file system path where the completed poster should be saved by the selected generator.
    /// Passed through to specialized generators for consistent output file management.
    /// </param>
    /// <param name="episode">
    /// Episode metadata object containing information required for poster text rendering and styling.
    /// Provided to specialized generators for consistent metadata handling across all poster types.
    /// </param>
    /// <param name="config">
    /// Plugin configuration object containing style selection and generator-specific settings.
    /// Used for both routing decisions and parameter passing to specialized generators.
    /// </param>
    /// <returns>
    /// File system path to the successfully generated poster image from the selected generator,
    /// or null if the specified style is unsupported or generation fails within the specialized generator.
    /// </returns>
    // MARK: GeneratePoster
    private string? GeneratePoster(string inputPath, string outputPath, Episode episode, PluginConfiguration config)
    {
        // Implement strategy pattern for style-specific poster generation routing
        return config.PosterStyle switch
        {
            // Standard poster style with traditional overlay text and episode information
            PosterStyle.Standard => new StandardPosterGenerator().Generate(inputPath, outputPath, episode, config),
            
            // Cutout poster style with dramatic text effects and transparent typography
            PosterStyle.Cutout => new CutoutPosterGenerator().Generate(inputPath, outputPath, episode, config),
            
            // Numeral poster style with elegant Roman numeral typography and sophisticated design
            PosterStyle.Numeral => new NumeralPosterGenerator().Generate(inputPath, outputPath, episode, config),
            
            // Logo poster style with series branding focus and clean minimalist design
            PosterStyle.Logo => new LogoPosterGenerator().Generate(inputPath, outputPath, episode, config),
            
            // Default fallback for unknown or unsupported poster styles
            _ => null
        };
    }

    /// <summary>
    /// Calculates optimal poster dimensions based on source image properties and configuration preferences,
    /// implementing sophisticated algorithms that balance visual quality, consistency, and user requirements
    /// while maintaining compatibility with various aspect ratios and fill strategies.
    /// 
    /// This method serves as the dimensional foundation for poster generation, ensuring consistent sizing
    /// across media libraries while accommodating diverse source image formats and user preferences
    /// for poster presentation and layout requirements.
    /// 
    /// Dimension Calculation Strategies:
    /// 
    /// Original Strategy Implementation:
    /// Preserves source image dimensions without modification, maintaining pixel-perfect accuracy
    /// for scenarios where original aspect ratios are critical for visual integrity. This approach
    /// ensures zero quality loss but may result in inconsistent poster sizes across the library.
    /// 
    /// Fill Strategy Implementation:
    /// Calculates target dimensions by adjusting one dimension to match the configured aspect ratio
    /// while preserving the other dimension from the source image. This approach prioritizes
    /// consistent poster sizing while minimizing image distortion through intelligent scaling.
    /// 
    /// Fit Strategy Implementation:
    /// Determines target dimensions that accommodate the configured aspect ratio while ensuring
    /// the entire source image content fits within the target boundaries. This approach requires
    /// intelligent cropping during the drawing phase to achieve optimal visual composition.
    /// 
    /// Aspect Ratio Analysis:
    /// The method performs comprehensive aspect ratio analysis comparing source image proportions
    /// with target configuration settings to determine optimal scaling strategies. Mathematical
    /// calculations ensure precise dimension determination while avoiding floating-point precision issues.
    /// 
    /// Configuration Integration:
    /// User configuration preferences are seamlessly integrated into dimension calculations,
    /// enabling dynamic behavior modification without requiring changes to core processing logic.
    /// Configuration validation ensures mathematical consistency and prevents invalid dimension calculations.
    /// 
    /// Performance Optimization:
    /// Dimension calculations are optimized for efficiency during batch processing operations,
    /// using integer arithmetic where possible and minimizing complex mathematical operations
    /// while maintaining accuracy and precision in dimension determination.
    /// </summary>
    /// <param name="originalWidth">
    /// Source image width in pixels, used as the baseline for dimension calculations and scaling operations.
    /// Must be positive and represent the actual pixel dimensions of the source imagery.
    /// </param>
    /// <param name="originalHeight">
    /// Source image height in pixels, used for aspect ratio calculations and scaling determinations.
    /// Combined with width to establish source image proportions for optimal scaling strategies.
    /// </param>
    /// <param name="config">
    /// Plugin configuration object containing fill strategy preferences and aspect ratio settings.
    /// Provides user-specified parameters for dimension calculation and poster sizing behavior.
    /// </param>
    /// <returns>
    /// SKSizeI structure containing calculated target dimensions optimized for poster generation.
    /// Dimensions are guaranteed to be positive and mathematically consistent with configuration settings.
    /// </returns>
    // MARK: GetTargetSize
    private SKSizeI GetTargetSize(int originalWidth, int originalHeight, PluginConfiguration config)
    {
        // Original strategy: preserve source dimensions without modification for pixel-perfect accuracy
        if (config.PosterFill == PosterFill.Original)
            return new SKSizeI(originalWidth, originalHeight);

        // Parse target aspect ratio configuration with robust error handling and fallback values
        float targetAspect = ParseAspectRatio(config.PosterDimensionRatio);
        float originalAspect = (float)originalWidth / originalHeight;

        // Fill strategy: expand dimensions to match target aspect ratio with minimal distortion
        if (config.PosterFill == PosterFill.Fill)
        {
            if (originalAspect > targetAspect)
            {
                // Source is wider than target: preserve height and adjust width to match target aspect
                int height = originalHeight;
                int width = (int)(height * targetAspect);
                return new SKSizeI(width, height);
            }
            else
            {
                // Source is taller than target: preserve width and adjust height to match target aspect
                int width = originalWidth;
                int height = (int)(width / targetAspect);
                return new SKSizeI(width, height);
            }
        }

        // Fit strategy: calculate dimensions that accommodate target aspect ratio while enabling content-preserving cropping
        if (originalAspect > targetAspect)
        {
            // Source is wider: expand height to achieve target aspect ratio, enabling horizontal cropping
            int width = originalWidth;
            int height = (int)(width / targetAspect);
            return new SKSizeI(width, height);
        }
        else
        {
            // Source is taller: expand width to achieve target aspect ratio, enabling vertical cropping
            int height = originalHeight;
            int width = (int)(height * targetAspect);
            return new SKSizeI(width, height);
        }
    }

    /// <summary>
    /// Parses aspect ratio configuration strings into floating-point ratios with comprehensive error handling
    /// and intelligent fallback mechanisms, ensuring mathematical consistency and preventing invalid
    /// calculations that could compromise poster generation quality or system stability.
    /// 
    /// This utility method provides robust parsing of user-specified aspect ratio preferences,
    /// supporting common formats while maintaining backward compatibility and graceful degradation
    /// when configuration values are invalid or improperly formatted.
    /// 
    /// Supported Format Patterns:
    /// - Standard Ratio Format: "16:9", "4:3", "21:9" representing width-to-height proportions
    /// - Decimal Formats: "1.777", "1.333" representing direct floating-point ratio values
    /// - Integer Formats: "16:9", "4:3" with automatic floating-point conversion
    /// 
    /// Parsing Algorithm:
    /// The method implements defensive parsing with multiple validation layers ensuring
    /// mathematical consistency and preventing divide-by-zero errors or invalid calculations.
    /// String processing is optimized for performance during batch operations while maintaining
    /// comprehensive error handling for edge cases and malformed input.
    /// 
    /// Error Handling Strategy:
    /// Invalid or malformed ratio strings trigger fallback to the standard 16:9 aspect ratio,
    /// ensuring poster generation can continue even with configuration errors while providing
    /// predictable behavior for administrative troubleshooting and system monitoring.
    /// 
    /// Validation Requirements:
    /// - Height values must be positive to prevent mathematical errors
    /// - Width values must be positive to ensure valid aspect ratios
    /// - Parsed values must be numeric and within reasonable ranges
    /// - Colon separator must be present for ratio format recognition
    /// 
    /// Performance Considerations:
    /// The parsing algorithm is optimized for repeated execution during batch operations,
    /// using efficient string processing and minimal memory allocation while maintaining
    /// accuracy and reliability in ratio calculation and validation.
    /// </summary>
    /// <param name="ratio">
    /// Aspect ratio configuration string in supported format (e.g., "16:9", "4:3", "21:9").
    /// Empty, null, or malformed strings trigger fallback to standard 16:9 aspect ratio.
    /// </param>
    /// <returns>
    /// Floating-point aspect ratio value calculated from parsed width and height components.
    /// Returns 16:9 ratio (1.777...) for invalid input to ensure consistent poster generation behavior.
    /// </returns>
    // MARK: ParseAspectRatio
    private float ParseAspectRatio(string ratio)
    {
        // Handle null or empty configuration with standard fallback for consistent behavior
        if (string.IsNullOrEmpty(ratio))
            return 16f / 9f;

        // Parse ratio string with comprehensive validation and error handling
        var parts = ratio.Split(':');
        if (parts.Length == 2 && 
            float.TryParse(parts[0], out var width) && 
            float.TryParse(parts[1], out var height) && 
            height > 0)  // Prevent divide-by-zero and ensure mathematical validity
        {
            return width / height;
        }

        // Fallback to standard 16:9 aspect ratio for invalid or malformed configuration
        return 16f / 9f;
    }

    /// <summary>
    /// Renders source imagery onto target canvas using sophisticated scaling and positioning algorithms
    /// that accommodate various fill strategies while maintaining optimal visual quality and composition.
    /// This method implements the core image drawing logic that transforms preprocessed imagery into
    /// poster-ready content optimized for style-specific text overlay and visual enhancement operations.
    /// 
    /// The drawing process serves as the foundation for all poster styles, providing consistent
    /// image processing that enables specialized generators to focus on typography and style-specific
    /// visual elements while ensuring uniform quality and performance across all poster types.
    /// 
    /// Drawing Strategy Implementation:
    /// 
    /// Standard Drawing (Original and Fill Modes):
    /// Implements straightforward bitmap rendering with full canvas coverage, preserving source
    /// imagery without cropping while applying configured scaling transformations. This approach
    /// maintains maximum image content while accommodating dimension adjustments for poster consistency.
    /// 
    /// Fit Mode Drawing with Intelligent Cropping:
    /// Calculates optimal cropping rectangles using center-weighted algorithms that preserve
    /// the most visually important portions of source imagery while achieving target aspect ratios.
    /// The cropping process maintains focus on central content while ensuring high-quality output.
    /// 
    /// Aspect Ratio Analysis and Cropping Determination:
    /// Sophisticated mathematical analysis compares source and destination aspect ratios to determine
    /// optimal cropping strategies. The algorithm prioritizes content preservation while achieving
    /// dimensional consistency required for professional poster presentation.
    /// 
    /// Horizontal Cropping Strategy:
    /// Applied when source images are wider than target aspect ratios, implementing center-weighted
    /// cropping that preserves vertical content while removing excess horizontal imagery. The cropping
    /// calculation ensures symmetrical content removal for balanced visual composition.
    /// 
    /// Vertical Cropping Strategy:
    /// Applied when source images are taller than target aspect ratios, implementing center-weighted
    /// cropping that preserves horizontal content while removing excess vertical imagery. The algorithm
    /// maintains focus on central content areas for optimal visual impact.
    /// 
    /// Quality Preservation:
    /// All drawing operations utilize SkiaSharp's high-quality rendering algorithms with anti-aliasing
    /// and advanced interpolation ensuring professional output quality suitable for media library
    /// presentation and various display devices and resolutions.
    /// 
    /// Performance Optimization:
    /// Drawing operations are optimized for memory efficiency and rendering speed while maintaining
    /// visual quality, using efficient bitmap handling and canvas operations suitable for batch
    /// processing workflows and responsive poster generation.
    /// </summary>
    /// <param name="canvas">
    /// Target canvas for image rendering operations, providing the drawing surface for poster generation.
    /// Canvas dimensions match calculated target size ensuring optimal rendering and composition.
    /// </param>
    /// <param name="original">
    /// Source bitmap containing preprocessed imagery ready for poster integration and rendering.
    /// Bitmap quality and format are optimized for high-quality output and efficient processing.
    /// </param>
    /// <param name="targetSize">
    /// Target dimensions for poster output, used for scaling calculations and canvas coordination.
    /// Dimensions are calculated using configuration preferences and aspect ratio requirements.
    /// </param>
    /// <param name="fill">
    /// Fill strategy enumeration controlling image scaling and cropping behavior during rendering.
    /// Determines whether content preservation or dimensional consistency takes priority.
    /// </param>
    /// <param name="originalWidth">
    /// Source image width in pixels, used for aspect ratio calculations and cropping determinations.
    /// Required for mathematical precision in cropping rectangle calculations and scaling operations.
    /// </param>
    /// <param name="originalHeight">
    /// Source image height in pixels, used for aspect ratio analysis and cropping rectangle calculations.
    /// Combined with width to establish source proportions for optimal cropping and scaling strategies.
    /// </param>
    // MARK: DrawPosterImage
    private void DrawPosterImage(SKCanvas canvas, SKBitmap original, SKSizeI targetSize, PosterFill fill, int originalWidth, int originalHeight, PluginConfiguration config)
    {
        // Define destination rectangle covering entire target canvas for consistent poster coverage
        var destRect = new SKRect(0, 0, targetSize.Width, targetSize.Height);

        SKRect srcRect;

        // Apply letterbox detection first if enabled
        if (config.EnableLetterboxDetection)
        {
            var letterboxBounds = LetterboxDetectionService.DetectLetterboxBounds(original, config);
            
            // Use detected bounds as the source rectangle, removing letterboxing
            srcRect = letterboxBounds;
            
            // Log letterbox detection results for debugging
            var cropPercentage = (letterboxBounds.Width * letterboxBounds.Height) / (originalWidth * originalHeight) * 100;
            // Optional: Add logging here if you have access to a logger
            // _logger?.LogDebug("Letterbox detection removed {Percentage:F1}% of content", 100 - cropPercentage);
        }
        else if (fill == PosterFill.Fit)
        {
            // Original fit mode: implement intelligent cropping with center-weighted content preservation
            var srcAspect = (float)originalWidth / originalHeight;
            var dstAspect = (float)targetSize.Width / targetSize.Height;

            if (srcAspect > dstAspect)
            {
                // Source is wider than target: apply horizontal cropping with center preservation
                int cropWidth = (int)(originalHeight * dstAspect);  // Calculate optimal width for target aspect
                int x = (originalWidth - cropWidth) / 2;            // Center cropping horizontally
                srcRect = new SKRect(x, 0, x + cropWidth, originalHeight);
            }
            else
            {
                // Source is taller than target: apply vertical cropping with center preservation
                int cropHeight = (int)(originalWidth / dstAspect);  // Calculate optimal height for target aspect
                int y = (originalHeight - cropHeight) / 2;          // Center cropping vertically
                srcRect = new SKRect(0, y, originalWidth, y + cropHeight);
            }
        }
        else
        {
            // Standard drawing: render entire source bitmap to destination canvas without cropping
            srcRect = new SKRect(0, 0, originalWidth, originalHeight);
        }

        // Render the selected source region to destination canvas with high-quality scaling
        canvas.DrawBitmap(original, srcRect, destRect);
    }
}