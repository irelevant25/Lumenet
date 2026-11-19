using System.Text;
using DmxBox.Server;

namespace DmxBox.Tests;

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
        StoreMemory(1, (Channel: 5, Level: 255), (Channel: 6, Level: 128));
        StoreMemory(3);   // a dark look: stored, but without levels
        _desk.SetChannelNote(5, "Front left");
        _desk.SetChannelNote(512, "Smoke machine");
        _desk.SetMemoryNote(3, "Blue wash");
        _desk.SetIndependent(2, 300);

        _store.Save("show");

        string expected = """
            {
              "format": "lumenet-project",
              "version": 1,
              "memories": [
                {
                  "number": 1,
                  "levels": {
                    "5": 255,
                    "6": 128
                  }
                },
                {
                  "number": 3,
                  "levels": {}
                }
              ],
              "channelNotes": {
                "5": "Front left",
                "512": "Smoke machine"
              },
              "memoryNotes": {
                "3": "Blue wash"
              },
              "independents": [
                1,
                300,
                3,
                4,
                5,
                6,
                7,
                8,
                9,
                10,
                11,
                12,
                13,
                14,
                15,
                16
              ]
            }
            """;
        Assert.Equal(expected.ReplaceLineEndings(), File.ReadAllText(PathOf("show")).ReplaceLineEndings());
    }

    [Fact]
    public void NewProjectHasEmptyListsAndTheSixteenFirstChannelsInItsFile()
    {
        _store.Save("show");

        string independents = string.Join(",\n    ", Enumerable.Range(1, 16));
        string expected = $$"""
            {
              "format": "lumenet-project",
              "version": 1,
              "memories": [],
              "channelNotes": {},
              "memoryNotes": {},
              "independents": [
                {{independents}}
              ]
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
        _desk.SetIndependent(16, 1);
        int[] saved = _desk.GetState().Independents;
        _store.Save("show");
        _desk.SetIndependent(1, 77);   // changed after the save: the load has to undo it

        _store.Load("show");

        Assert.Equal(saved, _desk.GetState().Independents);
        Assert.Equal(saved, LoadIntoNewDesk("show").GetState().Independents);
    }

    [Fact]
    public void FileWithoutIndependentsPutsTheKeysOnTheFirstSixteenChannels()
    {
        // A file from before there were independent keys.
        File.WriteAllText(PathOf("old"), ProjectWith("""{ "number": 4, "levels": { "9": 90 } }"""));
        _desk.SetIndependent(1, 300);

        _store.Load("old");

        Assert.Equal(Desk.DefaultIndependents(), _desk.GetState().Independents);
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
        AssertRefused("version 2", """{ "format": "lumenet-project", "version": 2, "memories": [] }""");
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
        Assert.True(Directory.Exists(folder));
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
        _desk.SetIndependent(16, 400);
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
        Assert.Equal(changesBefore, changesAfter);
        Assert.Equal(contentBefore.Looks[0], contentAfter.Looks[0]);
        Assert.Equal(new ProjectInfo("good", PathOf("good"), Unsaved: false), _store.Current);
    }
}
