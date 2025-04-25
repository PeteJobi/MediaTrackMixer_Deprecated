## Media Track Mixer
Using **Media Track Mixer**, you can extract and/or combine media tracks from multiple input media files. For example, you can combine your .mp4 video file with your .srt subtitle file and save it into a .mkv file, or you can extract .mp3 from a .mp4/.mkv music video, or even convert between .mp4 and .mkv. Best of all, this program keeps the encoding of your media files in tact!

![Screenshot (256)](https://github.com/user-attachments/assets/e59ac2f0-039e-4b66-8964-93036d1039c7)


## How to build
You need to have at least .NET 9 runtime installed to build the software. Download the latest runtime [here](https://dotnet.microsoft.com/en-us/download). If you're not sure which one to download, try [.NET 9.0 Version 9.0.203](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-9.0.203-windows-x64-installer)

In the project folder, run the below
```
dotnet publish -p:PublishSingleFile=true -r win-x64 -c Release --self-contained false
```
When that completes, go to `\bin\Release\net<version>-windows\win-x64\publish` and you'll find the **MediaTrackMixer.exe**.

## Run without building
You can also just download the release builds if you don't wish to build manually. The assets release contains the assets used by the software, which is just the ffmpeg executable. If you already have this on your system (or you'd rather get it from elsewhere), you won't need to download this. The main release contains the compiled executable.

If you wish to run the software without installing the required .NET runtime, download the self-contained release.

## How to use
When you open the program, you will be prompted to enter the full paths of input media files from which you wish to extract/combine tracks. Each path should be contained in quotes, and anything outside quotes is discarded. You can drag and drop the files instead of typing them out. Enter the corresponding menu number to use a menu.

1. **Add inputs**: Add more inputs to the ones you've already added.
2. **Replace inputs**: Clear out what you've inputed by replacing them with new ones.
3. **Remove single input**: Remove a single input by specifying the input's zero-based index. Zero-based means the first input has an index of 0, second has an index of 1 e.t.c.
4. **Remove all inputs**: Clear out all the inputs you've added to start afresh.

The last menu, "**5. Mix tracks**", does what you're here for. You'll need to enter the tracks you want to mix, using one or more of the formats below:
- **[InputIndex]:[TrackIndex]**..... **InputIndex** represents the zero-based index of the input media file, and **TrackIndex** represents the zero-based index of the track contained in that input. Zero-based means the first input has an index of 0, second has an index of 1 e.t.c. To select the first track of the second input, you would use **1:0**.
- **[InputIndex]:C**.... **C** means that you're selecting the chapters of this input. You can't select individual chapters - it has to be all or none. Use 0:C to select the chapters of the first input.
- **[InputIndex]:[TrackLetter(s)]**..... **TrackLetter** represents the letter of the track type, which can be **v** (video), **a** (audio), **s** (subtitles) or **c** (chapters). Use **TrackLetter** to select all tracks of that type. For example, if the first input has video track 0:0 and two audio tracks 0:1 and 0:2, you can use **0:a** to select both audio tracks. You can also use two or more letters. In the previous example, you can use **0:va** instead of typing out "0:0 0:1 0:2". The order of the letters do not matter. **0:avs** will select all audio, video and subtitle tracks of the first input.

The tracks you select must be separated by whitespace. You can also omit the colon (:), but you probably shouldn't do that for **[InputIndex]:[TrackIndex]** if there are 2-digit indices.

The order of the selection matters. If you wish to set a default track in a media file, you can select that track first. For example, if there are 3 subtitle track in your video file and you wish to select the 3rd one as the default, assuming that the subtitle tracks are 2, 3, 4, then your selection should be **"0:va 0:4 0:2 0:3"**. Track 0:4, which is the 3rd subtitle track, will be the default.

After you're done selecting tracks, you will need to enter the path of the output file, including the extension. If you specify a relative path (for example, a path without "C:" on Windows), that path will be relative to the path of the first input. So if you enter "MyVideo.mkv" as the output file, a file named as such will be created in the same folder that contains the input file. **Be warned** that if the specified output file already exists, it will be overwritten!

The file types that this program officially supports are .mkv, .mp4 and .mp3. But there are no limitations at all to file types, and any media file with tracks may work. Keep in mind that there may be limitations with certain file types - for example, .mp3 files cannot store video or subtitle tracks.
