using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExplode.Bridge;
using YoutubeExplode.Bridge.Cipher;
using YoutubeExplode.Common;
using YoutubeExplode.Exceptions;
using YoutubeExplode.Utils;
using YoutubeExplode.Utils.Extensions;

namespace YoutubeExplode.Videos.Streams;

/// <summary>
/// Operations related to media streams of YouTube videos.
/// </summary>
public class StreamClient(HttpClient http)
{
    private readonly StreamController _controller = new(http);

    private CipherManifest? _cipherManifest;

    private async ValueTask<CipherManifest> ResolveCipherManifestAsync(
        CancellationToken cancellationToken
    )
    {
        if (_cipherManifest is not null)
            return _cipherManifest;

        var playerSource = await _controller.GetPlayerSourceAsync(cancellationToken);

        return _cipherManifest =
            playerSource.CipherManifest
            ?? throw new YoutubeExplodeException("Failed to extract the cipher manifest.");
    }

    private async ValueTask<long?> TryGetContentLengthAsync(
        IStreamData streamData,
        string url,
        CancellationToken cancellationToken = default
    )
    {
        var contentLength = streamData.ContentLength;

        if (contentLength is null)
        {
            using var response = await http.HeadAsync(url, cancellationToken);
            contentLength = response.Content.Headers.ContentLength;

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
        }

        if (contentLength is not null)
        {
            using var response = await http.GetAsync(
                MediaStream.GetSegmentUrl(url, contentLength.Value - 2, contentLength.Value - 1),
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
        }

        return contentLength;
    }

    private async IAsyncEnumerable<IStreamInfo> GetStreamInfosAsync(
        IEnumerable<IStreamData> streamDatas,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        foreach (var streamData in streamDatas)
        {
            var itag =
                streamData.Itag
                ?? throw new YoutubeExplodeException("Failed to extract the stream itag.");

            var url =
                streamData.Url
                ?? throw new YoutubeExplodeException("Failed to extract the stream URL.");

            if (!string.IsNullOrWhiteSpace(streamData.Signature))
            {
                var cipherManifest = await ResolveCipherManifestAsync(cancellationToken);

                url = UrlEx.SetQueryParameter(
                    url,
                    streamData.SignatureParameter ?? "sig",
                    cipherManifest.Decipher(streamData.Signature)
                );
            }

            var contentLength = await TryGetContentLengthAsync(streamData, url, cancellationToken);
            if (contentLength is null)
                continue;

            var container =
                streamData.Container?.Pipe(s => new Container(s))
                ?? throw new YoutubeExplodeException("Failed to extract the stream container.");

            var bitrate =
                streamData.Bitrate?.Pipe(s => new Bitrate(s))
                ?? throw new YoutubeExplodeException("Failed to extract the stream bitrate.");

            var language = streamData.AudioTrack?.DisplayName;

            if (!string.IsNullOrWhiteSpace(streamData.VideoCodec))
            {
                var framerate = streamData.VideoFramerate ?? 24;

                var videoQuality = !string.IsNullOrWhiteSpace(streamData.VideoQualityLabel)
                    ? VideoQuality.FromLabel(streamData.VideoQualityLabel, framerate)
                    : VideoQuality.FromItag(itag, framerate);

                var videoResolution =
                    streamData.VideoWidth is not null && streamData.VideoHeight is not null
                        ? new Resolution(streamData.VideoWidth.Value, streamData.VideoHeight.Value)
                        : videoQuality.GetDefaultVideoResolution();

                if (!string.IsNullOrWhiteSpace(streamData.AudioCodec))
                {
                    var streamInfo = new MuxedStreamInfo(
                        url,
                        container,
                        new FileSize(contentLength.Value),
                        bitrate,
                        streamData.AudioCodec,
                        streamData.VideoCodec,
                        videoQuality,
                        videoResolution,
                        language
                    );

                    yield return streamInfo;
                }
                else
                {
                    var streamInfo = new VideoOnlyStreamInfo(
                        url,
                        container,
                        new FileSize(contentLength.Value),
                        bitrate,
                        streamData.VideoCodec,
                        videoQuality,
                        videoResolution
                    );

                    yield return streamInfo;
                }
            }
            else if (!string.IsNullOrWhiteSpace(streamData.AudioCodec))
            {
                var streamInfo = new AudioOnlyStreamInfo(
                    url,
                    container,
                    new FileSize(contentLength.Value),
                    bitrate,
                    streamData.AudioCodec,
                    language
                );

                yield return streamInfo;
            }
            else
            {
                throw new YoutubeExplodeException("Failed to extract the stream codec.");
            }
        }
    }

    private async ValueTask<IReadOnlyList<IStreamInfo>> GetStreamInfosAsync(
        VideoId videoId,
        PlayerResponse playerResponse,
        CancellationToken cancellationToken = default
    )
    {
        var streamInfos = new List<IStreamInfo>();

        if (!string.IsNullOrWhiteSpace(playerResponse.PreviewVideoId))
        {
            throw new VideoRequiresPurchaseException(
                $"Video '{videoId}' requires purchase and cannot be played.",
                playerResponse.PreviewVideoId
            );
        }

        if (!playerResponse.IsPlayable)
        {
            throw new VideoUnplayableException(
                $"Video '{videoId}' is unplayable. Reason: '{playerResponse.PlayabilityError}'."
            );
        }

        streamInfos.AddRange(await GetStreamInfosAsync(playerResponse.Streams, cancellationToken));

        if (!string.IsNullOrWhiteSpace(playerResponse.DashManifestUrl))
        {
            try
            {
                var dashManifest = await _controller.GetDashManifestAsync(
                    playerResponse.DashManifestUrl,
                    cancellationToken
                );

                streamInfos.AddRange(
                    await GetStreamInfosAsync(dashManifest.Streams, cancellationToken)
                );
            }
            catch (HttpRequestException) { }
        }

        if (!streamInfos.Any())
        {
            throw new VideoUnplayableException(
                $"Video '{videoId}' does not contain any playable streams."
            );
        }

        return streamInfos;
    }

    public async ValueTask<StreamManifest> GetManifestAsync(
        VideoId videoId,
        CancellationToken cancellationToken = default
    )
    {
        for (var retriesRemaining = 5; ; retriesRemaining--)
        {
            try
            {
                return new StreamManifest(await GetStreamInfosAsync(videoId, cancellationToken));
            }
            catch (Exception ex)
                when (ex is HttpRequestException or IOException && retriesRemaining > 0) { }
        }
    }
}
