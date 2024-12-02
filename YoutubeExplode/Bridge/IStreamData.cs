namespace YoutubeExplode.Bridge;

internal interface IStreamData
{
    int? Itag { get; }

    string? Url { get; }

    string? Signature { get; }

    string? SignatureParameter { get; }

    long? ContentLength { get; }

    long? Bitrate { get; }

    string? Container { get; }

    string? AudioCodec { get; }

    string? VideoCodec { get; }

    string? VideoQualityLabel { get; }

    int? VideoWidth { get; }

    int? VideoHeight { get; }

    int? VideoFramerate { get; }

    /// <summary>
    /// Metadata about the audio track, including language information.
    /// </summary>
    AudioTrack? AudioTrack { get; } // New property for audio track metadata
}

/// <summary>
/// Represents metadata about an audio track.
/// </summary>
public class AudioTrack
{
    /// <summary>
    /// The display name of the audio track, such as the language (e.g., "English (United States)").
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;
}
