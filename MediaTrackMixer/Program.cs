using System.Text.RegularExpressions;
using static MediaTrackMixer.MediaTrackMixer;

var mediaTrackMixer = new MediaTrackMixer.MediaTrackMixer();
var trackGroups = new List<TrackGroup>();
ConsoleColor[] colours =
[
    ConsoleColor.Magenta,
    ConsoleColor.Yellow,
    ConsoleColor.Cyan,
    ConsoleColor.Green,
    ConsoleColor.Blue,
    ConsoleColor.DarkCyan,
    ConsoleColor.Red,
    ConsoleColor.DarkMagenta,
    ConsoleColor.DarkYellow,
];
while (true)
{
    WriteTracks();
    await GetInputs();
    Console.Clear();
}

void WriteTracks()
{
    for (var i = 0; i < trackGroups.Count; i++)
    {
        var trackGroup = trackGroups[i];
        Console.ForegroundColor = colours[i % colours.Length];
        Console.WriteLine($"#{i}  {Path.GetFileName(trackGroup.Path)}");
        foreach (var track in trackGroup.Tracks)
        {
            Console.WriteLine($"#{i}:{track.Index}   ({GetTypeChar(track.Type)})  {track.Title}({track.Codec})");
        }
        if (trackGroup.Chapters.Any())
        {
            Console.WriteLine($"#{i}:C   (c)  {trackGroup.Chapters.Count} chapters");
        }
    }

    Console.ForegroundColor = ConsoleColor.White;
    if(trackGroups.Any()) Console.WriteLine();
}

async Task GetInputs()
{
    Console.WriteLine("Enter an empty input at any time to return to the main menu.");
    string? input;
    if (trackGroups.Any())
    {
        Console.WriteLine("Enter the menu's number to access the corresponding menu.");
        Console.WriteLine("1. Add tracks");
        Console.WriteLine("2. Replace tracks");
        Console.WriteLine("3. Remove single input");
        Console.WriteLine("4. Remove all inputs");
        Console.WriteLine("5. Mix tracks");
        input = Console.ReadLine();
    }
    else input = "2";

    switch (input)
    {
        case "1" or "2":
            var adding = input == "1";
            Console.WriteLine("Put the full paths of the input files in quotes. You can enter multiple paths separated by space or you can drag and drop the files.");
            input = Console.ReadLine();
            if(string.IsNullOrWhiteSpace(input)) return;
            var matchCollection = Regex.Matches(input, @"(?:""(.+?)"")+");
            var paths = matchCollection.Select(mc => mc.Groups[1].Value);
            if (adding) paths = trackGroups.Select(tg => tg.Path).Concat(paths);
            var inputs = paths as string[] ?? paths.ToArray();
            if(!inputs.Any()) return;
            trackGroups = await mediaTrackMixer.GetTracks(inputs);
            return;
        case "3":
            Console.WriteLine("Enter the index of the input you wish to remove");
            input = Console.ReadLine();
            if(!int.TryParse(input, out var index)) return;
            if(trackGroups.Count <= index) return;
            trackGroups.RemoveAt(index);
            return;
        case "4":
            trackGroups.Clear();
            return;
        case "5":
            Console.WriteLine("Enter the tracks you want to mix, separated by space, in the format explained below:");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("[InputIndex]:[TrackIndex] ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("- e.g 0:1 to select the second track (1) of the first input (0)");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("[InputIndex]:C ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("- e.g 1:C to select the chapters (C) of the second input (1)");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("[InputIndex]:[TrackLetters] ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("- e.g 1:a to select all the audio tracks (a) of the second input (1) or 0:s to select all the subtitle tracks (s) of the first input (0) or 2:vs to select all the audio and subtitle tracks (as) of the third input (2)");
            Console.WriteLine("The colon (:) can be omitted in most cases.");
            input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) return;
            var mapResult = GetMaps(input);
            if (mapResult.Maps == null)
            {
                Console.WriteLine(mapResult.ErrorMessage);
                Console.WriteLine("Press any key to retry");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Enter the full path of the output. If you enter an relative path, it's going to be relative to the path of the first input");
            input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input)) return;
            if (input[0] == '"') input = input[1..];
            if (input[^1] == '"') input = input[..^1];
            input = Path.Combine(Path.GetDirectoryName(trackGroups[0].Path)!, input);
            if (trackGroups.Any(tg => tg.Path == input))
            {
                Console.WriteLine("Output path cannot be the same as input path");
                Console.WriteLine("Press any key to retry");
                Console.ReadKey();
                return;
            }

            Console.WriteLine();
            await mediaTrackMixer.Mix(trackGroups, input, mapResult.Maps, progress =>
            {
                Console.Write("\r");
                Console.Write($"Mixing.... {Math.Round(progress, 2)}%{new string(' ', 3)}");
            });
            Console.WriteLine();
            Console.WriteLine("Done");
            Console.ReadKey();
            return;
    }
}

char GetTypeChar(TrackType type) => type switch
{
    TrackType.Video => 'v',
    TrackType.Audio => 'a',
    TrackType.Subtitle => 's',
    _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
};

(List<Map>? Maps, string? ErrorMessage) GetMaps(string mapsArg)
{
    var mapStrings = mapsArg.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct();
    var maps = new List<Map>();
    foreach (var mapString in mapStrings)
    {
        var matchCollection = Regex.Matches(mapString, @"(\d+?):?(\d+)");
        if (matchCollection.Count > 0)
        {
            var inputIndex = int.Parse(matchCollection[0].Groups[1].Value);
            if (trackGroups.Count <= inputIndex) return InputIndexError(mapString, inputIndex);
            var trackIndex = int.Parse(matchCollection[0].Groups[2].Value);
            if (trackGroups[inputIndex].Tracks.Count <= trackIndex)
                return TrackIndexError(mapString, inputIndex, trackIndex);
            maps.Add(new Map(inputIndex, trackIndex));
            continue;
        }

        matchCollection = Regex.Matches(mapString, @"(\d+):?([acsvACSV]+)");
        if (matchCollection.Count <= 0) return (null, $"{mapString} not valid");

        {
            var inputIndex = int.Parse(matchCollection[0].Groups[1].Value);
            if (trackGroups.Count <= inputIndex) return InputIndexError(mapString, inputIndex);
            var trackLetters = matchCollection[0].Groups[2].Value.Distinct();
            foreach (var letter in trackLetters)
            {
                switch (letter)
                {
                    case 'v':
                        maps.AddRange(trackGroups[inputIndex].Tracks.Where(tr => tr.Type == TrackType.Video).Select(tr => new Map(inputIndex, tr.Index)));
                        break;
                    case 'a':
                        maps.AddRange(trackGroups[inputIndex].Tracks.Where(tr => tr.Type == TrackType.Audio).Select(tr => new Map(inputIndex, tr.Index)));
                        break;
                    case 's':
                        maps.AddRange(trackGroups[inputIndex].Tracks.Where(tr => tr.Type == TrackType.Subtitle).Select(tr => new Map(inputIndex, tr.Index)));
                        break;
                    case 'c':
                        maps.Add(new Map(inputIndex, 0, true));
                        break;
                }
            }
        }
    }
    return (maps, null);

    (List<Map>? Maps, string ErrorMessage) InputIndexError(string mapString, int inputIndex) => 
        (null, $"{mapString} not valid. InputIndex {inputIndex} exceeds number of inputs ({trackGroups.Count})");
    (List<Map>? Maps, string ErrorMessage) TrackIndexError(string mapString, int inputIndex, int trackIndex) => 
        (null, $"{mapString} not valid. TrackIndex {trackIndex} exceeds number of tracks ({trackGroups[inputIndex].Tracks.Count}) in input #{inputIndex}");
}
