using System.Diagnostics.CodeAnalysis;
using YoutubeExplode.Common;

namespace YoutubeExplode.Videos.Streams;

/// <summary>
/// Metadata associated with a muxed (audio + video combined) media stream.
/// </summary>
public class MuxedStreamInfo(
    string url,
    Container container,
    FileSize size,
    Bitrate bitrate,
    string audioCodec,
    string videoCodec,
    VideoQuality videoQuality,
    Resolution videoResolution,
    string? language // Add a new parameter for language
) : IAudioStreamInfo, IVideoStreamInfo
{
    /// <inheritdoc />
    public string Url { get; } = url;

    /// <inheritdoc />
    public Container Container { get; } = container;

    /// <inheritdoc />
    public FileSize Size { get; } = size;

    /// <inheritdoc />
    public Bitrate Bitrate { get; } = bitrate;

    /// <inheritdoc />
    public string AudioCodec { get; } = audioCodec;

    /// <inheritdoc />
    public string VideoCodec { get; } = videoCodec;

    /// <inheritdoc />
    public VideoQuality VideoQuality { get; } = videoQuality;

    /// <inheritdoc />
    public Resolution VideoResolution { get; } = videoResolution;

    /// <summary>
    /// Language information of the audio stream, if available.
    /// </summary>
    public string? Language { get; } = language; // Define and initialize the Language property

    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public override string ToString() =>
        $"Muxed ({VideoQuality} | {Container} | Language: {Language ?? "Unknown"})";
}
