using System.Text;

namespace DmxBox.Tests.Projects;

/// <summary>
/// Project files: saving and loading the memories and the notes, the list of projects, the
/// "unsaved" mark, and files that cannot be used. Every test has a projects folder of its own.
/// </summary>
public sealed class ProjectStoreTests : IDisposable
{
    private readonly DirectoryInfo _folder = Directory.CreateTempSubdirectory("lumenet-test-");
    private readonly Desk _desk = new();
    private readonly ProjectStore _store;

    /// <summary>Every test starts with a new desk and an empty projects folder.</summary>
    public ProjectStoreTests()
    {
        _store = new ProjectStore(_desk, _folder.FullName);
    }

    /// <summary>Deletes the test's projects folder.</summary>
    public void Dispose()
    {
        try
        {
            _folder.Delete(recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A leftover folder in the temp directory is harmless. It must not fail a test.
        }
    }

    // ---------------------------------------------------------------- saving

    [Fact]
    public void NewProjectIsUntitledAndHasNoFile()
    {
        Assert.Equal(new ProjectInfo("Untitled", Path: null, Unsaved: false), _store.Current);
        Assert.Equal(_folder.FullName, _store.Folder);
    }

    [Fact]
    public void SaveWritesTheDocumentedFormat()
    {
        _desk.SetUniverses(2);
        _desk.SetBoxes([new BoxPlan("192.168.7.1", [1]), new BoxPlan("10.1.3.1", [2, 0])]);
        StoreMemory(1, (Channel: 5, Level: 255), (Channel: 6, Level: 128), (Channel: 600, Level: 10));
        StoreMemory(3);   // a dark look: stored, but without levels
        _desk.SetChannelNote(5, "Front left");
        _desk.SetChannelNote(512, "Smoke machine");
        _desk.SetChannelNote(513, "Hazer");
        _desk.SetMemoryNote(3, "Blue wash");
        _desk.SetIndependent(2, 300);
        _desk.SetMemoryIndependent(10, 128);
        _desk.SetSequence(7, loop: true, [new Step("channel", 5, 255, 1, 0.5, 2)]);
        _desk.AddStackStep(new Step("memory", 1, 200, 3, 0, 4));

        _store.Save("show");

        string channelKeys = string.Join(",\n    ", Enumerable.Range(1, 32).Select(key => key == 2 ? 300 : key));
        string memoryKeys = string.Join(",\n    ", Enumerable.Range(1, 32).Select(key => key == 10 ? 128 : key));
        string expected = $$"""
            {
              "format": "lumenet-project",
              "version": 5,
              "universes": 2,
              "boxes": [
                {
                  "address": "192.168.7.1",
                  "universes": [
                    1
                  ]
                },
                {
                  "address": "10.1.3.1",
                  "universes": [
                    2,
                    0
                  ]
                }
              ],
              "memories": [
                {
                  "number": 1,
                  "levels": {
                    "5": 255,
                    "6": 128,
                    "600": 10
                  }
                },
                {
                  "number": 3,
                  "levels": {}
                }
              ],
              "channelNotes": {
                "5": "Front left",
                "512": "Smoke machine",
                "513": "Hazer"
              },
              "memoryNotes": {
                "3": "Blue wash"
              },
              "independents": [
                {{channelKeys}}
              ],
              "memoryIndependents": [
                {{memoryKeys}}
              ],
              "sequences": [
                {
                  "number": 7,
                  "loop": true,
                  "random": false,
                  "speed": 1,
                  "steps": [
                    {
                      "target": "channel",
                      "number": 5,
                      "value": 255,
                      "up": 1,
                      "wait": 0.5,
                      "down": 2
                    }
                  ]
                }
              ],
              "stack": [
                {
                  "cue": "1.0",
                  "target": "memory",
                  "number": 1,
                  "value": 200,
                  "up": 3,
                  "wait": 0,
                  "down": 4
                }
              ],
              "stage": {
                "width": 16,
                "depth": 10,
                "lights": []
              }
            }
            """;
        Assert.Equal(expected.ReplaceLineEndings(), File.ReadAllText(PathOf("show")).ReplaceLineEndings());
    }

    [Fact]
    public void NewProjectHasEmptyListsAndThe32FirstChannelsAndMemoriesInItsFile()
    {
        _store.Save("show");

        string keys = string.Join(",\n    ", Enumerable.Range(1, 32));
        string expected = $$"""
            {
              "format": "lumenet-project",
              "version": 5,
              "universes": 1,
              "memories": [],
              "channelNotes": {},
              "memoryNotes": {},
              "independents": [
                {{keys}}
              ],
              "memoryIndependents": [
                {{keys}}
              ],
              "sequences": [],
              "stack": [],
              "stage": {
                "width": 16,
                "depth": 10,
                "lights": []
              }
            }
            """;
        Assert.Equal(expected.ReplaceLineEndings(), File.ReadAllText(PathOf("show")).ReplaceLineEndings());
    }

    [Fact]
    public void NoteIsWrittenTheWayItWasTyped()
    {
        // Letters beyond ASCII, and signs that JSON writers like to turn into codes.
        string note = "Sv" + (char)0x11B + "tlo & <stage> 'left'";
        _desk.SetChannelNote(1, note);

        _store.Save("show");

        Assert.Contains($"\"1\": \"{note}\"", File.ReadAllText(PathOf("show")));
        Assert.Equal(note, LoadIntoNewDesk("show").GetState().ChannelNotes[0]);
    }

    [Fact]
    public void SavedFileIsUtf8WithoutByteOrderMark()
    {
        _store.Save("show");

        Assert.Equal((byte)'{', File.ReadAllBytes(PathOf("show"))[0]);
    }

    [Fact]
    public void SaveLeavesNoTemporaryFileBehind()
    {
        _store.Save("show");

        Assert.Equal(["show.json"], _folder.GetFiles().Select(file => file.Name));
    }

    [Fact]
    public void SaveMakesTheProjectTheOpenOne()
    {
        ProjectInfo saved = _store.Save("show");

        Assert.Equal(new ProjectInfo("show", PathOf("show"), Unsaved: false), saved);
        Assert.Equal(saved, _store.Current);
    }

    [Fact]
    public void SaveCreatesTheProjectsFolder()
    {
        string folder = Path.Combine(_folder.FullName, "not", "there", "yet");
        var store = new ProjectStore(_desk, folder);

        store.Save("show");

        Assert.True(File.Exists(Path.Combine(folder, "show.json")));
    }

    [Fact]
    public void SaveUnderAnExistingNameReplacesTheFile()
    {
        StoreMemory(1, (Channel: 1, Level: 10));
        _store.Save("show");
        _desk.DeleteMemory(1);
        StoreMemory(2, (Channel: 2, Level: 20));

        _store.Save("show");

        byte[]?[] looks = LoadIntoNewDesk("show").GetProject().Content.Looks;
        Assert.Null(looks[0]);
        Assert.Equal(20, looks[1]![1]);
    }

    [Fact]
    public void SaveWithoutANameUsesTheFileOfTheProject()
    {
        _store.Save("show");
        StoreMemory(7, (Channel: 1, Level: 10));

        ProjectInfo saved = _store.Save(null);

        Assert.Equal(new ProjectInfo("show", PathOf("show"), Unsaved: false), saved);
        Assert.Equal(10, LoadIntoNewDesk("show").GetProject().Content.Looks[6]![0]);
    }

    [Fact]
    public void SaveWithoutANameFailsForAProjectThatWasNeverSaved()
    {
        var error = Assert.Throws<DeskException>(() => _store.Save(null));

        Assert.Equal("This project has no file yet. Save it under a name.", error.Message);
        Assert.Empty(_folder.GetFiles());
    }

    [Fact]
    public void NameThatIsNoFileNameIsRejectedBeforeAnythingIsWritten()
    {
        Assert.Throws<DeskException>(() => _store.Save("..\\outside"));
        Assert.Throws<DeskException>(() => _store.Save("in/side"));
        Assert.Throws<DeskException>(() => _store.Save("NUL"));
        Assert.Throws<DeskException>(() => _store.Load("../outside"));

        Assert.Empty(_folder.GetFiles());
        Assert.False(File.Exists(Path.Combine(_folder.Parent!.FullName, "outside.json")));
        Assert.Equal("Untitled", _store.Current.Name);
    }

    // ---------------------------------------------------------------- loading

    [Fact]
    public void SaveAndLoadBringEveryMemoryBack()
    {
        StoreMemory(1, (Channel: 1, Level: 1), (Channel: 2, Level: 255), (Channel: 512, Level: 128));
        StoreMemory(2);
        StoreMemory(128, (Channel: 300, Level: 77));
        byte[]?[] saved = _desk.GetProject().Content.Looks;
        _store.Save("show");

        Desk other = LoadIntoNewDesk("show");

        byte[]?[] loaded = other.GetProject().Content.Looks;
        for (int index = 0; index < 128; index++)
            Assert.Equal(saved[index], loaded[index]);

        Assert.Equal(3, other.GetState().MemoryStored.Count(stored => stored));
    }

    [Fact]
    public void LoadReplacesTheMemoriesBringsTheirFadersDownAndKeepsTheChannelFaders()
    {
        StoreMemory(1, (Channel: 1, Level: 10));
        _store.Save("show");
        _desk.DeleteMemory(1);
        StoreMemory(2, (Channel: 2, Level: 20));   // not in the file: has to go
        _desk.SetChannel(9, 99);
        _desk.SetMemoryLevel(1, 255);
        _desk.SetMemoryLevel(2, 255);

        _store.Load("show");

        DeskState state = _desk.GetState();
        Assert.True(state.MemoryStored[0]);
        Assert.False(state.MemoryStored[1]);
        Assert.Equal(new int[128], state.MemoryLevels);
        Assert.Equal(99, state.Channels[8]);
    }

    [Fact]
    public void LoadMakesTheProjectTheOpenOne()
    {
        _store.Save("first");
        _store.Save("second");

        ProjectInfo loaded = _store.Load("first");

        Assert.Equal(new ProjectInfo("first", PathOf("first"), Unsaved: false), loaded);
        Assert.Equal(loaded, _store.Current);
    }

    [Fact]
    public void LoadAcceptsAFileWithAByteOrderMark()
    {
        // Some editors put one in front when a file is saved by hand.
        string json = """{ "format": "lumenet-project", "version": 1, "memories": [ { "number": 4, "levels": { "9": 90 } } ] }""";
        File.WriteAllText(PathOf("edited"), json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        _store.Load("edited");

        Assert.Equal(90, _desk.GetProject().Content.Looks[3]![8]);
    }

    [Fact]
    public void SaveAndLoadBringEveryNoteBack()
    {
        _desk.SetChannelNote(1, "Front left");
        _desk.SetChannelNote(512, new string('x', 24));
        _desk.SetMemoryNote(1, "Blue wash");
        _desk.SetMemoryNote(128, "Finale");
        DeskState saved = _desk.GetState();
        _store.Save("show");

        DeskState loaded = LoadIntoNewDesk("show").GetState();

        Assert.Equal(saved.ChannelNotes, loaded.ChannelNotes);
        Assert.Equal(saved.MemoryNotes, loaded.MemoryNotes);
    }

    [Fact]
    public void LoadReplacesAllNotes()
    {
        _desk.SetChannelNote(1, "Front left");
        _desk.SetMemoryNote(1, "Blue wash");
        _store.Save("show");
        _desk.SetChannelNote(1, "changed");
        _desk.SetChannelNote(2, "not in the file");
        _desk.SetMemoryNote(2, "not in the file");

        _store.Load("show");

        DeskState state = _desk.GetState();
        Assert.Equal(["Front left", ""], state.ChannelNotes[..2]);
        Assert.Equal(["Blue wash", ""], state.MemoryNotes[..2]);
    }

    [Fact]
    public void FileWithoutNotesIsValidAndClearsTheNotes()
    {
        // A file from before there were notes: it has the memories only.
        File.WriteAllText(PathOf("old"), ProjectWith("""{ "number": 4, "levels": { "9": 90 } }"""));
        _desk.SetChannelNote(1, "Front left");
        _desk.SetMemoryNote(1, "Blue wash");

        _store.Load("old");

        DeskState state = _desk.GetState();
        Assert.True(state.MemoryStored[3]);
        Assert.All(state.ChannelNotes, note => Assert.Equal("", note));
        Assert.All(state.MemoryNotes, note => Assert.Equal("", note));
    }

    [Fact]
    public void NoteInAFileIsTrimmedAndAnEmptyOneIsNoNote()
    {
        File.WriteAllText(
            PathOf("edited"),
            ProjectWithNotes("""{ "1": "  Front left ", "2": "", "3": "   " }""", """{ "128": "Finale\n" }"""));

        _store.Load("edited");

        DeskState state = _desk.GetState();
        Assert.Equal(["Front left", "", ""], state.ChannelNotes[..3]);
        Assert.Equal("Finale", state.MemoryNotes[127]);
    }

    [Fact]
    public void SaveAndLoadBringTheIndependentKeysBack()
    {
        _desk.SetIndependent(1, 512);
        _desk.SetIndependent(10, 1);
        _desk.SetMemoryIndependent(3, 77);
        int[] saved = _desk.GetState().Independents;
        int[] savedMemoryKeys = _desk.GetState().MemoryIndependents;
        _store.Save("show");
        _desk.SetIndependent(1, 77);   // changed after the save: the load has to undo it
        _desk.SetMemoryIndependent(3, 3);

        _store.Load("show");

        Assert.Equal(saved, _desk.GetState().Independents);
        Assert.Equal(savedMemoryKeys, _desk.GetState().MemoryIndependents);
        Assert.Equal(saved, LoadIntoNewDesk("show").GetState().Independents);
        Assert.Equal(savedMemoryKeys, LoadIntoNewDesk("show").GetState().MemoryIndependents);
    }

    [Fact]
    public void FileWithoutIndependentsPutsTheKeysOnTheFirstTenChannelsAndMemories()
    {
        // A file from before there were independent keys.
        File.WriteAllText(PathOf("old"), ProjectWith("""{ "number": 4, "levels": { "9": 90 } }"""));
        _desk.SetIndependent(1, 300);
        _desk.SetMemoryIndependent(1, 30);

        _store.Load("old");

        Assert.Equal(Desk.DefaultIndependents(), _desk.GetState().Independents);
        Assert.Equal(Desk.DefaultMemoryIndependents(), _desk.GetState().MemoryIndependents);
    }

    [Fact]
    public void FileFromBeforeTheMemoryKeysKeepsItsSixteenChannelKeys()
    {
        // Versions 1 and 2 had sixteen channel keys and no memory keys. Keys 17 to 32 are where
        // they are on a new desk.
        string sixteen = string.Join(", ", Enumerable.Range(101, 16));
        File.WriteAllText(PathOf("old"), $$"""{ "format": "lumenet-project", "version": 2, "memories": [], "independents": [{{sixteen}}] }""");

        _store.Load("old");

        Assert.Equal(Enumerable.Range(101, 16).Concat(Enumerable.Range(17, 16)), _desk.GetState().Independents);
        Assert.Equal(Desk.DefaultMemoryIndependents(), _desk.GetState().MemoryIndependents);
    }

    [Fact]
    public void FileOfVersion3KeepsItsTenKeysOfEachKind()
    {
        string channels = string.Join(", ", Enumerable.Range(201, 10));
        string memories = string.Join(", ", Enumerable.Range(41, 10));
        File.WriteAllText(
            PathOf("old"),
            $$"""{ "format": "lumenet-project", "version": 3, "memories": [], "independents": [{{channels}}], "memoryIndependents": [{{memories}}] }""");

        _store.Load("old");

        Assert.Equal(Enumerable.Range(201, 10).Concat(Enumerable.Range(11, 22)), _desk.GetState().Independents);
        Assert.Equal(Enumerable.Range(41, 10).Concat(Enumerable.Range(11, 22)), _desk.GetState().MemoryIndependents);
    }

    [Fact]
    public void LoadingAProjectThatIsNotThereFails()
    {
        var error = Assert.Throws<DeskException>(() => _store.Load("nothing"));

        Assert.Equal("There is no project \"nothing\".", error.Message);
    }

    // ---------------------------------------------------------------- files that cannot be used

    [Fact]
    public void FileThatIsNotJsonIsRefused()
    {
        AssertRefused("damaged", "this is not JSON");
        AssertRefused("damaged", "");
        AssertRefused("damaged", """{ "format": "lumenet-project", "version": 1, "memories": [""");
        AssertRefused("damaged", "[1, 2, 3]");
        AssertRefused("holds no project", "null");
    }

    [Fact]
    public void FileOfAnotherFormatIsRefused()
    {
        AssertRefused("not a Lumenet project", """{ "format": "other-program", "version": 1, "memories": [] }""");
        AssertRefused("not a Lumenet project", """{ "version": 1, "memories": [] }""");
        AssertRefused("not a Lumenet project", """{ "name": "some other JSON file" }""");
        AssertRefused("not a Lumenet project", "{}");
    }

    [Fact]
    public void FileOfANewerVersionIsRefused()
    {
        AssertRefused("version 11", """{ "format": "lumenet-project", "version": 11, "memories": [] }""");
    }

    [Fact]
    public void UniversesAndBoxesComeBackFromTheFile()
    {
        _desk.SetUniverses(3);
        _desk.SetBoxes([new BoxPlan("10.1.2.1", [3, 1]), new BoxPlan("127.0.0.1:7001", [2])]);
        StoreMemory(1, (Channel: 1100, Level: 77));
        _desk.SetChannelNote(1536, "Last of U3");
        _store.Save("show");

        Desk loaded = LoadIntoNewDesk("show");

        Assert.Equal(3, loaded.Universes);
        Assert.Equal(1536, loaded.ChannelCount);
        Assert.Equal(["10.1.2.1", "127.0.0.1:7001"], loaded.Boxes.Select(box => box.Address));
        Assert.Equal([3, 1], loaded.Boxes[0].Universes);
        Assert.Equal(1536, loaded.GetState().Channels.Length);
        Assert.Equal("Last of U3", loaded.GetState().ChannelNotes[1535]);
        Assert.Equal(1536, loaded.GetMemoryLooks()[1].Length);
        Assert.Equal(77, loaded.GetMemoryLooks()[1][1099]);
    }

    [Fact]
    public void FileOfAnEarlierVersionHasOneUniverseAndLetsTheBoxesPlayByThemselves()
    {
        File.WriteAllText(PathOf("old"), """{ "format": "lumenet-project", "version": 4, "memories": [ { "number": 1, "levels": { "512": 9 } } ] }""");

        Desk loaded = LoadIntoNewDesk("old");

        Assert.Equal(1, loaded.Universes);
        Assert.Empty(loaded.Boxes);
        Assert.Equal(9, loaded.GetMemoryLooks()[1][511]);
    }

    [Theory]
    [InlineData(""" "universes": 0, "memories": [] """, "The file says 0 universes; a project has 1 to 8.")]
    [InlineData(""" "universes": 9, "memories": [] """, "The file says 9 universes; a project has 1 to 8.")]
    [InlineData(""" "universes": 1, "memories": [ { "number": 1, "levels": { "513": 10 } } ] """, "from 1 to 512")]
    [InlineData(""" "universes": 2, "channelNotes": { "1025": "x" }, "memories": [] """, "from 1 to 1024")]
    [InlineData(""" "boxes": [ { "address": "10.1", "universes": [1] } ], "memories": [] """, "is no address like 10.1.3.1")]
    [InlineData(""" "boxes": [ { "address": "10.1.1.1", "universes": [] } ], "memories": [] """, "1 to 8 outputs")]
    [InlineData(""" "boxes": [ { "address": "10.1.1.1", "universes": [9] } ], "memories": [] """, "universe 1 to 8, or none (0)")]
    [InlineData(""" "boxes": [ { "address": "10.1.1.1", "universes": [1] }, { "address": "10.1.1.1:7000", "universes": [2] } ], "memories": [] """, "twice")]
    [InlineData(""" "boxes": [ { "universes": [1] } ], "memories": [] """, "Box 1")]
    public void FileWithUniversesOrBoxesThatCannotBeUsedIsRefused(string part, string expectedInMessage)
    {
        AssertRefused(expectedInMessage, $$"""{ "format": "lumenet-project", "version": 5, {{part}} }""");
    }

    [Fact]
    public void FileWithoutAUsableVersionIsRefused()
    {
        AssertRefused("which version", """{ "format": "lumenet-project", "memories": [] }""");
        AssertRefused("which version", """{ "format": "lumenet-project", "version": 0, "memories": [] }""");
        AssertRefused("damaged", """{ "format": "lumenet-project", "version": "one", "memories": [] }""");
    }

    [Fact]
    public void FileWithoutMemoriesIsRefused()
    {
        AssertRefused("no list of memories", """{ "format": "lumenet-project", "version": 1 }""");
        AssertRefused("empty entry", """{ "format": "lumenet-project", "version": 1, "memories": [ null ] }""");
    }

    [Fact]
    public void FileWithABadMemoryNumberIsRefused()
    {
        AssertRefused("from 1 to 128", ProjectWith("""{ "number": 0, "levels": {} }"""));
        AssertRefused("from 1 to 128", ProjectWith("""{ "number": 129, "levels": {} }"""));
        AssertRefused("without a number", ProjectWith("""{ "levels": {} }"""));
        AssertRefused("damaged", ProjectWith("""{ "number": 1.5, "levels": {} }"""));
        AssertRefused("damaged", ProjectWith("""{ "number": "first", "levels": {} }"""));
        AssertRefused("memory 7 twice", ProjectWith("""{ "number": 7, "levels": {} }, { "number": 7, "levels": {} }"""));
    }

    [Fact]
    public void FileWithABadChannelNumberIsRefused()
    {
        AssertRefused("from 1 to 512", ProjectWith("""{ "number": 1, "levels": { "0": 10 } }"""));
        AssertRefused("from 1 to 512", ProjectWith("""{ "number": 1, "levels": { "513": 10 } }"""));
        AssertRefused("damaged", ProjectWith("""{ "number": 1, "levels": { "front": 10 } }"""));
        AssertRefused("no levels", ProjectWith("""{ "number": 1 }"""));
    }

    [Fact]
    public void FileWithABadLevelIsRefused()
    {
        AssertRefused("from 0 to 255", ProjectWith("""{ "number": 1, "levels": { "5": 256 } }"""));
        AssertRefused("from 0 to 255", ProjectWith("""{ "number": 1, "levels": { "5": -1 } }"""));
        AssertRefused("damaged", ProjectWith("""{ "number": 1, "levels": { "5": 12.5 } }"""));
        AssertRefused("damaged", ProjectWith("""{ "number": 1, "levels": { "5": "full" } }"""));
    }

    [Fact]
    public void FileWithOneBadMemoryLoadsNoneOfItsMemories()
    {
        // Memory 1 in the file is fine, memory 2 is not. Memory 1 must not arrive on the desk either.
        AssertRefused(
            "from 0 to 255",
            ProjectWith("""{ "number": 1, "levels": { "1": 10 } }, { "number": 2, "levels": { "1": 999 } }"""));
    }

    [Fact]
    public void FileWithANoteForAFaderThatDoesNotExistIsRefused()
    {
        AssertRefused("note for channel 0", ProjectWithNotes("""{ "0": "Front left" }""", "{}"));
        AssertRefused("from 1 to 512", ProjectWithNotes("""{ "513": "Front left" }""", "{}"));
        AssertRefused("note for memory 0", ProjectWithNotes("{}", """{ "0": "Blue wash" }"""));
        AssertRefused("from 1 to 128", ProjectWithNotes("{}", """{ "129": "Blue wash" }"""));
        AssertRefused("damaged", ProjectWithNotes("""{ "front": "Front left" }""", "{}"));
    }

    [Fact]
    public void FileWithANoteThatIsTooLongIsRefused()
    {
        string tooLong = new string('a', 25);

        AssertRefused("at most 24 characters", ProjectWithNotes($$"""{ "5": "{{tooLong}}" }""", "{}"));
        AssertRefused("at most 24 characters", ProjectWithNotes("{}", $$"""{ "3": "{{tooLong}}" }"""));
    }

    [Fact]
    public void FileWithAControlCharacterInANoteIsRefused()
    {
        // In JSON a tab inside a text is written as \t, and the character 127 as \u007f.
        AssertRefused("a tab, a line break or the like", ProjectWithNotes("""{ "5": "Front\tleft" }""", "{}"));
        AssertRefused("a tab, a line break or the like", ProjectWithNotes("{}", """{ "3": "Blue\nwash" }"""));
        AssertRefused(
            "a tab, a line break or the like", ProjectWithNotes("{ \"5\": \"Front\\" + "u007fleft\" }", "{}"));
    }

    [Fact]
    public void FileWithANoteThatIsNoTextIsRefused()
    {
        AssertRefused("damaged", ProjectWithNotes("""{ "5": 17 }""", "{}"));
        AssertRefused("damaged", ProjectWithNotes("""{ "5": true }""", "{}"));
        AssertRefused("damaged", ProjectWithNotes("""{ "5": { "text": "Front left" } }""", "{}"));
        AssertRefused("damaged", ProjectWithNotes("{}", """{ "3": ["Blue wash"] }"""));
        AssertRefused("not a text", ProjectWithNotes("""{ "5": null }""", "{}"));
        AssertRefused("not a text", ProjectWithNotes("{}", """{ "3": null }"""));
    }

    [Fact]
    public void FileWhoseNotesAreNoListIsRefused()
    {
        AssertRefused("damaged", ProjectWithNotes("\"Front left\"", "{}"));
        AssertRefused("damaged", ProjectWithNotes("{}", "[]"));
    }

    [Fact]
    public void FileWithOneBadNoteLoadsNothingOfIt()
    {
        // The memory and the first note are fine. Neither must arrive on the desk.
        AssertRefused(
            "at most 24 characters",
            """
            {
              "format": "lumenet-project",
              "version": 1,
              "memories": [ { "number": 9, "levels": { "1": 10 } } ],
              "channelNotes": { "1": "Front left", "2": "this note is far too long to fit" }
            }
            """);
    }

    [Fact]
    public void FileWithTheWrongNumberOfIndependentsIsRefused()
    {
        string fifteen = string.Join(", ", Enumerable.Range(1, 15));
        string seventeen = string.Join(", ", Enumerable.Range(1, 17));

        AssertRefused("lists 15 independents; there must be 16", ProjectWithIndependents($"[{fifteen}]"));
        AssertRefused("lists 17 independents; there must be 16", ProjectWithIndependents($"[{seventeen}]"));
        AssertRefused("lists 0 independents", ProjectWithIndependents("[]"));
    }

    [Fact]
    public void FileWithAnIndependentOnAChannelThatDoesNotExistIsRefused()
    {
        string firstFifteen = string.Join(", ", Enumerable.Range(1, 15));

        AssertRefused("Independent 16 in the file is on channel 513", ProjectWithIndependents($"[{firstFifteen}, 513]"));
        AssertRefused("Independent 16 in the file is on channel 0", ProjectWithIndependents($"[{firstFifteen}, 0]"));
        AssertRefused("from 1 to 512", ProjectWithIndependents($"[-1, {firstFifteen}]"));
    }

    [Fact]
    public void FileWhoseIndependentsAreNotWholeNumbersIsRefused()
    {
        string firstFifteen = string.Join(", ", Enumerable.Range(1, 15));

        AssertRefused("damaged", ProjectWithIndependents($"[{firstFifteen}, 16.5]"));
        AssertRefused("damaged", ProjectWithIndependents($"[{firstFifteen}, \"16\"]"));
        AssertRefused("damaged", ProjectWithIndependents($"[{firstFifteen}, null]"));
        AssertRefused("damaged", ProjectWithIndependents("{ \"1\": 1 }"));
        AssertRefused("damaged", ProjectWithIndependents("16"));
    }

    [Fact]
    public void FileOfVersionThreeWithWrongKeysIsRefused()
    {
        string ten = string.Join(", ", Enumerable.Range(1, 10));
        string sixteen = string.Join(", ", Enumerable.Range(1, 16));
        string nine = string.Join(", ", Enumerable.Range(1, 9));
        string Version3(string keys) => $$"""{ "format": "lumenet-project", "version": 3, "memories": [], {{keys}} }""";

        AssertRefused("lists 16 independents; there must be 10", Version3($"\"independents\": [{sixteen}]"));
        AssertRefused("lists 9 memory independents; there must be 10", Version3($"\"memoryIndependents\": [{nine}]"));
        AssertRefused(
            "Memory independent 10 in the file is on memory 129; memories go from 1 to 128",
            Version3($"\"memoryIndependents\": [{nine}, 129]"));
        AssertRefused("Memory independent 1 in the file is on memory 0", Version3($"\"independents\": [{ten}], \"memoryIndependents\": [0, {nine}]"));
    }

    // ---------------------------------------------------------------- the list of projects

    [Fact]
    public void ListShowsTheProjectFilesTheNewestFirst()
    {
        _store.Save("old");
        _store.Save("new");
        _store.Save("middle");
        File.SetLastWriteTimeUtc(PathOf("old"), new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(PathOf("middle"), new DateTime(2026, 2, 1, 12, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(PathOf("new"), new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc));
        File.WriteAllText(Path.Combine(_folder.FullName, "notes.txt"), "not a project");

        ProjectList list = _store.List();

        Assert.Equal(_folder.FullName, list.Folder);
        Assert.Equal(["new", "middle", "old"], list.Projects.Select(project => project.Name));
        Assert.Equal(PathOf("new"), list.Projects[0].Path);
        Assert.Equal(new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc), list.Projects[0].Modified);
        Assert.Equal(DateTimeKind.Utc, list.Projects[0].Modified.Kind);
    }

    [Fact]
    public void ListOfAFolderThatIsNotThereYetIsEmptyAndCreatesIt()
    {
        string folder = Path.Combine(_folder.FullName, "new folder");
        var store = new ProjectStore(_desk, folder);

        ProjectList list = store.List();

        Assert.Empty(list.Projects);
        Assert.Empty(list.Examples);
        Assert.True(Directory.Exists(folder));
    }

    [Fact]
    public void ExamplesAreListedByNameAndOpenWithoutAFileAndWithNothingToSave()
    {
        string examples = Path.Combine(_folder.FullName, "examples");
        Directory.CreateDirectory(examples);
        _desk.SetNotes("How to use it");
        _store.Save("2 Second");
        File.Move(PathOf("2 Second"), Path.Combine(examples, "2 Second.json"));
        File.Copy(Path.Combine(examples, "2 Second.json"), Path.Combine(examples, "1 First.json"));
        File.WriteAllText(Path.Combine(examples, "readme.txt"), "not an example");
        var store = new ProjectStore(_desk, _folder.FullName, examples);
        _desk.StartNewProject();

        Assert.Equal(["1 First", "2 Second"], store.List().Examples);
        Assert.Equal(new ProjectInfo("2 Second", Path: null, Unsaved: false), store.OpenExample("2 Second"));
        Assert.Equal("How to use it", _desk.Notes);

        // Only an example of the list: a project of the projects folder, or a way out of the folder, is none.
        Assert.Equal("There is no example \"show\".", Assert.Throws<DeskException>(() => store.OpenExample("show")).Message);
        Assert.Throws<DeskException>(() => store.OpenExample("../examples/1 First"));
        Assert.Throws<DeskException>(() => _store.OpenExample("1 First"));   // a store without examples
    }

    // ---------------------------------------------------------------- the "unsaved" mark

    [Fact]
    public void ChangingAMemoryMarksTheProjectUnsaved()
    {
        Assert.False(_store.Current.Unsaved);

        _desk.SaveMemory(1);
        Assert.True(_store.Current.Unsaved);

        _store.Save("show");
        Assert.False(_store.Current.Unsaved);

        _desk.CopyMemory(1, 2);
        Assert.True(_store.Current.Unsaved);

        _store.Save(null);
        Assert.False(_store.Current.Unsaved);

        _desk.DeleteMemory(2);
        Assert.True(_store.Current.Unsaved);
    }

    [Fact]
    public void ChangingANoteMarksTheProjectUnsaved()
    {
        _desk.SetChannelNote(1, "Front left");
        Assert.True(_store.Current.Unsaved);

        _store.Save("show");
        Assert.False(_store.Current.Unsaved);

        _desk.SetMemoryNote(1, "Blue wash");
        Assert.True(_store.Current.Unsaved);

        _store.Save(null);
        Assert.False(_store.Current.Unsaved);

        _desk.SetChannelNote(1, "");   // taking a note away is a change too
        Assert.True(_store.Current.Unsaved);
    }

    [Fact]
    public void WritingTheSameNoteAgainDoesNotMarkTheProjectUnsaved()
    {
        _desk.SetChannelNote(1, "Front left");
        _desk.SetMemoryNote(1, "Blue wash");
        _store.Save("show");

        _desk.SetChannelNote(1, "Front left");
        _desk.SetChannelNote(1, " Front left ");   // the same once the spaces are off
        _desk.SetMemoryNote(1, "Blue wash");
        _desk.SetChannelNote(2, "");               // there was no note, and there is none

        Assert.False(_store.Current.Unsaved);
    }

    [Fact]
    public void BindingAnIndependentKeyMarksTheProjectUnsaved()
    {
        _desk.SetIndependent(1, 300);
        Assert.True(_store.Current.Unsaved);

        _store.Save("show");
        _desk.SetIndependent(1, 300);   // the channel it has already
        Assert.False(_store.Current.Unsaved);

        _desk.SetIndependent(1, 1);
        Assert.True(_store.Current.Unsaved);
    }

    [Fact]
    public void NewProjectMakesTheDeskCleanAndTheProjectUntitled()
    {
        StoreMemory(1, (Channel: 1, Level: 10));
        _desk.SetChannel(2, 20);
        _desk.SetMemoryLevel(1, 200);
        _desk.SetChannelNote(1, "Front left");
        _desk.SetIndependent(1, 300);
        _desk.SetMaster(100);
        _desk.SetBlackout(true);
        _store.Save("show");

        ProjectInfo project = _store.StartNew();

        Assert.Equal(new ProjectInfo("Untitled", Path: null, Unsaved: false), project);
        Assert.Equal(project, _store.Current);
        DeskState state = _desk.GetState();
        Assert.Equal(new int[512], state.Channels);
        Assert.Equal(new int[128], state.MemoryLevels);
        Assert.Equal(new bool[128], state.MemoryStored);
        Assert.All(state.ChannelNotes, note => Assert.Equal("", note));
        Assert.Equal(Desk.DefaultIndependents(), state.Independents);
        Assert.Equal(100, state.Master);   // master and blackout are not part of a project
        Assert.True(state.Blackout);
        Assert.True(File.Exists(PathOf("show")));   // the file of the old project is not touched
    }

    [Fact]
    public void NewProjectDropsWhatWasNotSaved()
    {
        _desk.SaveMemory(1);
        Assert.True(_store.Current.Unsaved);

        _store.StartNew();

        Assert.False(_store.Current.Unsaved);
        var error = Assert.Throws<DeskException>(() => _store.Save(null));
        Assert.Equal("This project has no file yet. Save it under a name.", error.Message);
    }

    [Fact]
    public void WhatIsNotInTheFileDoesNotMarkTheProjectUnsaved()
    {
        _desk.SaveMemory(1);
        _store.Save("show");

        _desk.SetChannel(1, 10);
        _desk.SetMemoryLevel(1, 10);
        _desk.SetMaster(10);
        _desk.SetBlackout(true);
        _desk.LoadMemory(1);
        _desk.Clear();
        _desk.DeleteMemory(50);   // empty already: nothing changes

        Assert.False(_store.Current.Unsaved);
    }

    [Fact]
    public void LoadClearsTheUnsavedMark()
    {
        _store.Save("show");
        _desk.SaveMemory(1);
        Assert.True(_store.Current.Unsaved);

        _store.Load("show");

        Assert.False(_store.Current.Unsaved);
    }

    [Fact]
    public void FailedSaveKeepsTheUnsavedMark()
    {
        _desk.SaveMemory(1);

        Assert.Throws<DeskException>(() => _store.Save("in/valid"));

        Assert.True(_store.Current.Unsaved);
    }

    [Fact]
    public void TakeChangeGivesTheProjectOnlyWhenItChanged()
    {
        Assert.Null(_store.TakeChange());

        _desk.SaveMemory(1);
        Assert.Equal(new ProjectInfo("Untitled", Path: null, Unsaved: true), _store.TakeChange());
        Assert.Null(_store.TakeChange());

        _desk.SaveMemory(2);   // unsaved already: nothing new to tell
        Assert.Null(_store.TakeChange());

        _store.Save("show");
        Assert.Equal(new ProjectInfo("show", PathOf("show"), Unsaved: false), _store.TakeChange());
        Assert.Null(_store.TakeChange());

        _store.Save(null);     // saved again without a change
        Assert.Null(_store.TakeChange());
    }

    // Stores a look in a memory of the test's desk and leaves the faders down again.
    private void StoreMemory(int memory, params (int Channel, int Level)[] look)
    {
        _desk.Clear();
        foreach ((int channel, int level) in look)
            _desk.SetChannel(channel, level);

        _desk.SaveMemory(memory);
        _desk.Clear();
    }

    private string PathOf(string name)
    {
        return Path.Combine(_folder.FullName, name + ".json");
    }

    // Loads a project of the test's folder the way another run of the server would.
    private Desk LoadIntoNewDesk(string name)
    {
        var desk = new Desk();
        new ProjectStore(desk, _folder.FullName).Load(name);
        return desk;
    }

    // A project file whose list of memories holds the given entries.
    private static string ProjectWith(string memories)
    {
        return $$"""{ "format": "lumenet-project", "version": 1, "memories": [ {{memories}} ] }""";
    }

    // A project file without memories whose two lists of notes are the given JSON.
    private static string ProjectWithNotes(string channelNotes, string memoryNotes)
    {
        return $$"""
            {
              "format": "lumenet-project",
              "version": 1,
              "memories": [],
              "channelNotes": {{channelNotes}},
              "memoryNotes": {{memoryNotes}}
            }
            """;
    }

    // A project file without memories and notes whose list of independents is the given JSON.
    private static string ProjectWithIndependents(string independents)
    {
        return $$"""{ "format": "lumenet-project", "version": 1, "memories": [], "independents": {{independents}} }""";
    }

    // Puts the text into a project file and checks that loading it fails with a message that
    // names the problem, and that neither the desk nor the open project has changed.
    private void AssertRefused(string expectedInMessage, string fileContent)
    {
        StoreMemory(1, (Channel: 1, Level: 10));
        _desk.SetMemoryLevel(1, 200);
        _desk.SetChannel(2, 20);
        _desk.SetChannelNote(1, "Front left");
        _desk.SetMemoryNote(1, "Blue wash");
        _desk.SetIndependent(10, 400);
        _desk.SetMemoryIndependent(10, 40);
        _store.Save("good");
        DeskState stateBefore = _desk.GetState();
        (ProjectContent contentBefore, long changesBefore) = _desk.GetProject();
        File.WriteAllText(PathOf("bad"), fileContent);

        var error = Assert.Throws<DeskException>(() => _store.Load("bad"));

        Assert.StartsWith("The project \"bad\" could not be loaded. ", error.Message);
        Assert.Contains(expectedInMessage, error.Message);

        DeskState stateAfter = _desk.GetState();
        (ProjectContent contentAfter, long changesAfter) = _desk.GetProject();
        Assert.Equal(stateBefore.Channels, stateAfter.Channels);
        Assert.Equal(stateBefore.MemoryLevels, stateAfter.MemoryLevels);
        Assert.Equal(stateBefore.MemoryStored, stateAfter.MemoryStored);
        Assert.Equal(stateBefore.ChannelNotes, stateAfter.ChannelNotes);
        Assert.Equal(stateBefore.MemoryNotes, stateAfter.MemoryNotes);
        Assert.Equal(stateBefore.Independents, stateAfter.Independents);
        Assert.Equal(stateBefore.MemoryIndependents, stateAfter.MemoryIndependents);
        Assert.Equal(changesBefore, changesAfter);
        Assert.Equal(contentBefore.Looks[0], contentAfter.Looks[0]);
        Assert.Equal(new ProjectInfo("good", PathOf("good"), Unsaved: false), _store.Current);
    }
}
