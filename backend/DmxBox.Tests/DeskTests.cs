using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>
/// The fader desk on its own: the output it computes, the memory functions, the notes, and what
/// it refuses. No box, no files, no server.
/// </summary>
public sealed class DeskTests
{
    [Fact]
    public void NewDeskIsDarkWithTheMasterFull()
    {
        var desk = new Desk();

        DeskState state = desk.GetState();
        Assert.Equal(new int[512], state.Channels);
        Assert.Equal(new int[128], state.MemoryLevels);
        Assert.Equal(new bool[128], state.MemoryStored);
        Assert.Equal(255, state.Master);
        Assert.False(state.Blackout);
        Assert.Equal(new byte[512], desk.GetOutput());
    }

    [Fact]
    public void StateShowsEveryFader()
    {
        var desk = new Desk();
        desk.SetChannel(1, 11);
        desk.SetChannel(512, 12);
        desk.SetMemoryLevel(1, 21);
        desk.SetMemoryLevel(128, 22);
        desk.SetMaster(31);
        desk.SetBlackout(true);

        DeskState state = desk.GetState();
        Assert.Equal(11, state.Channels[0]);
        Assert.Equal(12, state.Channels[511]);
        Assert.Equal(21, state.MemoryLevels[0]);
        Assert.Equal(22, state.MemoryLevels[127]);
        Assert.Equal(31, state.Master);
        Assert.True(state.Blackout);
    }

    // ---------------------------------------------------------------- the output rule

    [Fact]
    public void ChannelFaderGoesToTheOutput()
    {
        var desk = new Desk();
        desk.SetChannel(1, 255);
        desk.SetChannel(2, 7);
        desk.SetChannel(512, 128);

        byte[] output = desk.GetOutput();
        Assert.Equal(512, output.Length);
        Assert.Equal([255, 7, 0], output[..3]);
        Assert.Equal(128, output[511]);
    }

    [Fact]
    public void HigherOfChannelAndMemoryWins()
    {
        var desk = new Desk();
        Store(desk, memory: 1, (Channel: 1, Level: 200), (Channel: 2, Level: 50));
        desk.SetMemoryLevel(1, 255);
        desk.SetChannel(1, 100);   // below the memory's 200
        desk.SetChannel(2, 120);   // above the memory's 50
        desk.SetChannel(3, 30);    // not in the memory

        Assert.Equal([200, 120, 30], desk.GetOutput()[..3]);
    }

    [Fact]
    public void HighestOfTwoMemoriesWins()
    {
        var desk = new Desk();
        Store(desk, memory: 1, (Channel: 1, Level: 200), (Channel: 2, Level: 100));
        Store(desk, memory: 128, (Channel: 1, Level: 100), (Channel: 2, Level: 200), (Channel: 3, Level: 50));
        desk.SetMemoryLevel(1, 255);
        desk.SetMemoryLevel(128, 255);

        Assert.Equal([200, 200, 50], desk.GetOutput()[..3]);
    }

    [Fact]
    public void MemoryFaderScalesItsLook()
    {
        var desk = new Desk();
        Store(desk, memory: 5, (Channel: 1, Level: 255), (Channel: 2, Level: 100));
        desk.SetMemoryLevel(5, 128);
        desk.SetChannel(2, 60);   // the memory gives 100 x 128 / 255 = 50.2: the channel fader is higher

        Assert.Equal([128, 60], desk.GetOutput()[..2]);
    }

    [Fact]
    public void MemoryWithItsFaderDownAddsNothing()
    {
        var desk = new Desk();
        Store(desk, memory: 1, (Channel: 1, Level: 255));
        desk.SetChannel(1, 10);

        Assert.Equal(10, desk.GetOutput()[0]);
    }

    [Fact]
    public void EmptyMemoryAddsNothing()
    {
        var desk = new Desk();
        desk.SetChannel(1, 10);
        desk.SetMemoryLevel(7, 255);

        Assert.Equal(10, desk.GetOutput()[0]);
    }

    [Fact]
    public void MasterScalesChannelsAndMemories()
    {
        var desk = new Desk();
        Store(desk, memory: 1, (Channel: 2, Level: 200));
        desk.SetMemoryLevel(1, 255);
        desk.SetChannel(1, 100);
        desk.SetMaster(128);

        Assert.Equal([50, 100], desk.GetOutput()[..2]);   // 100 becomes 50.2, 200 becomes 100.4

        desk.SetMaster(0);
        Assert.Equal([0, 0], desk.GetOutput()[..2]);
    }

    [Fact]
    public void ScalingRoundsToTheNearestLevel()
    {
        var desk = new Desk();
        desk.SetChannel(1, 1);
        desk.SetChannel(2, 3);

        desk.SetMaster(127);   // 1 x 127 / 255 = 0.498 and 3 x 127 / 255 = 1.494: both round down
        Assert.Equal([0, 1], desk.GetOutput()[..2]);

        desk.SetMaster(128);   // 1 x 128 / 255 = 0.502 and 3 x 128 / 255 = 1.506: both round up
        Assert.Equal([1, 2], desk.GetOutput()[..2]);
    }

    [Fact]
    public void MemoryFaderRoundsToTheNearestLevelToo()
    {
        var desk = new Desk();
        Store(desk, memory: 1, (Channel: 1, Level: 3));

        desk.SetMemoryLevel(1, 127);
        Assert.Equal(1, desk.GetOutput()[0]);

        desk.SetMemoryLevel(1, 128);
        Assert.Equal(2, desk.GetOutput()[0]);
    }

    [Fact]
    public void MemoryFaderAndMasterEachRoundOnTheirOwn()
    {
        // The memory fader makes 36 x 17 / 255 = 2.4, which is level 2. The master then makes
        // 2 x 160 / 255 = 1.25, which is level 1. Rounding only once, at the very end, would
        // give 2.4 x 160 / 255 = 1.506 and so level 2.
        var desk = new Desk();
        Store(desk, memory: 1, (Channel: 1, Level: 36));
        desk.SetMemoryLevel(1, 17);
        desk.SetMaster(160);

        Assert.Equal(1, desk.GetOutput()[0]);
    }

    [Fact]
    public void BlackoutDarkensTheOutputAndLeavesTheFaders()
    {
        var desk = new Desk();
        desk.SetChannel(1, 200);

        desk.SetBlackout(true);

        Assert.True(desk.Blackout);
        Assert.Equal(0, desk.GetOutput()[0]);
        Assert.Equal(200, desk.GetState().Channels[0]);

        desk.SetBlackout(false);
        Assert.Equal(200, desk.GetOutput()[0]);
    }

    [Fact]
    public void OutputCounterMovesWithEveryChangeThatCanAlterTheOutput()
    {
        var desk = new Desk();
        desk.SetChannel(1, 10);
        desk.SaveMemory(1);

        AssertMovesOutputCounter(desk, () => desk.SetChannel(2, 20));
        AssertMovesOutputCounter(desk, () => desk.SetMemoryLevel(1, 255));
        AssertMovesOutputCounter(desk, () => desk.SetMaster(200));
        AssertMovesOutputCounter(desk, () => desk.SaveMemory(2));
        AssertMovesOutputCounter(desk, () => desk.LoadMemory(1));
        AssertMovesOutputCounter(desk, () => desk.CopyMemory(1, 3));
        AssertMovesOutputCounter(desk, () => desk.DeleteMemory(3));
        AssertMovesOutputCounter(desk, () => desk.Clear());
        AssertMovesOutputCounter(desk, () => desk.ReplaceProject(EmptyProject()));
        AssertMovesOutputCounter(desk, () => desk.SetBlackout(true));

        long beforeNote = desk.OutputChanges;
        desk.SetChannelNote(1, "Front left");
        desk.SetMemoryNote(1, "Blue wash");
        desk.SetBlackout(true);   // it is on already
        Assert.Equal(beforeNote, desk.OutputChanges);
    }

    // ---------------------------------------------------------------- numbers out of range

    [Fact]
    public void NumbersOutOfRangeAreRejectedWithTheRangeInTheMessage()
    {
        var desk = new Desk();

        AssertOutOfRange("from 1 to 512", () => desk.SetChannel(0, 0));
        AssertOutOfRange("from 1 to 512", () => desk.SetChannel(513, 0));
        AssertOutOfRange("from 0 to 255", () => desk.SetChannel(1, -1));
        AssertOutOfRange("from 0 to 255", () => desk.SetChannel(1, 256));

        AssertOutOfRange("from 1 to 128", () => desk.SetMemoryLevel(0, 0));
        AssertOutOfRange("from 1 to 128", () => desk.SetMemoryLevel(129, 0));
        AssertOutOfRange("from 0 to 255", () => desk.SetMemoryLevel(1, -1));
        AssertOutOfRange("from 0 to 255", () => desk.SetMemoryLevel(1, 256));

        AssertOutOfRange("from 0 to 255", () => desk.SetMaster(-1));
        AssertOutOfRange("from 0 to 255", () => desk.SetMaster(256));

        AssertOutOfRange("from 1 to 128", () => desk.SaveMemory(0));
        AssertOutOfRange("from 1 to 128", () => desk.SaveMemory(129));
        AssertOutOfRange("from 1 to 128", () => desk.LoadMemory(0));
        AssertOutOfRange("from 1 to 128", () => desk.LoadMemory(129));
        AssertOutOfRange("from 1 to 128", () => desk.CopyMemory(0, 1));
        AssertOutOfRange("from 1 to 128", () => desk.CopyMemory(1, 129));
        AssertOutOfRange("from 1 to 128", () => desk.DeleteMemory(0));
        AssertOutOfRange("from 1 to 128", () => desk.DeleteMemory(129));
    }

    [Fact]
    public void MessageForANumberOutOfRangeIsPlain()
    {
        var desk = new Desk();

        var error = Assert.Throws<ArgumentOutOfRangeException>(() => desk.SetChannel(600, 0));

        // Exactly this text goes to the UI: no "(Parameter 'channel')" that .NET likes to append.
        Assert.Equal("The channel must be from 1 to 512, not 600.", error.Message);
    }

    [Fact]
    public void RejectedNumberChangesNothing()
    {
        var desk = new Desk();
        desk.SetChannel(1, 10);
        long outputChanges = desk.OutputChanges;

        Assert.Throws<ArgumentOutOfRangeException>(() => desk.SetChannel(1, 256));

        Assert.Equal(10, desk.GetState().Channels[0]);
        Assert.Equal(outputChanges, desk.OutputChanges);
    }

    // ---------------------------------------------------------------- memories

    [Fact]
    public void SaveMemoryStoresACopyOfTheChannelFaders()
    {
        var desk = new Desk();
        desk.SetChannel(1, 10);
        desk.SetChannel(512, 20);

        desk.SaveMemory(3);
        desk.SetChannel(1, 99);   // moving a fader afterwards must not reach into the memory

        Assert.True(desk.GetState().MemoryStored[2]);
        byte[] look = Looks(desk)[2]!;
        Assert.Equal(10, look[0]);
        Assert.Equal(20, look[511]);
    }

    [Fact]
    public void SaveMemoryTakesTheFadersNotTheOutput()
    {
        // Channel 1 shines at 200 because of memory 1, but its own fader is at 5.
        var desk = new Desk();
        Store(desk, memory: 1, (Channel: 1, Level: 200));
        desk.SetMemoryLevel(1, 255);
        desk.SetChannel(1, 5);
        desk.SetMaster(100);

        desk.SaveMemory(2);

        Assert.Equal(5, Looks(desk)[1]![0]);
    }

    [Fact]
    public void SavedDarkLookCountsAsStored()
    {
        var desk = new Desk();

        desk.SaveMemory(1);

        Assert.True(desk.GetState().MemoryStored[0]);
        Assert.Equal(new byte[512], Looks(desk)[0]);
    }

    [Fact]
    public void SaveMemoryReplacesWhatTheMemoryHeld()
    {
        var desk = new Desk();
        Store(desk, memory: 1, (Channel: 1, Level: 10), (Channel: 2, Level: 20));
        desk.SetChannel(2, 99);

        desk.SaveMemory(1);

        Assert.Equal([0, 99], Looks(desk)[0]![..2]);
    }

    [Fact]
    public void LoadMemorySetsTheChannelFadersToTheLook()
    {
        var desk = new Desk();
        Store(desk, memory: 4, (Channel: 1, Level: 10), (Channel: 2, Level: 20));
        desk.SetChannel(2, 99);
        desk.SetChannel(3, 99);   // not in the look: goes down to the look's 0

        desk.LoadMemory(4);

        Assert.Equal([10, 20, 0], desk.GetState().Channels[..3]);
        Assert.Equal([10, 20, 0], desk.GetOutput()[..3]);
    }

    [Fact]
    public void LoadingAnEmptyMemoryFailsAndLeavesTheFaders()
    {
        var desk = new Desk();
        desk.SetChannel(1, 10);

        var error = Assert.Throws<DeskException>(() => desk.LoadMemory(4));

        Assert.Equal("Memory 4 is empty.", error.Message);
        Assert.Equal(10, desk.GetState().Channels[0]);
    }

    [Fact]
    public void CopyMemoryGivesTheOtherMemoryACopyOfItsOwn()
    {
        var desk = new Desk();
        Store(desk, memory: 1, (Channel: 1, Level: 10));
        Store(desk, memory: 2, (Channel: 2, Level: 20));

        desk.CopyMemory(1, 2);
        desk.SetChannel(1, 99);
        desk.SaveMemory(1);       // changing the original afterwards must not change the copy

        Assert.Equal([10, 0], Looks(desk)[1]![..2]);
        Assert.Equal(99, Looks(desk)[0]![0]);
    }

    [Fact]
    public void CopyingAnEmptyMemoryFailsAndLeavesTheOtherMemory()
    {
        var desk = new Desk();
        Store(desk, memory: 2, (Channel: 1, Level: 10));

        var error = Assert.Throws<DeskException>(() => desk.CopyMemory(1, 2));

        Assert.Equal("Memory 1 is empty.", error.Message);
        Assert.Equal(10, Looks(desk)[1]![0]);
    }

    [Fact]
    public void CopyingAMemoryOntoItselfFails()
    {
        var desk = new Desk();
        Store(desk, memory: 1, (Channel: 1, Level: 10));

        var error = Assert.Throws<DeskException>(() => desk.CopyMemory(1, 1));

        Assert.Equal("Memory 1 cannot be copied onto itself.", error.Message);
    }

    [Fact]
    public void DeleteMemoryEmptiesIt()
    {
        var desk = new Desk();
        Store(desk, memory: 1, (Channel: 1, Level: 200));
        desk.SetMemoryLevel(1, 255);

        desk.DeleteMemory(1);

        Assert.False(desk.GetState().MemoryStored[0]);
        Assert.Null(Looks(desk)[0]);
        Assert.Equal(0, desk.GetOutput()[0]);
        Assert.Equal(255, desk.GetState().MemoryLevels[0]);   // the fader stays where it is
    }

    [Fact]
    public void TheTableOfTheMemoriesViewChangesOneLevelOfALook()
    {
        var desk = new Desk(new ManualTime());
        Store(desk, memory: 1, (Channel: 1, Level: 200));
        long changes = desk.ProjectChanges;

        Assert.True(desk.SetMemoryChannel(1, 2, 90));
        Assert.False(desk.SetMemoryChannel(1, 2, 90));   // the level it has already: no change
        Assert.Equal(changes + 1, desk.ProjectChanges);
        Assert.Equal((200, 90), (Looks(desk)[0]![0], Looks(desk)[0]![1]));

        // An empty memory gets a look with only that channel; a 0 leaves it empty.
        Assert.False(desk.SetMemoryChannel(5, 3, 0));
        Assert.False(desk.GetState().MemoryStored[4]);
        Assert.True(desk.SetMemoryChannel(5, 3, 40));
        Assert.True(desk.GetState().MemoryStored[4]);
        Assert.Equal(40, Looks(desk)[4]![2]);
        Assert.Equal(1, Looks(desk)[4]!.Count(level => level > 0));

        // A sequence is changed on the Sequences page, and the stack being live locks every memory.
        desk.SetSequence(7, loop: true, [new Step("channel", 1, 255, 1, 0, 1)]);
        Assert.Equal(
            "Memory 7 holds a sequence. Change it on the Sequences page.",
            Assert.Throws<DeskException>(() => desk.SetMemoryChannel(7, 1, 10)).Message);
        desk.AddStackStep(new Step("channel", 1, 255, 0, 0, 0));
        desk.SetStackLive(true);
        Assert.StartsWith("The stack is live", Assert.Throws<DeskException>(() => desk.SetMemoryChannel(1, 2, 10)).Message);
        AssertOutOfRange("from 1 to 512", () => desk.SetMemoryChannel(1, 513, 10));
        AssertOutOfRange("from 0 to 255", () => desk.SetMemoryChannel(1, 1, 256));
    }

    [Fact]
    public void DeletingAnEmptyMemoryIsNotAnError()
    {
        var desk = new Desk();
        long projectChanges = desk.ProjectChanges;

        desk.DeleteMemory(1);

        Assert.False(desk.GetState().MemoryStored[0]);
        Assert.Equal(projectChanges, desk.ProjectChanges);   // nothing was there to change
    }

    [Fact]
    public void ClearBringsAllFadersDownAndKeepsTheRest()
    {
        var desk = new Desk();
        Store(desk, memory: 1, (Channel: 1, Level: 200));
        desk.SetChannel(2, 50);
        desk.SetMemoryLevel(1, 255);
        desk.SetMemoryLevel(9, 40);
        desk.SetMaster(100);
        desk.SetBlackout(true);

        desk.Clear();

        DeskState state = desk.GetState();
        Assert.Equal(new int[512], state.Channels);
        Assert.Equal(new int[128], state.MemoryLevels);
        Assert.True(state.MemoryStored[0]);
        Assert.Equal(200, Looks(desk)[0]![0]);
        Assert.Equal(100, state.Master);
        Assert.True(state.Blackout);
        Assert.Equal(new byte[512], desk.GetOutput());
    }

    [Fact]
    public void ReplaceProjectSwapsAllLooksAndNotesAndBringsTheMemoryFadersDown()
    {
        var desk = new Desk();
        Store(desk, memory: 1, (Channel: 1, Level: 200));
        desk.SetChannelNote(1, "old note");
        desk.SetMemoryNote(128, "old note");
        desk.SetIndependent(1, 300);
        desk.SetChannel(5, 55);
        desk.SetMemoryLevel(1, 255);
        desk.SetMemoryLevel(2, 255);

        ProjectContent project = EmptyProject();
        project.Looks[1] = new byte[512];
        project.Looks[1]![9] = 77;
        project.ChannelNotes[511] = "Back right";
        project.MemoryNotes[0] = "Blue wash";
        project.Independents[9] = 400;
        project.MemoryIndependents[9] = 99;
        long changes = desk.ReplaceProject(project);
        project.Looks[1]![9] = 1;   // the desk has copies of its own
        project.ChannelNotes[511] = "changed afterwards";
        project.Independents[9] = 1;
        project.MemoryIndependents[9] = 1;

        DeskState state = desk.GetState();
        Assert.False(state.MemoryStored[0]);
        Assert.True(state.MemoryStored[1]);
        Assert.Equal(77, Looks(desk)[1]![9]);
        Assert.Equal("", state.ChannelNotes[0]);          // the old notes are gone
        Assert.Equal("Back right", state.ChannelNotes[511]);
        Assert.Equal("Blue wash", state.MemoryNotes[0]);
        Assert.Equal("", state.MemoryNotes[127]);
        Assert.Equal(1, state.Independents[0]);           // and so is the old binding
        Assert.Equal(400, state.Independents[9]);
        Assert.Equal(99, state.MemoryIndependents[9]);
        Assert.Equal(new int[128], state.MemoryLevels);
        Assert.Equal(55, state.Channels[4]);              // the channel faders stay
        Assert.Equal(changes, desk.ProjectChanges);
    }

    [Fact]
    public void ReplaceProjectWantsAnEntryForEveryMemoryAndEveryFader()
    {
        var desk = new Desk();
        ProjectContent whole = EmptyProject();
        byte[]?[] shortLook = new byte[]?[128];
        shortLook[0] = new byte[511];

        Assert.Throws<ArgumentException>(() => desk.ReplaceProject(whole with { Looks = new byte[]?[127] }));
        Assert.Throws<ArgumentException>(() => desk.ReplaceProject(whole with { Looks = shortLook }));
        Assert.Throws<ArgumentException>(() => desk.ReplaceProject(whole with { ChannelNotes = NoNotes(511) }));
        Assert.Throws<ArgumentException>(() => desk.ReplaceProject(whole with { MemoryNotes = NoNotes(129) }));
        Assert.Throws<ArgumentException>(() => desk.ReplaceProject(whole with { Independents = new int[16] }));
        Assert.Throws<ArgumentException>(() => desk.ReplaceProject(whole with { MemoryIndependents = new int[9] }));
    }

    [Fact]
    public void ProjectComesOutAsCopies()
    {
        var desk = new Desk();
        Store(desk, memory: 1, (Channel: 1, Level: 10));
        desk.SetChannelNote(1, "Front left");

        ProjectContent project = desk.GetProject().Content;
        project.Looks[0]![0] = 99;
        project.ChannelNotes[0] = "changed outside";
        desk.GetState().ChannelNotes[0] = "changed outside";

        Assert.Equal(10, Looks(desk)[0]![0]);
        Assert.Equal("Front left", desk.GetProject().Content.ChannelNotes[0]);
        Assert.Equal("Front left", desk.GetState().ChannelNotes[0]);
    }

    [Fact]
    public void ProjectCounterMovesOnlyWhenALookOrANoteChanges()
    {
        var desk = new Desk();

        AssertMovesProjectCounter(desk, () => desk.SaveMemory(1));
        AssertMovesProjectCounter(desk, () => desk.CopyMemory(1, 2));
        AssertMovesProjectCounter(desk, () => desk.DeleteMemory(2));
        AssertMovesProjectCounter(desk, () => desk.ReplaceProject(desk.GetProject().Content));
        AssertMovesProjectCounter(desk, () => desk.SetChannelNote(1, "Front left"));
        AssertMovesProjectCounter(desk, () => desk.SetMemoryNote(1, "Blue wash"));
        AssertMovesProjectCounter(desk, () => desk.SetChannelNote(1, ""));

        long before = desk.ProjectChanges;
        desk.SetChannel(1, 10);
        desk.SetMemoryLevel(1, 10);
        desk.SetMaster(10);
        desk.SetBlackout(true);
        desk.LoadMemory(1);
        desk.Clear();
        desk.SetMemoryNote(1, "Blue wash");      // the note that is there already
        desk.SetMemoryNote(1, "  Blue wash  ");  // the same again after trimming
        desk.SetChannelNote(1, "");              // no note before, no note after
        Assert.Equal(before, desk.ProjectChanges);
        Assert.Equal(before, desk.GetProject().Changes);
    }

    // ---------------------------------------------------------------- notes

    [Fact]
    public void NewDeskHasNoNotes()
    {
        var desk = new Desk();

        DeskState state = desk.GetState();
        Assert.Equal(NoNotes(512), state.ChannelNotes);
        Assert.Equal(NoNotes(128), state.MemoryNotes);
    }

    [Fact]
    public void NoteGoesUnderItsFader()
    {
        var desk = new Desk();

        desk.SetChannelNote(1, "Front left");
        desk.SetChannelNote(512, "Smoke machine");
        desk.SetMemoryNote(1, "Blue wash");
        desk.SetMemoryNote(128, "Finale");

        DeskState state = desk.GetState();
        Assert.Equal("Front left", state.ChannelNotes[0]);
        Assert.Equal("Smoke machine", state.ChannelNotes[511]);
        Assert.Equal("", state.ChannelNotes[1]);
        Assert.Equal("Blue wash", state.MemoryNotes[0]);
        Assert.Equal("Finale", state.MemoryNotes[127]);
        Assert.Equal("", state.MemoryNotes[1]);
    }

    [Fact]
    public void NoteIsStoredWithoutTheSpacesAtItsEnds()
    {
        var desk = new Desk();

        string channelNote = desk.SetChannelNote(5, "  Front left ");
        string memoryNote = desk.SetMemoryNote(3, "\tBlue wash\r\n");   // pasted with a line break at the end

        Assert.Equal("Front left", channelNote);
        Assert.Equal("Blue wash", memoryNote);
        Assert.Equal("Front left", desk.GetState().ChannelNotes[4]);
        Assert.Equal("Blue wash", desk.GetState().MemoryNotes[2]);
    }

    [Fact]
    public void NoteWithAnEmptyFirstLineKeepsItsLineSeparator()
    {
        var desk = new Desk();

        // The UI writes two lines with U+2028 between them; "only the second line" starts with it.
        Assert.Equal("\u2028PAR", desk.SetChannelNote(1, "\u2028PAR"));
        Assert.Equal("\u2028PAR", desk.SetChannelNote(2, "  \u2028PAR  "));
        Assert.Equal("Front\u2028left", desk.SetChannelNote(3, "Front\u2028left"));

        // At the end it says nothing, and alone it is no note at all.
        Assert.Equal("Front", desk.SetChannelNote(4, "Front\u2028"));
        Assert.Equal("", desk.SetChannelNote(5, "\u2028"));
        Assert.Equal("\u2028PAR", desk.GetState().ChannelNotes[0]);
    }

    [Fact]
    public void MemoryLooksListsWhatEveryStoredMemoryHolds()
    {
        var desk = new Desk();
        Assert.Empty(desk.GetMemoryLooks());

        desk.SetChannel(1, 10);
        desk.SetChannel(512, 255);
        desk.SaveMemory(3);
        desk.SetChannel(1, 99);
        desk.SaveMemory(128);
        desk.DeleteMemory(128);

        Dictionary<int, int[]> looks = desk.GetMemoryLooks();
        Assert.Equal([3], looks.Keys);
        Assert.Equal(512, looks[3].Length);
        Assert.Equal(10, looks[3][0]);
        Assert.Equal(0, looks[3][1]);
        Assert.Equal(255, looks[3][511]);

        looks[3][0] = 77;                                  // a copy: changing it changes nothing on the desk
        Assert.Equal(10, desk.GetMemoryLooks()[3][0]);
    }

    [Fact]
    public void EmptyTextTakesTheNoteAway()
    {
        var desk = new Desk();
        desk.SetChannelNote(1, "Front left");
        desk.SetChannelNote(2, "Front right");
        desk.SetMemoryNote(1, "Blue wash");

        Assert.Equal("", desk.SetChannelNote(1, ""));
        Assert.Equal("", desk.SetChannelNote(2, "   "));
        Assert.Equal("", desk.SetMemoryNote(1, null));

        Assert.Equal(NoNotes(512), desk.GetState().ChannelNotes);
        Assert.Equal(NoNotes(128), desk.GetState().MemoryNotes);
    }

    [Fact]
    public void NoteCanHave24CharactersAndNoMore()
    {
        var desk = new Desk();
        string longest = new string('a', 24);

        Assert.Equal(longest, desk.SetChannelNote(1, longest));
        Assert.Equal(longest, desk.SetMemoryNote(1, $"   {longest}   "));   // the spaces at the ends do not count

        var channelError = Assert.Throws<DeskException>(() => desk.SetChannelNote(2, longest + "a"));
        var memoryError = Assert.Throws<DeskException>(() => desk.SetMemoryNote(2, longest + "a"));
        Assert.Equal("A note can have at most 24 characters.", channelError.Message);
        Assert.Equal("A note can have at most 24 characters.", memoryError.Message);
    }

    [Fact]
    public void ControlCharactersInANoteAreRejected()
    {
        var desk = new Desk();
        char[] controlCharacters = ['\t', '\n', '\r', '\0', (char)0x1F, (char)0x7F];

        foreach (char character in controlCharacters)
        {
            var error = Assert.Throws<DeskException>(() => desk.SetChannelNote(1, $"Front{character}left"));
            Assert.Equal("A note cannot contain a tab, a line break or the like.", error.Message);
            Assert.Throws<DeskException>(() => desk.SetMemoryNote(1, $"Blue{character}wash"));
        }

        Assert.Throws<DeskException>(() => desk.SetChannelNote(1, $"{(char)0x7F}Front left"));   // not a space: it is not trimmed away
    }

    [Fact]
    public void LettersBeyondAsciiAreFineInANote()
    {
        var desk = new Desk();
        string note = "Sv" + (char)0x11B + "tlo vpravo";   // Czech for "light on the right", with an e-caron

        Assert.Equal(note, desk.SetChannelNote(1, note));
        Assert.Equal(note, desk.GetState().ChannelNotes[0]);
    }

    [Fact]
    public void NoteForAFaderThatDoesNotExistIsRejected()
    {
        var desk = new Desk();

        AssertOutOfRange("from 1 to 512", () => desk.SetChannelNote(0, "note"));
        AssertOutOfRange("from 1 to 512", () => desk.SetChannelNote(513, "note"));
        AssertOutOfRange("from 1 to 128", () => desk.SetMemoryNote(0, "note"));
        AssertOutOfRange("from 1 to 128", () => desk.SetMemoryNote(129, "note"));
    }

    [Fact]
    public void RejectedNoteLeavesTheOldOne()
    {
        var desk = new Desk();
        desk.SetChannelNote(1, "Front left");
        long projectChanges = desk.ProjectChanges;

        Assert.Throws<DeskException>(() => desk.SetChannelNote(1, new string('a', 25)));
        Assert.Throws<DeskException>(() => desk.SetChannelNote(1, "Front\tleft"));

        Assert.Equal("Front left", desk.GetState().ChannelNotes[0]);
        Assert.Equal(projectChanges, desk.ProjectChanges);
    }

    [Fact]
    public void ClearAndTheMemoryFunctionsLeaveTheNotes()
    {
        var desk = new Desk();
        desk.SetChannelNote(1, "Front left");
        desk.SetMemoryNote(1, "Blue wash");
        desk.SetMemoryNote(2, "Red wash");

        desk.SetChannel(1, 200);
        desk.SaveMemory(1);
        desk.LoadMemory(1);
        desk.CopyMemory(1, 2);
        desk.DeleteMemory(1);
        desk.Clear();

        DeskState state = desk.GetState();
        Assert.Equal("Front left", state.ChannelNotes[0]);
        Assert.Equal("Blue wash", state.MemoryNotes[0]);   // the memory is empty now, its label stays
        Assert.Equal("Red wash", state.MemoryNotes[1]);    // the copy brought no note along
    }

    // ---------------------------------------------------------------- independent keys

    [Fact]
    public void IndependentKeysStartOnTheFirst32ChannelsAndTheFirst32Memories()
    {
        var desk = new Desk();

        Assert.Equal(Enumerable.Range(1, 32), desk.GetState().Independents);
        Assert.Equal(Enumerable.Range(1, 32), desk.GetState().MemoryIndependents);
    }

    [Fact]
    public void IndependentKeyCanBeBoundToAnyChannel()
    {
        var desk = new Desk();

        desk.SetIndependent(1, 512);
        desk.SetIndependent(10, 1);
        desk.SetIndependent(8, 10);   // two keys on one channel are no mistake

        int[] independents = desk.GetState().Independents;
        Assert.Equal(512, independents[0]);
        Assert.Equal(10, independents[7]);
        Assert.Equal(1, independents[9]);
        Assert.Equal(2, independents[1]);   // the others stay
    }

    [Fact]
    public void MemoryKeyOfTheIndependentsCanBeBoundToAnyMemory()
    {
        var desk = new Desk();

        desk.SetMemoryIndependent(1, 128);
        desk.SetMemoryIndependent(10, 1);

        int[] keys = desk.GetState().MemoryIndependents;
        Assert.Equal(128, keys[0]);
        Assert.Equal(1, keys[9]);
        Assert.Equal(2, keys[1]);
        Assert.Equal(Desk.DefaultIndependents(), desk.GetState().Independents);   // the channel keys are others
        Assert.Equal(keys, desk.GetProject().Content.MemoryIndependents);
    }

    [Fact]
    public void IndependentOutOfRangeIsRejectedWithAPlainMessage()
    {
        var desk = new Desk();

        var noSuchKey = Assert.Throws<ArgumentOutOfRangeException>(() => desk.SetIndependent(33, 1));
        Assert.Equal("The independent must be from 1 to 32, not 33.", noSuchKey.Message);
        AssertOutOfRange("from 1 to 32", () => desk.SetIndependent(0, 1));

        var noSuchChannel = Assert.Throws<ArgumentOutOfRangeException>(() => desk.SetIndependent(1, 513));
        Assert.Equal("The channel must be from 1 to 512, not 513.", noSuchChannel.Message);
        AssertOutOfRange("from 1 to 512", () => desk.SetIndependent(1, 0));

        var noSuchMemoryKey = Assert.Throws<ArgumentOutOfRangeException>(() => desk.SetMemoryIndependent(33, 1));
        Assert.Equal("The memory independent must be from 1 to 32, not 33.", noSuchMemoryKey.Message);
        var noSuchMemory = Assert.Throws<ArgumentOutOfRangeException>(() => desk.SetMemoryIndependent(1, 129));
        Assert.Equal("The memory must be from 1 to 128, not 129.", noSuchMemory.Message);

        Assert.Equal(Desk.DefaultIndependents(), desk.GetState().Independents);
        Assert.Equal(Desk.DefaultMemoryIndependents(), desk.GetState().MemoryIndependents);
    }

    [Fact]
    public void BindingAnIndependentKeyChangesTheProjectButNotTheOutput()
    {
        var desk = new Desk();
        long outputChanges = desk.OutputChanges;

        AssertMovesProjectCounter(desk, () => desk.SetIndependent(1, 200));
        AssertMovesProjectCounter(desk, () => desk.SetMemoryIndependent(1, 50));

        long projectChanges = desk.ProjectChanges;
        desk.SetIndependent(1, 200);         // the channel it has already
        desk.SetIndependent(2, 2);           // where it was from the start
        desk.SetMemoryIndependent(1, 50);    // the memory it has already
        Assert.Equal(projectChanges, desk.ProjectChanges);
        Assert.Equal(outputChanges, desk.OutputChanges);
    }

    [Fact]
    public void ClearAndTheMemoryFunctionsLeaveTheIndependentKeys()
    {
        var desk = new Desk();
        desk.SetIndependent(3, 300);

        desk.SetChannel(300, 255);
        desk.SaveMemory(1);
        desk.LoadMemory(1);
        desk.CopyMemory(1, 2);
        desk.DeleteMemory(1);
        desk.Clear();

        Assert.Equal(300, desk.GetState().Independents[2]);
        Assert.Equal(0, desk.GetState().Channels[299]);   // the level went down, the binding stayed
    }

    // ---------------------------------------------------------------- a new project

    [Fact]
    public void NewProjectMakesTheDeskCleanAndKeepsMasterAndBlackout()
    {
        var desk = new Desk();
        Store(desk, memory: 1, (Channel: 1, Level: 200));
        desk.SetChannel(2, 50);
        desk.SetMemoryLevel(1, 255);
        desk.SetChannelNote(1, "Front left");
        desk.SetMemoryNote(1, "Blue wash");
        desk.SetIndependent(1, 300);
        desk.SetMemoryIndependent(1, 30);
        desk.SetMaster(100);
        desk.SetBlackout(true);
        long outputChanges = desk.OutputChanges;

        long changes = desk.StartNewProject();

        DeskState state = desk.GetState();
        Assert.Equal(new int[512], state.Channels);
        Assert.Equal(new int[128], state.MemoryLevels);
        Assert.Equal(new bool[128], state.MemoryStored);
        Assert.Equal(NoNotes(512), state.ChannelNotes);
        Assert.Equal(NoNotes(128), state.MemoryNotes);
        Assert.Equal(Desk.DefaultIndependents(), state.Independents);
        Assert.Equal(Desk.DefaultMemoryIndependents(), state.MemoryIndependents);
        Assert.Equal(100, state.Master);
        Assert.True(state.Blackout);
        Assert.Equal(new byte[512], desk.GetOutput());
        Assert.True(desk.OutputChanges > outputChanges);   // the box has to get the dark stage
        Assert.Equal(changes, desk.ProjectChanges);
    }

    // ---------------------------------------------------------------- universes

    [Fact]
    public void MoreUniversesGiveMoreChannelsAndTheyReachTheOutput()
    {
        var desk = new Desk();
        Assert.Equal(1, desk.Universes);
        Assert.Equal(512, desk.GetOutput().Length);

        desk.SetUniverses(3);
        desk.SetChannel(1536, 200);

        Assert.Equal(1536, desk.ChannelCount);
        Assert.Equal(3, desk.GetState().Universes);
        Assert.Equal(1536, desk.GetState().Channels.Length);
        Assert.Equal(1536, desk.GetState().ChannelNotes.Length);
        Assert.Equal(200, desk.GetOutput()[1535]);
        AssertOutOfRange("from 1 to 1536", () => desk.SetChannel(1537, 1));
        AssertOutOfRange("The number of universes must be from 1 to 8, not 9.", () => desk.SetUniverses(9));
        AssertOutOfRange("The number of universes must be from 1 to 8, not 0.", () => desk.SetUniverses(0));
    }

    [Fact]
    public void FewerUniversesDropTheLevelsAndNotesAboveThemAndUndoBringsThemBack()
    {
        var desk = new Desk();
        var history = new UndoHistory(desk);
        desk.SetUniverses(2);
        Store(desk, 1, (Channel: 1, Level: 10), (Channel: 600, Level: 20));
        desk.SetChannel(600, 30);
        desk.SetChannelNote(600, "Hazer");

        using (history.Change("the universes"))
            desk.SetUniverses(1);

        Assert.Equal(512, desk.GetOutput().Length);
        Assert.Equal(512, Looks(desk)[0]!.Length);
        Assert.Equal(10, Looks(desk)[0]![0]);

        Assert.Equal("the universes", history.Undo());
        Assert.Equal(2, desk.Universes);
        Assert.Equal(20, Looks(desk)[0]![599]);
        Assert.Equal("Hazer", desk.GetState().ChannelNotes[599]);
        Assert.Equal(0, desk.GetState().Channels[599]);   // a fader is no part of the project
    }

    [Fact]
    public void FewerUniversesAreRefusedWhileSomethingOfTheProjectUsesThem()
    {
        var desk = new Desk();
        desk.SetUniverses(2);

        desk.SetSequence(1, loop: true, [new Step("channel", 600, 255, 1, 0, 1)]);
        AssertRefused(
            "Universe 2 is still in use: step 1 of the sequence of memory 1 is on channel 88 (U2). Take it out of there first.",
            () => desk.SetUniverses(1));
        desk.SetSequence(1, loop: true, []);

        int step = desk.AddStackStep(new Step("channel", 513, 255, 1, 0, 1));
        AssertRefused("step 1.0 of the stack", () => desk.SetUniverses(1));
        desk.DeleteStackStep(step);

        desk.SetStage(new StagePlan(16, 10, [new StageLight(1, "Wash", 1000, "RGB", null, 0, 0, 0)]));
        AssertRefused("the light \"Wash\" of the stage", () => desk.SetUniverses(1));
        desk.SetStage(new StagePlan(16, 10, []));

        desk.SetIndependent(4, 700);
        AssertRefused("channel key 4 of the independents", () => desk.SetUniverses(1));
        desk.SetIndependent(4, 4);

        desk.SetUniverses(1);
        Assert.Equal(1, desk.Universes);
    }

    [Fact]
    public void StepOrLightBeyondTheUniversesOfTheProjectIsRefused()
    {
        var desk = new Desk();

        AssertRefused(
            "Channel 88 (U2) is not in the project, which has 1 universe. Add universes in Settings first.",
            () => desk.SetSequence(1, loop: true, [new Step("channel", 600, 255, 1, 0, 1)]));
        AssertRefused(
            "Channel 1 (U2) is not in the project, which has 1 universe. Add universes in Settings first.",
            () => desk.AddStackStep(new Step("channel", 513, 255, 1, 0, 1)));
        AssertRefused(
            "The light \"Wash\" ends on channel 490 (U2), and the project has 1 universe. Add universes in Settings first.",
            () => desk.SetStage(new StagePlan(16, 10, [new StageLight(1, "Wash", 1000, "RGB", null, 0, 0, 0)])));
    }

    // Stores a look in a memory and leaves every fader down again.
    private static void Store(Desk desk, int memory, params (int Channel, int Level)[] look)
    {
        desk.Clear();
        foreach ((int channel, int level) in look)
            desk.SetChannel(channel, level);

        desk.SaveMemory(memory);
        desk.Clear();
    }

    // The look of every memory, null for an empty one.
    private static byte[]?[] Looks(Desk desk)
    {
        return desk.GetProject().Content.Looks;
    }

    // A project without looks and without notes.
    private static ProjectContent EmptyProject()
    {
        return new ProjectContent(new byte[]?[128], NoNotes(512), NoNotes(128), Desk.DefaultIndependents());
    }

    private static string[] NoNotes(int count)
    {
        return Enumerable.Repeat("", count).ToArray();
    }

    private static void AssertOutOfRange(string expectedRange, Action action)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(action);
        Assert.Contains(expectedRange, error.Message);
    }

    private static void AssertRefused(string expectedInMessage, Action action)
    {
        var error = Assert.Throws<DeskException>(action);
        Assert.Contains(expectedInMessage, error.Message);
    }

    private static void AssertMovesOutputCounter(Desk desk, Action change)
    {
        long before = desk.OutputChanges;
        change();
        Assert.True(desk.OutputChanges > before);
    }

    private static void AssertMovesProjectCounter(Desk desk, Action change)
    {
        long before = desk.ProjectChanges;
        change();
        Assert.True(desk.ProjectChanges > before);
    }
}
