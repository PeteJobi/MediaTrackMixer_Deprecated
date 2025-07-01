using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MediaTrackMixer
{
    public class MediaTrackMixer(string ffmpegPath)
    {
        public MediaTrackMixer() : this("ffmpeg.exe")
        {
        }

        public async Task<List<TrackGroup>> GetTracks(string[] inputs)
        {
            var trackGroups = new List<TrackGroup>();
            var inputArgs = string.Join(" ", inputs.Select(inp => $"-i \"{inp}\""));
            var currentInputIndex = -1;
            var currentTrackIndex = -1;
            var currentIsChapter = false;
            await StartProcess(ffmpegPath, inputArgs, null, (sender, args) =>
            {
                if (string.IsNullOrWhiteSpace(args.Data)) return;
                var matchCollection = Regex.Matches(args.Data, @"\s*Input #(\d+).*");
                if (matchCollection.Count != 0)
                {
                    var inputIndex = currentInputIndex = int.Parse(matchCollection[0].Groups[1].Value);
                    trackGroups.Add(new TrackGroup(inputs[inputIndex]));
                    currentTrackIndex = -1;
                }
                else
                {
                    matchCollection = Regex.Matches(args.Data, @"\s*Stream #(\d+):(\d+).*?: (\w+): (\w+).*");
                    if (matchCollection.Count != 0)
                    {
                        var inputIndex = currentInputIndex = int.Parse(matchCollection[0].Groups[1].Value);
                        var trackIndex = currentTrackIndex = int.Parse(matchCollection[0].Groups[2].Value);
                        var trackType = GetTrackType(matchCollection[0].Groups[3].Value);
                        var trackCodec = matchCollection[0].Groups[4].Value;
                        trackGroups[inputIndex].Tracks.Add(new Track(trackIndex, trackType, trackCodec));
                        currentIsChapter = false;
                    }
                    else
                    {
                        matchCollection = Regex.Matches(args.Data, @"\s*Chapter #(\d+):(\d+).+");
                        if (matchCollection.Count != 0)
                        {
                            var inputIndex = currentInputIndex = int.Parse(matchCollection[0].Groups[1].Value);
                            var chapterIndex = currentTrackIndex = int.Parse(matchCollection[0].Groups[2].Value);
                            trackGroups[inputIndex].Chapters.Add(new Chapter(chapterIndex));
                            currentIsChapter = true;
                        }
                    }
                }

                if (currentInputIndex < 0) return;
                matchCollection = Regex.Matches(args.Data, @"\s*Duration:\s(\d{2}:\d{2}:\d{2}\.\d{2}).+");
                if (matchCollection.Count != 0 && TimeSpan.TryParse(matchCollection[0].Groups[1].Value, out var timeSpan))
                {
                    trackGroups[currentInputIndex].Duration = timeSpan;
                }
                else
                {
                    if (currentTrackIndex < 0) return;

                    matchCollection = Regex.Matches(args.Data, @"\s*\btitle\s*:\s*(.+)\s*");
                    if (matchCollection.Count != 0)
                    {
                        var title = matchCollection[0].Groups[1].Value;
                        if (currentIsChapter) trackGroups[currentInputIndex].Chapters[currentTrackIndex].Title = title;
                        else trackGroups[currentInputIndex].Tracks[currentTrackIndex].Title = title;
                    }

                    if((!currentIsChapter && trackGroups[currentInputIndex].Tracks[currentTrackIndex].Title != null
                        || currentIsChapter && trackGroups[currentInputIndex].Chapters[currentTrackIndex].Title != null) 
                       && trackGroups[currentInputIndex].Duration != TimeSpan.MinValue)
                        currentInputIndex = currentTrackIndex = -1;
                }
            });

            return trackGroups;
        }

        public async Task Mix(List<TrackGroup> tracks, string output, List<Map> maps, Action<double>? progress = null)
        {
            var inputArgs = string.Join(" ", tracks.Select(tr => $"-i \"{tr.Path}\""));
            var mapArgs = string.Join(" ", maps.Select(mp => mp.ForChapter ? $"-map_chapters {mp.InputIndex}" : $"-map {mp.InputIndex}:{mp.TrackIndex}"));
            var outputExtension = Path.GetExtension(output);
            var subtitleEncode = "-c:s mov_text";
            var audioEncode = "-c:a copy";
            var disableDefaultMappingFromFirstInput = "-map_metadata -1 -map_chapters -1"; //By default, ffmpeg maps the global metadata and chapters from the first input. These arguments disable that.
            switch (outputExtension)
            {
                case ".mkv":
                    subtitleEncode = "-c:s copy";
                    break;
                case ".mp3":
                    audioEncode = string.Empty;
                    break;
            }
            var totalDuration = TimeSpan.MinValue;
            foreach (var map in maps)
            {
                if (tracks.Count <= map.InputIndex)
                    throw new ArgumentException(
                        $"You mapped to a track that does not exist. Input index: {map.InputIndex}");
                var trackDuration = tracks[map.InputIndex].Duration;
                if (totalDuration < trackDuration) totalDuration = trackDuration;
            }

            File.Delete(output);
            await StartProcess(ffmpegPath, $"{inputArgs} -c:v copy {audioEncode} {subtitleEncode} {disableDefaultMappingFromFirstInput} {mapArgs} -max_interleave_delta 0 \"{output}\"", null, (sender, args) =>
            {
                if (string.IsNullOrWhiteSpace(args.Data)) return;
                Debug.WriteLine(args.Data);
                if (progress == null) return;
                var matchCollection = Regex.Matches(args.Data, @"^(?:frame|size)=\s*.+?time=(\d{2}:\d{2}:\d{2}\.\d{2}).+");
                if (matchCollection.Count == 0) return;
                progress(TimeSpan.Parse(matchCollection[0].Groups[1].Value) / totalDuration * 100);
            });
            progress?.Invoke(100);
        }

        private static TrackType GetTrackType(string type) => type switch
        {
            "Video" => TrackType.Video,
            "Audio" => TrackType.Audio,
            "Subtitle" => TrackType.Subtitle,
            _ => TrackType.Other
        };

        private static async Task StartProcess(string processFileName, string arguments, DataReceivedEventHandler? outputEventHandler, DataReceivedEventHandler? errorEventHandler)
        {
            Process ffmpeg = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = processFileName,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                },
                EnableRaisingEvents = true
            };
            ffmpeg.OutputDataReceived += outputEventHandler;
            ffmpeg.ErrorDataReceived += errorEventHandler;
            ffmpeg.Start();
            ffmpeg.BeginErrorReadLine();
            ffmpeg.BeginOutputReadLine();
            await ffmpeg.WaitForExitAsync();
            ffmpeg.Dispose();
        }

        public enum TrackType{ Video, Audio, Subtitle, Other }
        public class Track(int index, TrackType type, string codec)
        {
            public int Index { get; set; } = index;
            public TrackType Type { get; set; } = type;
            public string Codec { get; set; } = codec;
            public string? Title { get; set; }
        }

        public class Chapter(int index)
        {
            public int Index { get; set; } = index;
            public string? Title { get; set; }

        }
        public class TrackGroup(string path)
        {
            public string Path { get; set; } = path;
            public TimeSpan Duration { get; set; }
            public List<Track> Tracks { get; set; } = [];
            public List<Chapter> Chapters { get; set; } = [];
        }
        public class Map(int inputIndex, int trackIndex, bool forChapter = false)
        {
            public int InputIndex { get; set; } = inputIndex;
            public int TrackIndex { get; set; } = trackIndex;
            public bool ForChapter { get; set; } = forChapter;
        }
    }
}
