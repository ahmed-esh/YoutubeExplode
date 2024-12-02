namespace YoutubeExplode.Videos.Streams;

/// <summary>
/// Metadata associated with a media stream that contains audio.
/// </summary>
public interface IAudioStreamInfo : IStreamInfo
{
    /// <summary>
    /// Audio codec.
    /// </summary>
    string AudioCodec { get; }
    
    // New property to expose language information
    string? Language { get; } // Nullable to handle cases where language data is unavailable
}
