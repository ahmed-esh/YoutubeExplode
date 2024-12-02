using System.Diagnostics.CodeAnalysis;

namespace YoutubeExplode.Videos.Streams;

/// <summary>
/// Metadata associated with an audio-only YouTube media stream.
/// </summary>
public class AudioOnlyStreamInfo(
    string url,
    Container container,
    FileSize size,
    Bitrate bitrate,
    string audioCodec,
    string? language // Add a new parameter for language
) : IAudioStreamInfo
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

    /// <summary>
    /// Language information of the audio stream, if available.
    /// </summary>
    public string? Language { get; } = language; // Define and initialize the Language property

    /// <inheritdoc />
    [ExcludeFromCodeCoverage]
    public override string ToString() => $"Audio-only ({Container}, Language: {Language ?? "Unknown"})";
}
