using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lazy;
using YoutubeExplode.Utils;
using YoutubeExplode.Utils.Extensions;

namespace YoutubeExplode.Bridge;

internal partial class PlayerResponse(JsonElement content)
{
    [Lazy]
    private JsonElement? Playability => content.GetPropertyOrNull("playabilityStatus");

    [Lazy]
    private string? PlayabilityStatus =>
        Playability?.GetPropertyOrNull("status")?.GetStringOrNull();

    [Lazy]
    public string? PlayabilityError => Playability?.GetPropertyOrNull("reason")?.GetStringOrNull();

    [Lazy]
    public bool IsAvailable =>
        !string.Equals(PlayabilityStatus, "error", StringComparison.OrdinalIgnoreCase)
        && Details is not null;

    [Lazy]
    public bool IsPlayable =>
        string.Equals(PlayabilityStatus, "ok", StringComparison.OrdinalIgnoreCase);

    [Lazy]
    private JsonElement? Details => content.GetPropertyOrNull("videoDetails");

    [Lazy]
    public string? Title => Details?.GetPropertyOrNull("title")?.GetStringOrNull();

    [Lazy]
    public string? ChannelId => Details?.GetPropertyOrNull("channelId")?.GetStringOrNull();

    [Lazy]
    public string? Author => Details?.GetPropertyOrNull("author")?.GetStringOrNull();

    [Lazy]
    public DateTimeOffset? UploadDate =>
        content
            .GetPropertyOrNull("microformat")
            ?.GetPropertyOrNull("playerMicroformatRenderer")
            ?.GetPropertyOrNull("uploadDate")
            ?.GetDateTimeOffset();

    [Lazy]
    public TimeSpan? Duration =>
        Details
            ?.GetPropertyOrNull("lengthSeconds")
            ?.GetStringOrNull()
            ?.ParseDoubleOrNull()
            ?.Pipe(TimeSpan.FromSeconds);

    [Lazy]
    public IReadOnlyList<ThumbnailData> Thumbnails =>
        Details
            ?.GetPropertyOrNull("thumbnail")
            ?.GetPropertyOrNull("thumbnails")
            ?.EnumerateArrayOrNull()
            ?.Select(j => new ThumbnailData(j))
            .ToArray() ?? [];

    public IReadOnlyList<string> Keywords =>
        Details
            ?.GetPropertyOrNull("keywords")
            ?.EnumerateArrayOrNull()
            ?.Select(j => j.GetStringOrNull())
            .WhereNotNull()
            .ToArray() ?? [];

    [Lazy]
    public string? Description => Details?.GetPropertyOrNull("shortDescription")?.GetStringOrNull();

    [Lazy]
    public long? ViewCount =>
        Details?.GetPropertyOrNull("viewCount")?.GetStringOrNull()?.ParseLongOrNull();

    [Lazy]
    public string? PreviewVideoId =>
        Playability
            ?.GetPropertyOrNull("errorScreen")
            ?.GetPropertyOrNull("playerLegacyDesktopYpcTrailerRenderer")
            ?.GetPropertyOrNull("trailerVideoId")
            ?.GetStringOrNull()
        ?? Playability
            ?.GetPropertyOrNull("errorScreen")
            ?.GetPropertyOrNull("ypcTrailerRenderer")
            ?.GetPropertyOrNull("playerVars")
            ?.GetStringOrNull()
            ?.Pipe(UrlEx.GetQueryParameters)
            .GetValueOrDefault("video_id")
        ?? Playability
            ?.GetPropertyOrNull("errorScreen")
            ?.GetPropertyOrNull("ypcTrailerRenderer")
            ?.GetPropertyOrNull("playerResponse")
            ?.GetStringOrNull()
            ?
            // YouTube uses weird base64-like encoding here that I don't know how to deal with.
            // It's supposed to have JSON inside, but if extracted as is, it contains garbage.
            // Luckily, some of the text gets decoded correctly, which is enough for us to
            // extract the preview video ID using regex.
            .Replace('-', '+')
            .Replace('_', '/')
            .Pipe(Convert.FromBase64String)
            .Pipe(Encoding.UTF8.GetString)
            .Pipe(s => Regex.Match(s, @"video_id=(.{11})").Groups[1].Value)
            .NullIfWhiteSpace();

    [Lazy]
    private JsonElement? StreamingData => content.GetPropertyOrNull("streamingData");

    [Lazy]
    public string? DashManifestUrl =>
        StreamingData?.GetPropertyOrNull("dashManifestUrl")?.GetStringOrNull();

    [Lazy]
    public string? HlsManifestUrl =>
        StreamingData?.GetPropertyOrNull("hlsManifestUrl")?.GetStringOrNull();

    [Lazy]
    public IReadOnlyList<IStreamData> Streams
    {
        get
        {
            var result = new List<IStreamData>();

            var muxedStreams = StreamingData
                ?.GetPropertyOrNull("formats")
                ?.EnumerateArrayOrNull()
                ?.Select(j => new StreamData(j));

            if (muxedStreams is not null)
                result.AddRange(muxedStreams);

            var adaptiveStreams = StreamingData
                ?.GetPropertyOrNull("adaptiveFormats")
                ?.EnumerateArrayOrNull()
                ?.Select(j => new StreamData(j));

            if (adaptiveStreams is not null)
                result.AddRange(adaptiveStreams);

            return result;
        }
    }

    [Lazy]
    public IReadOnlyList<ClosedCaptionTrackData> ClosedCaptionTracks =>
        content
            .GetPropertyOrNull("captions")
            ?.GetPropertyOrNull("playerCaptionsTracklistRenderer")
            ?.GetPropertyOrNull("captionTracks")
            ?.EnumerateArrayOrNull()
            ?.Select(j => new ClosedCaptionTrackData(j))
            .ToArray() ?? [];
}

internal partial class PlayerResponse
{
    public class ClosedCaptionTrackData
    {
        public string Url { get; }
        public string LanguageCode { get; }
        public string LanguageName { get; }
        public bool IsAutoGenerated { get; }

        public ClosedCaptionTrackData(JsonElement content)
        {
            Url = content.GetPropertyOrNull("baseUrl")?.GetStringOrNull() ?? string.Empty;
            LanguageCode = content.GetPropertyOrNull("languageCode")?.GetStringOrNull() ?? string.Empty;
            LanguageName = content.GetPropertyOrNull("name")?.GetPropertyOrNull("simpleText")?.GetStringOrNull()
                ?? content.GetPropertyOrNull("name")?.GetPropertyOrNull("runs")?.EnumerateArrayOrNull()
                    ?.Select(j => j.GetPropertyOrNull("text")?.GetStringOrNull())
                    .WhereNotNull()
                    .ConcatToString() ?? string.Empty;
            IsAutoGenerated = content.GetPropertyOrNull("vssId")?.GetStringOrNull()?.StartsWith("a.") ?? false;
        }
    }

    public static PlayerResponse Parse(string raw) => new(Json.Parse(raw));
}
