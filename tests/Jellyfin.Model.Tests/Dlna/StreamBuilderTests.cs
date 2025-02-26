using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Extensions.Json;
using MediaBrowser.Model.Dlna;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Model.Tests
{
    public class StreamBuilderTests
    {
        [Theory]
        // Chrome
        [InlineData("Chrome", "mp4-h264-aac-vtt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("Chrome", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")] // #6450
        [InlineData("Chrome", "mp4-h264-ac3-aacDef-srt-2600k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")] // #6450
        [InlineData("Chrome", "mp4-h264-ac3-aacExt-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioIsExternal)] // #6450
        [InlineData("Chrome", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("Chrome", "mp4-hevc-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported, "Transcode")]
        [InlineData("Chrome", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported | TranscodeReason.SecondaryAudioNotSupported, "Transcode")]
        [InlineData("Chrome", "mkv-vp9-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.ContainerNotSupported, "Remux")] // #6450
        [InlineData("Chrome", "mkv-vp9-ac3-srt-2600k", PlayMethod.DirectStream, TranscodeReason.ContainerNotSupported | TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("Chrome", "mkv-vp9-vorbis-vtt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        // Firefox
        [InlineData("Firefox", "mp4-h264-aac-vtt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("Firefox", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")] // #6450
        [InlineData("Firefox", "mp4-h264-ac3-aacDef-srt-2600k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")] // #6450
        [InlineData("Firefox", "mp4-h264-ac3-aacExt-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioIsExternal)] // #6450
        [InlineData("Firefox", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("Firefox", "mp4-hevc-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported, "Transcode")]
        [InlineData("Firefox", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported | TranscodeReason.SecondaryAudioNotSupported, "Transcode")]
        [InlineData("Firefox", "mkv-vp9-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.ContainerNotSupported, "Remux")] // #6450
        [InlineData("Firefox", "mkv-vp9-ac3-srt-2600k", PlayMethod.DirectStream, TranscodeReason.ContainerNotSupported | TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("Firefox", "mkv-vp9-vorbis-vtt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        // Safari
        [InlineData("SafariNext", "mp4-h264-aac-vtt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("SafariNext", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("SafariNext", "mp4-h264-ac3-aacDef-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("SafariNext", "mp4-h264-ac3-aacExt-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("SafariNext", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("SafariNext", "mp4-hevc-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported, "Remux", "HLS.mp4")] // #6450
        [InlineData("SafariNext", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported, "Remux", "HLS.mp4")] // #6450
        [InlineData("SafariNext", "mp4-hevc-ac3-aacExt-srt-15200k", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported, "Remux", "HLS.mp4")] // #6450
        // AndroidPixel
        [InlineData("AndroidPixel", "mp4-h264-aac-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("AndroidPixel", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("AndroidPixel", "mp4-h264-ac3-aacDef-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("AndroidPixel", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("AndroidPixel", "mp4-hevc-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.ContainerBitrateExceedsLimit, "Transcode")]
        [InlineData("AndroidPixel", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.ContainerBitrateExceedsLimit, "Transcode")]
        // Yatse
        [InlineData("Yatse", "mp4-h264-aac-srt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        [InlineData("Yatse", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")] // #6450
        [InlineData("Yatse", "mp4-h264-ac3-aacDef-srt-2600k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")] // #6450
        [InlineData("Yatse", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)]
        [InlineData("Yatse", "mp4-hevc-aac-srt-15200k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        [InlineData("Yatse", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")] // #6450
        // RokuSSPlus
        [InlineData("RokuSSPlus", "mp4-h264-aac-srt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        [InlineData("RokuSSPlus", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450 should be DirectPlay
        [InlineData("RokuSSPlus", "mp4-h264-ac3-aacDef-srt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        [InlineData("RokuSSPlus", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("RokuSSPlus", "mp4-hevc-aac-srt-15200k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        [InlineData("RokuSSPlus", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        [InlineData("RokuSSPlus", "mp4-hevc-ac3-srt-15200k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450
        // JellyfinMediaPlayer
        [InlineData("JellyfinMediaPlayer", "mp4-h264-aac-vtt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        [InlineData("JellyfinMediaPlayer", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        [InlineData("JellyfinMediaPlayer", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        [InlineData("JellyfinMediaPlayer", "mp4-hevc-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.ContainerBitrateExceedsLimit, "Transcode")]
        [InlineData("JellyfinMediaPlayer", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.ContainerBitrateExceedsLimit, "Transcode")]
        [InlineData("JellyfinMediaPlayer", "mkv-vp9-aac-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("JellyfinMediaPlayer", "mkv-vp9-ac3-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("JellyfinMediaPlayer", "mkv-vp9-vorbis-vtt-2600k", PlayMethod.DirectPlay)] // #6450
        // Chrome-NoHLS
        [InlineData("Chrome-NoHLS", "mp4-h264-aac-vtt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("Chrome-NoHLS", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")] // #6450
        [InlineData("Chrome-NoHLS", "mp4-h264-ac3-aacDef-srt-2600k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")] // #6450
        [InlineData("Chrome-NoHLS", "mp4-h264-ac3-aacExt-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioIsExternal)] // #6450
        [InlineData("Chrome-NoHLS", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("Chrome-NoHLS", "mp4-hevc-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported, "Transcode", "http")]
        [InlineData("Chrome-NoHLS", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported | TranscodeReason.SecondaryAudioNotSupported, "Transcode", "http")]
        [InlineData("Chrome-NoHLS", "mkv-vp9-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.ContainerNotSupported, "Remux")] // #6450
        [InlineData("Chrome-NoHLS", "mkv-vp9-ac3-srt-2600k", PlayMethod.DirectStream, TranscodeReason.ContainerNotSupported | TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("Chrome-NoHLS", "mkv-vp9-vorbis-vtt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        // TranscodeMedia
        [InlineData("TranscodeMedia", "mp4-h264-aac-vtt-2600k", PlayMethod.Transcode, TranscodeReason.DirectPlayError, "Remux", "HLS.mp4")]
        [InlineData("TranscodeMedia", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.Transcode, TranscodeReason.DirectPlayError, "Remux", "HLS.mp4")]
        [InlineData("TranscodeMedia", "mp4-h264-ac3-srt-2600k", PlayMethod.Transcode, TranscodeReason.DirectPlayError, "DirectStream", "HLS.mp4")]
        [InlineData("TranscodeMedia", "mp4-hevc-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.DirectPlayError, "Remux", "HLS.mp4")]
        [InlineData("TranscodeMedia", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.DirectPlayError, "Remux", "HLS.mp4")]
        [InlineData("TranscodeMedia", "mkv-av1-aac-srt-2600k", PlayMethod.Transcode, TranscodeReason.DirectPlayError, "DirectStream", "http")]
        [InlineData("TranscodeMedia", "mkv-av1-vorbis-srt-2600k", PlayMethod.Transcode, TranscodeReason.DirectPlayError, "Remux", "http")]
        [InlineData("TranscodeMedia", "mkv-vp9-aac-srt-2600k", PlayMethod.Transcode, TranscodeReason.DirectPlayError, "DirectStream", "http")]
        [InlineData("TranscodeMedia", "mkv-vp9-ac3-srt-2600k", PlayMethod.Transcode, TranscodeReason.DirectPlayError, "DirectStream", "http")]
        [InlineData("TranscodeMedia", "mkv-vp9-vorbis-vtt-2600k", PlayMethod.Transcode, TranscodeReason.DirectPlayError, "Remux", "http")]
        // DirectMedia
        [InlineData("DirectMedia", "mp4-h264-aac-vtt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")]
        [InlineData("DirectMedia", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")]
        [InlineData("DirectMedia", "mp4-h264-ac3-aacDef-srt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")]
        [InlineData("DirectMedia", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")]
        [InlineData("DirectMedia", "mp4-hevc-aac-srt-15200k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")]
        [InlineData("DirectMedia", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")]
        [InlineData("DirectMedia", "mkv-vp9-aac-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("DirectMedia", "mkv-vp9-ac3-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("DirectMedia", "mkv-vp9-vorbis-vtt-2600k", PlayMethod.DirectPlay)]
        // LowBandwidth
        [InlineData("LowBandwidth", "mp4-h264-aac-vtt-2600k", PlayMethod.Transcode, TranscodeReason.ContainerBitrateExceedsLimit, "Transcode")]
        [InlineData("LowBandwidth", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.Transcode, TranscodeReason.ContainerBitrateExceedsLimit, "Transcode")]
        [InlineData("LowBandwidth", "mp4-h264-ac3-srt-2600k", PlayMethod.Transcode, TranscodeReason.ContainerBitrateExceedsLimit, "Transcode")]
        [InlineData("LowBandwidth", "mp4-hevc-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.ContainerBitrateExceedsLimit, "Transcode")]
        [InlineData("LowBandwidth", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.ContainerBitrateExceedsLimit, "Transcode")]
        [InlineData("LowBandwidth", "mkv-vp9-aac-srt-2600k", PlayMethod.Transcode, TranscodeReason.ContainerBitrateExceedsLimit, "Transcode")]
        [InlineData("LowBandwidth", "mkv-vp9-ac3-srt-2600k", PlayMethod.Transcode, TranscodeReason.ContainerBitrateExceedsLimit, "Transcode")]
        [InlineData("LowBandwidth", "mkv-vp9-vorbis-vtt-2600k", PlayMethod.Transcode, TranscodeReason.ContainerBitrateExceedsLimit, "Transcode")]
        // Null
        [InlineData("Null", "mp4-h264-aac-vtt-2600k", null, TranscodeReason.ContainerBitrateExceedsLimit)]
        [InlineData("Null", "mp4-h264-ac3-aac-srt-2600k", null, TranscodeReason.ContainerBitrateExceedsLimit)]
        [InlineData("Null", "mp4-h264-ac3-srt-2600k", null, TranscodeReason.ContainerBitrateExceedsLimit)]
        [InlineData("Null", "mp4-hevc-aac-srt-15200k", null, TranscodeReason.ContainerBitrateExceedsLimit)]
        [InlineData("Null", "mp4-hevc-ac3-aac-srt-15200k", null, TranscodeReason.ContainerBitrateExceedsLimit)]
        [InlineData("Null", "mkv-vp9-aac-srt-2600k", null, TranscodeReason.ContainerBitrateExceedsLimit)]
        [InlineData("Null", "mkv-vp9-ac3-srt-2600k", null, TranscodeReason.ContainerBitrateExceedsLimit)]
        [InlineData("Null", "mkv-vp9-vorbis-vtt-2600k", null, TranscodeReason.ContainerBitrateExceedsLimit)]
        // AndroidTV
        [InlineData("AndroidTVExoPlayer", "mp4-h264-aac-vtt-2600k", PlayMethod.DirectPlay)]
        [InlineData("AndroidTVExoPlayer", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("AndroidTVExoPlayer", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("AndroidTVExoPlayer", "mp4-hevc-aac-srt-15200k", PlayMethod.DirectPlay)]
        [InlineData("AndroidTVExoPlayer", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.DirectPlay)]
        [InlineData("AndroidTVExoPlayer", "mkv-vp9-aac-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("AndroidTVExoPlayer", "mkv-vp9-ac3-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("AndroidTVExoPlayer", "mkv-vp9-vorbis-vtt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)]
        // Tizen 3 Stereo
        [InlineData("Tizen3-stereo", "mp4-h264-aac-vtt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen3-stereo", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen3-stereo", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen3-stereo", "mp4-h264-dts-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen3-stereo", "mp4-hevc-aac-srt-15200k", PlayMethod.DirectPlay)]
        [InlineData("Tizen3-stereo", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.DirectPlay)]
        [InlineData("Tizen3-stereo", "mp4-hevc-truehd-srt-15200k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)]
        [InlineData("Tizen3-stereo", "mkv-vp9-aac-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen3-stereo", "mkv-vp9-ac3-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen3-stereo", "mkv-vp9-vorbis-vtt-2600k", PlayMethod.DirectPlay)]
        // Tizen 4 4K 5.1
        [InlineData("Tizen4-4K-5.1", "mp4-h264-aac-vtt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen4-4K-5.1", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen4-4K-5.1", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen4-4K-5.1", "mp4-h264-dts-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)]
        [InlineData("Tizen4-4K-5.1", "mp4-hevc-aac-srt-15200k", PlayMethod.DirectPlay)]
        [InlineData("Tizen4-4K-5.1", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.DirectPlay)]
        [InlineData("Tizen4-4K-5.1", "mp4-hevc-truehd-srt-15200k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)]
        [InlineData("Tizen4-4K-5.1", "mkv-vp9-aac-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen4-4K-5.1", "mkv-vp9-ac3-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen4-4K-5.1", "mkv-vp9-vorbis-vtt-2600k", PlayMethod.DirectPlay)]
        public async Task BuildVideoItemSimple(string deviceName, string mediaSource, PlayMethod? playMethod, TranscodeReason why = default, string transcodeMode = "DirectStream", string transcodeProtocol = "")
        {
            var options = await GetMediaOptions(deviceName, mediaSource);
            BuildVideoItemSimpleTest(options, playMethod, why, transcodeMode, transcodeProtocol);
        }

        [Theory]
        // Chrome
        [InlineData("Chrome", "mp4-h264-aac-vtt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("Chrome", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("Chrome", "mp4-h264-ac3-aacDef-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450 <BUG: this is direct played>
        [InlineData("Chrome", "mp4-h264-ac3-aacExt-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("Chrome", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("Chrome", "mp4-hevc-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported, "Transcode")]
        [InlineData("Chrome", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported | TranscodeReason.AudioCodecNotSupported, "Transcode")]
        [InlineData("Chrome", "mkv-vp9-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.ContainerNotSupported, "Remux")] // #6450
        [InlineData("Chrome", "mkv-vp9-ac3-srt-2600k", PlayMethod.DirectStream, TranscodeReason.ContainerNotSupported | TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("Chrome", "mkv-vp9-vorbis-vtt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        // Firefox
        [InlineData("Firefox", "mp4-h264-aac-vtt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("Firefox", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("Firefox", "mp4-h264-ac3-aacDef-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("Firefox", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("Firefox", "mp4-hevc-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported, "Transcode")]
        [InlineData("Firefox", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported | TranscodeReason.AudioCodecNotSupported, "Transcode")]
        [InlineData("Firefox", "mkv-vp9-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.ContainerNotSupported, "Remux")] // #6450
        [InlineData("Firefox", "mkv-vp9-ac3-srt-2600k", PlayMethod.DirectStream, TranscodeReason.ContainerNotSupported | TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("Firefox", "mkv-vp9-vorbis-vtt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        // Safari
        [InlineData("SafariNext", "mp4-h264-aac-vtt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("SafariNext", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("SafariNext", "mp4-h264-ac3-aacDef-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("SafariNext", "mp4-h264-ac3-aacExt-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("SafariNext", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("SafariNext", "mp4-hevc-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported, "Remux", "HLS.mp4")] // #6450
        [InlineData("SafariNext", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported, "Remux", "HLS.mp4")] // #6450
        [InlineData("SafariNext", "mp4-hevc-ac3-aacExt-srt-15200k", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported, "Remux", "HLS.mp4")] // #6450
        // AndroidPixel
        [InlineData("AndroidPixel", "mp4-h264-aac-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("AndroidPixel", "mp4-h264-ac3-aacDef-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("AndroidPixel", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("AndroidPixel", "mp4-hevc-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.ContainerBitrateExceedsLimit, "Transcode")]
        [InlineData("AndroidPixel", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.ContainerBitrateExceedsLimit, "Transcode")]
        // Yatse
        [InlineData("Yatse", "mp4-h264-aac-srt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        [InlineData("Yatse", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("Yatse", "mp4-h264-ac3-aacDef-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("Yatse", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)]
        [InlineData("Yatse", "mp4-hevc-aac-srt-15200k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        [InlineData("Yatse", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450
        // RokuSSPlus
        [InlineData("RokuSSPlus", "mp4-h264-aac-srt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        [InlineData("RokuSSPlus", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450 should be DirectPlay
        [InlineData("RokuSSPlus", "mp4-h264-ac3-aacDef-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("RokuSSPlus", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("RokuSSPlus", "mp4-hevc-aac-srt-15200k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        [InlineData("RokuSSPlus", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450
        [InlineData("RokuSSPlus", "mp4-hevc-ac3-srt-15200k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)] // #6450
        // JellyfinMediaPlayer
        [InlineData("JellyfinMediaPlayer", "mp4-h264-aac-vtt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        [InlineData("JellyfinMediaPlayer", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        [InlineData("JellyfinMediaPlayer", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        [InlineData("JellyfinMediaPlayer", "mp4-hevc-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.ContainerBitrateExceedsLimit, "Transcode")] // #6450
        [InlineData("JellyfinMediaPlayer", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.ContainerBitrateExceedsLimit, "Transcode")] // #6450
        [InlineData("JellyfinMediaPlayer", "mkv-vp9-aac-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("JellyfinMediaPlayer", "mkv-vp9-ac3-srt-2600k", PlayMethod.DirectPlay)] // #6450
        [InlineData("JellyfinMediaPlayer", "mkv-vp9-vorbis-vtt-2600k", PlayMethod.DirectPlay)] // #6450
        // AndroidTV
        [InlineData("AndroidTVExoPlayer", "mp4-h264-aac-vtt-2600k", PlayMethod.DirectPlay)]
        [InlineData("AndroidTVExoPlayer", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("AndroidTVExoPlayer", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("AndroidTVExoPlayer", "mp4-hevc-aac-srt-15200k", PlayMethod.DirectPlay)]
        [InlineData("AndroidTVExoPlayer", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.DirectPlay)]
        [InlineData("AndroidTVExoPlayer", "mkv-vp9-aac-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("AndroidTVExoPlayer", "mkv-vp9-ac3-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("AndroidTVExoPlayer", "mkv-vp9-vorbis-vtt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)]
        // Tizen 3 Stereo
        [InlineData("Tizen3-stereo", "mp4-h264-aac-vtt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen3-stereo", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen3-stereo", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen3-stereo", "mp4-h264-dts-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen3-stereo", "mp4-hevc-aac-srt-15200k", PlayMethod.DirectPlay)]
        [InlineData("Tizen3-stereo", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.DirectPlay)]
        [InlineData("Tizen3-stereo", "mp4-hevc-truehd-srt-15200k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)]
        [InlineData("Tizen3-stereo", "mkv-vp9-aac-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen3-stereo", "mkv-vp9-ac3-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen3-stereo", "mkv-vp9-vorbis-vtt-2600k", PlayMethod.DirectPlay)]
        // Tizen 4 4K 5.1
        [InlineData("Tizen4-4K-5.1", "mp4-h264-aac-vtt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen4-4K-5.1", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen4-4K-5.1", "mp4-h264-ac3-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen4-4K-5.1", "mp4-h264-dts-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)]
        [InlineData("Tizen4-4K-5.1", "mp4-hevc-aac-srt-15200k", PlayMethod.DirectPlay)]
        [InlineData("Tizen4-4K-5.1", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.DirectPlay)]
        [InlineData("Tizen4-4K-5.1", "mp4-hevc-truehd-srt-15200k", PlayMethod.DirectStream, TranscodeReason.AudioCodecNotSupported)]
        [InlineData("Tizen4-4K-5.1", "mkv-vp9-aac-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen4-4K-5.1", "mkv-vp9-ac3-srt-2600k", PlayMethod.DirectPlay)]
        [InlineData("Tizen4-4K-5.1", "mkv-vp9-vorbis-vtt-2600k", PlayMethod.DirectPlay)]
        public async Task BuildVideoItemWithFirstExplicitStream(string deviceName, string mediaSource, PlayMethod? playMethod, TranscodeReason why = default, string transcodeMode = "DirectStream", string transcodeProtocol = "")
        {
            var options = await GetMediaOptions(deviceName, mediaSource);
            options.AudioStreamIndex = 1;
            options.SubtitleStreamIndex = options.MediaSources[0].MediaStreams.Count - 1;

            var streamInfo = BuildVideoItemSimpleTest(options, playMethod, why, transcodeMode, transcodeProtocol);
            Assert.Equal(streamInfo?.AudioStreamIndex, options.AudioStreamIndex);
            Assert.Equal(streamInfo?.SubtitleStreamIndex, options.SubtitleStreamIndex);
        }

        [Theory]
        // Chrome
        [InlineData("Chrome", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")] // #6450
        [InlineData("Chrome", "mp4-h264-ac3-aac-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")]
        [InlineData("Chrome", "mp4-h264-ac3-aacExt-srt-2600k", PlayMethod.DirectStream, TranscodeReason.AudioIsExternal)] // #6450
        [InlineData("Chrome", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported | TranscodeReason.SecondaryAudioNotSupported, "Transcode")]
        // Firefox
        [InlineData("Firefox", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")] // #6450
        [InlineData("Firefox", "mp4-h264-ac3-aac-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")]
        [InlineData("Firefox", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported | TranscodeReason.SecondaryAudioNotSupported, "Transcode")]
        // Yatse
        [InlineData("Yatse", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")] // #6450
        [InlineData("Yatse", "mp4-h264-ac3-aac-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")]
        [InlineData("Yatse", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")] // #6450
        // RokuSSPlus
        [InlineData("RokuSSPlus", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        [InlineData("RokuSSPlus", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")] // #6450
        // no streams
        [InlineData("Chrome", "no-streams", PlayMethod.Transcode, TranscodeReason.VideoCodecNotSupported, "Transcode")] // #6450
        // AndroidTV
        [InlineData("AndroidTVExoPlayer", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")]
        [InlineData("AndroidTVExoPlayer", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.DirectPlay, (TranscodeReason)0, "Remux")]
        // Tizen 3 Stereo
        [InlineData("Tizen3-stereo", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")]
        [InlineData("Tizen3-stereo", "mp4-h264-ac3-aac-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")]
        [InlineData("Tizen3-stereo", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")]
        // Tizen 4 4K 5.1
        [InlineData("Tizen4-4K-5.1", "mp4-h264-ac3-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")]
        [InlineData("Tizen4-4K-5.1", "mp4-h264-ac3-aac-aac-srt-2600k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")]
        [InlineData("Tizen4-4K-5.1", "mp4-hevc-ac3-aac-srt-15200k", PlayMethod.DirectStream, TranscodeReason.SecondaryAudioNotSupported, "Remux")]
        public async Task BuildVideoItemWithDirectPlayExplicitStreams(string deviceName, string mediaSource, PlayMethod? playMethod, TranscodeReason why = default, string transcodeMode = "DirectStream", string transcodeProtocol = "")
        {
            var options = await GetMediaOptions(deviceName, mediaSource);
            var streamCount = options.MediaSources[0].MediaStreams.Count;
            if (streamCount > 0)
            {
                options.AudioStreamIndex = streamCount - 2;
                options.SubtitleStreamIndex = streamCount - 1;
            }

            var streamInfo = BuildVideoItemSimpleTest(options, playMethod, why, transcodeMode, transcodeProtocol);
            Assert.Equal(streamInfo?.AudioStreamIndex, options.AudioStreamIndex);
            Assert.Equal(streamInfo?.SubtitleStreamIndex, options.SubtitleStreamIndex);
        }

        private StreamInfo? BuildVideoItemSimpleTest(MediaOptions options, PlayMethod? playMethod, TranscodeReason why, string transcodeMode, string transcodeProtocol)
        {
            if (string.IsNullOrEmpty(transcodeProtocol))
            {
                transcodeProtocol = playMethod == PlayMethod.DirectStream ? "http" : "HLS.ts";
            }

            var builder = GetStreamBuilder();

            var streamInfo = builder.GetOptimalVideoStream(options);
            Assert.NotNull(streamInfo);

            if (playMethod is not null)
            {
                Assert.Equal(playMethod, streamInfo.PlayMethod);
            }

            Assert.Equal(why, streamInfo.TranscodeReasons);

            var audioStreamIndexInput = options.AudioStreamIndex;
            var targetVideoStream = streamInfo.TargetVideoStream;
            var targetAudioStream = streamInfo.TargetAudioStream;

            var mediaSource = options.MediaSources.First(source => source.Id == streamInfo.MediaSourceId);
            Assert.NotNull(mediaSource);
            var videoStreams = mediaSource.MediaStreams.Where(stream => stream.Type == MediaStreamType.Video);
            var audioStreams = mediaSource.MediaStreams.Where(stream => stream.Type == MediaStreamType.Audio);
            // TODO: Check AudioStreamIndex vs options.AudioStreamIndex
            var inputAudioStream = mediaSource.GetDefaultAudioStream(audioStreamIndexInput ?? mediaSource.DefaultAudioStreamIndex);

            var uri = ParseUri(streamInfo);

            if (playMethod == PlayMethod.DirectPlay)
            {
                // Check expected container
                var containers = ContainerProfile.SplitValue(mediaSource.Container);
                // TODO: Test transcode too
                // Assert.Contains(uri.Extension, containers);

                // Check expected video codec (1)
                Assert.Contains(targetVideoStream?.Codec, streamInfo.TargetVideoCodec);
                Assert.Single(streamInfo.TargetVideoCodec);

                // Check expected audio codecs (1)
                Assert.Contains(targetAudioStream?.Codec, streamInfo.TargetAudioCodec);
                Assert.Single(streamInfo.TargetAudioCodec);
                // Assert.Single(val.AudioCodecs);

                if (transcodeMode.Equals("DirectStream", StringComparison.Ordinal))
                {
                    Assert.Equal(streamInfo.Container, uri.Extension);
                }
            }
            else if (playMethod == PlayMethod.DirectStream || playMethod == PlayMethod.Transcode)
            {
                Assert.NotNull(streamInfo.Container);
                Assert.NotEmpty(streamInfo.VideoCodecs);
                Assert.NotEmpty(streamInfo.AudioCodecs);

                // Check expected container (todo: this could be a test param)
                if (transcodeProtocol.Equals("http", StringComparison.Ordinal))
                {
                    // Assert.Equal("webm", val.Container);
                    Assert.Equal(streamInfo.Container, uri.Extension);
                    Assert.Equal("stream", uri.Filename);
                    Assert.Equal("http", streamInfo.SubProtocol);
                }
                else if (transcodeProtocol.Equals("HLS.mp4", StringComparison.Ordinal))
                {
                    Assert.Equal("mp4", streamInfo.Container);
                    Assert.Equal("m3u8", uri.Extension);
                    Assert.Equal("master", uri.Filename);
                    Assert.Equal("hls", streamInfo.SubProtocol);
                }
                else
                {
                    Assert.Equal("ts", streamInfo.Container);
                    Assert.Equal("m3u8", uri.Extension);
                    Assert.Equal("master", uri.Filename);
                    Assert.Equal("hls", streamInfo.SubProtocol);
                }

                // Full transcode
                if (transcodeMode.Equals("Transcode", StringComparison.Ordinal))
                {
                    if ((streamInfo.TranscodeReasons & (StreamBuilder.ContainerReasons | TranscodeReason.DirectPlayError)) == 0)
                    {
                        Assert.All(
                            videoStreams,
                            stream => Assert.DoesNotContain(stream.Codec, streamInfo.VideoCodecs));
                    }

                    // TODO: fill out tests here
                }

                // DirectStream and Remux
                else
                {
                    // Check expected video codec (1)
                    Assert.Contains(targetVideoStream?.Codec, streamInfo.TargetVideoCodec);
                    Assert.Single(streamInfo.TargetVideoCodec);

                    if (transcodeMode.Equals("DirectStream", StringComparison.Ordinal))
                    {
                        // Check expected audio codecs (1)
                        if (targetAudioStream?.IsExternal == false)
                        {
                            // Check expected audio codecs (1)
                            Assert.DoesNotContain(targetAudioStream.Codec, streamInfo.AudioCodecs);
                        }
                    }
                    else if (transcodeMode.Equals("Remux", StringComparison.Ordinal))
                    {
                        // Check expected audio codecs (1)
                        Assert.Contains(targetAudioStream?.Codec, streamInfo.AudioCodecs);
                        Assert.Single(streamInfo.AudioCodecs);
                    }

                    // Video details
                    var videoStream = targetVideoStream;
                    Assert.False(streamInfo.EstimateContentLength);
                    Assert.Equal(TranscodeSeekInfo.Auto, streamInfo.TranscodeSeekInfo);
                    Assert.Contains(videoStream?.Profile?.ToLowerInvariant() ?? string.Empty, streamInfo.TargetVideoProfile?.Split(",").Select(s => s.ToLowerInvariant()) ?? Array.Empty<string>());
                    Assert.Equal(videoStream?.Level, streamInfo.TargetVideoLevel);
                    Assert.Equal(videoStream?.BitDepth, streamInfo.TargetVideoBitDepth);
                    Assert.InRange(streamInfo.VideoBitrate.GetValueOrDefault(), videoStream?.BitRate.GetValueOrDefault() ?? 0, int.MaxValue);

                    // Audio codec not supported
                    if ((why & TranscodeReason.AudioCodecNotSupported) != 0)
                    {
                        // Audio stream specified
                        if (options.AudioStreamIndex >= 0)
                        {
                            // TODO:fixme
                            if (targetAudioStream?.IsExternal == false)
                            {
                                Assert.DoesNotContain(targetAudioStream.Codec, streamInfo.AudioCodecs);
                            }
                        }

                        // Audio stream not specified
                        else
                        {
                            // TODO: Fixme
                            Assert.All(audioStreams, stream =>
                            {
                                if (!stream.IsExternal)
                                {
                                    Assert.DoesNotContain(stream.Codec, streamInfo.AudioCodecs);
                                }
                            });
                        }
                    }
                }
            }
            else if (playMethod is null)
            {
                Assert.Null(streamInfo.SubProtocol);
                Assert.Equal("stream", uri.Filename);

                Assert.False(streamInfo.EstimateContentLength);
                Assert.Equal(TranscodeSeekInfo.Auto, streamInfo.TranscodeSeekInfo);
            }

            return streamInfo;
        }

        private static async ValueTask<T> TestData<T>(string name)
        {
            var path = Path.Join("Test Data", typeof(T).Name + "-" + name + ".json");

            using var stream = File.OpenRead(path);

            var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonDefaults.Options);
            if (value is not null)
            {
                return value;
            }

            throw new SerializationException("Invalid test data: " + name);
        }

        private StreamBuilder GetStreamBuilder()
        {
            var transcodeSupport = new Mock<ITranscoderSupport>();
            var logger = new NullLogger<StreamBuilderTests>();

            return new StreamBuilder(transcodeSupport.Object, logger);
        }

        private static async ValueTask<MediaOptions> GetMediaOptions(string deviceProfile, params string[] sources)
        {
            var mediaSources = sources.Select(src => TestData<MediaSourceInfo>(src))
                .Select(val => val.Result)
                .ToArray();
            var mediaSourceId = mediaSources[0]?.Id;

            var dp = await TestData<DeviceProfile>(deviceProfile);

            return new MediaOptions()
            {
                ItemId = new Guid("11D229B7-2D48-4B95-9F9B-49F6AB75E613"),
                MediaSourceId = mediaSourceId,
                MediaSources = mediaSources,
                DeviceId = "test-deviceId",
                Profile = dp,
                AllowAudioStreamCopy = true,
                AllowVideoStreamCopy = true,
            };
        }

        private static (string Path, NameValueCollection Query, string Filename, string Extension) ParseUri(StreamInfo val)
        {
            var href = val.ToUrl("media:", "ACCESSTOKEN").Split("?", 2);
            var path = href[0];

            var queryString = href.ElementAtOrDefault(1);
            var query = string.IsNullOrEmpty(queryString) ? System.Web.HttpUtility.ParseQueryString(queryString ?? string.Empty) : new NameValueCollection();

            var filename = Path.GetFileNameWithoutExtension(path);
            var extension = Path.GetExtension(path);
            if (extension.Length > 0)
            {
                extension = extension.Substring(1);
            }

            return (path, query, filename, extension);
        }
    }
}
