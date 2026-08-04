namespace Regira.Media.Drawing.Services.Abstractions;

/// <summary>
/// Provides an abstraction for image processing services, including parsing, format conversion,
/// cropping, resizing, rotating, flipping, color manipulation, and drawing operations.
/// </summary>
public interface IImageService : IImageParsingService, IImageFormatService, IImageTransformService, IImageColorService, IImageDrawService;