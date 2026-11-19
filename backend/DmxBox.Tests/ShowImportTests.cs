using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>Making a Lumenet project out of a show file of a theatre console.</summary>
public class ShowImportTests
{
    private const char SecondLine = (char)0x2028;

    [Fact]
    public void CuesBecomeMemoriesAndStepsOfTheStack()
    {
        ImportedShow show = Import("""
            Cue 1
            Up 3
            Down 4
            Chan 1 50
            Text Front
            Cue 2
            Up 6
            Down 2
            Chan 2 FL 3 H80
            Text Back
            """);

        byte[]?[] looks = show.Content.Looks;
        Assert.Equal(128, looks[0]![0]);   // 50 % of 255 is 127.5
        Assert.Equal(255, looks[1]![1]);
        Assert.Equal(128, looks[1]![2]);
        Assert.Equal(2, looks.Count(look => look is not null));
        Assert.Equal(["Cue 1" + SecondLine + "Front", "Cue 2" + SecondLine + "Back"], show.Content.MemoryNotes[..2]);

        // the down of a step is that of the cue after it; after the last comes the first
        Assert.Equal(
            [
                new ProjectStep(new CueNumber(1, 0), new Step(Step.Memory, 1, 255, 3, 0, 2)),
                new ProjectStep(new CueNumber(2, 0), new Step(Step.Memory, 2, 255, 6, 0, 4)),
            ],
            show.Content.Stack);
    }

    [Fact]
    public void FollowTimeBecomesTheWaitOnceTheStepIsUp()
    {
        ImportedShow show = Import("""
            Cue 1
            Up 2
            Followon 5
            Cue 2
            Up 4
            Followon 1
            Cue 3
            Up 1
            Followon 3
            """);

        // 5 seconds after the start is 3 after the end of the fade; a follow inside the fade
        // starts the next cue once this one is up; the last cue ends the show
        Assert.Equal([3, 0.1, 0], show.Content.Stack.Select(step => step.Step.Wait).ToArray());
        Assert.Contains("Follows that start the next cue before this one is up: 1. Here the next cue starts once this one is up.", show.Report);
    }

    [Fact]
    public void LastCueThatLinksToTheFirstGoesRoundAndOtherLinksAreLeftOut()
    {
        ImportedShow show = Import("""
            Cue 1
            Up 2
            Link 3
            Cue 2
            Up 2
            Link 3
            Cue 3
            Up 2
            Followon 5
            Link 1
            """);

        // cue 2 links to the next cue anyway; cue 3 to the first, which is how the stack goes round
        Assert.Equal([0, 0, 3], show.Content.Stack.Select(step => step.Step.Wait).ToArray());
        Assert.Contains(
            "Links to other cues: 1. They are left out: the stack plays the cues in the order of their numbers, and after the last comes the first.",
            show.Report);
    }

    [Fact]
    public void CueInPartsBecomesOneFadeAsLongAsItsSlowestPart()
    {
        ImportedShow show = Import("""
            Cue 14
            Part 1
            Up 2
            Chan 1 100
            Part 2
            Up 5 3
            Chan 2 100
            """);

        Step step = Assert.Single(show.Content.Stack).Step;
        Assert.Equal(8, step.Up);     // part 2 waits 3 seconds and fades for 5
        Assert.Equal(5, step.Down);   // a part that gives only its up time has the same time down
        Assert.Equal(255, show.Content.Looks[0]![1]);
        Assert.Contains("Cues in parts: 1. Each became one fade, as long as its slowest part.", show.Report);
        Assert.Contains("Cues with a delay: 1. The delay is added to the fade time, so the fade ends when it would on the console.", show.Report);
    }

    [Fact]
    public void CueWithoutTimesFadesInFiveSecondsAndOneDirectionGivesTheOther()
    {
        ImportedShow show = Import("Cue 1\nChan 1 FL\nCue 2\nDown 3\nChan 2 FL");

        Assert.Equal<(double, double)>((5, 3), (show.Content.Stack[0].Step.Up, show.Content.Stack[0].Step.Down));
        Assert.Equal<(double, double)>((3, 5), (show.Content.Stack[1].Step.Up, show.Content.Stack[1].Step.Down));
        Assert.Contains("Cues without a fade time: 1. They fade in 5 seconds.", show.Report);
    }

    [Fact]
    public void BlackoutCueHoldsALookOfZeros()
    {
        ImportedShow show = Import("Cue 1\nChan 1 FL\nCue 2\nText Blackout\nDown 3");

        Assert.Equal(new byte[512], show.Content.Looks[1]);
        Assert.Equal("Cue 2" + SecondLine + "Blackout", show.Content.MemoryNotes[1]);
    }

    [Fact]
    public void PatchPutsTheLevelsOntoTheAddressesAndNamesTheChannels()
    {
        ImportedShow show = Import("""
            Patch 1 5<8@100 5<9@80 7<7@100 3<600@100
            $$DimmerNames 7 FRONTLEFTWASH
            Cue 1
            Chan 5 FL 6 50 7 50
            """);

        byte[] look = show.Content.Looks[0]!;
        Assert.Equal(255, look[7]);   // address 8
        Assert.Equal(204, look[8]);   // address 9: 80 %
        Assert.Equal(128, look[6]);   // address 7
        Assert.Equal(3, look.Count(level => level > 0));

        string[] notes = show.Content.ChannelNotes;
        Assert.Equal("Ch 5", notes[7]);
        Assert.Equal("Ch 5" + SecondLine + "80%", notes[8]);
        Assert.Equal("FRONTLEFTWA" + SecondLine + "SH", notes[6]);   // the dimmer's name, over both lines
        Assert.Equal(3, notes.Count(note => note != ""));

        Assert.Contains("Channels without a DMX address from 1 to 512: 1 (channel 6). Their levels are left out.", show.Report);
        Assert.Contains("Dimmers above 512 in the patch: 1. They are left out: the box sends one universe, addresses 1 to 512.", show.Report);
    }

    [Fact]
    public void WithoutPatchChannelOneIsAddressOne()
    {
        ImportedShow show = Import("Cue 1\nChan 1 FL 512 FL 513 FL 600 FL 601 FL 602 FL 603 FL 604 FL");

        byte[] look = show.Content.Looks[0]!;
        Assert.Equal<(int, int)>((255, 255), (look[0], look[511]));
        Assert.All(show.Content.ChannelNotes, note => Assert.Equal("", note));
        Assert.Contains(
            "Channels without a DMX address from 1 to 512: 6 (channels 513, 600, 601, 602, 603 and 1 more). Their levels are left out.",
            show.Report);
    }

    [Fact]
    public void SubmastersAndThenGroupsComeAfterTheCues()
    {
        ImportedShow show = Import("""
            Group 10
            Chan 1 FL
            Text blue wash
            Sub 2
            Chan 2 FL
            Sub 1
            Chan 3 FL
            Text Haze
            Cue 1
            Chan 4 FL
            """);

        Assert.Equal(
            ["Cue 1", "Sub 1" + SecondLine + "Haze", "Sub 2", "Group 10" + SecondLine + "blue wash"],
            show.Content.MemoryNotes[..4]);
        Assert.Equal([3, 2, 1, 0], show.Content.Looks.Take(4).Select(look => Array.IndexOf(look!, (byte)255)).ToArray());
        Assert.Contains("Memory 1 holds the cue, 2 to 3 the submasters and 4 the group.", show.Report);
        Assert.Single(show.Content.Stack);
    }

    [Fact]
    public void SubmastersOnSeveralPagesNameTheirPage()
    {
        ImportedShow show = Import("Sub 1 2\nChan 1 FL\nSub 1 1\nChan 2 FL");

        Assert.Equal(["Sub 1 p1", "Sub 1 p2"], show.Content.MemoryNotes[..2]);
    }

    [Fact]
    public void WhatDoesNotFitIntoTheMemoriesIsLeftOut()
    {
        string cues = string.Concat(Enumerable.Range(1, 130).Select(number => $"Cue {number}\nChan 1 {number % 100}\n"));
        ImportedShow show = Import(cues + "Sub 1\nChan 2 FL\nGroup 1\nChan 3 FL");

        Assert.All(show.Content.Looks, look => Assert.NotNull(look));
        Assert.Equal(128, show.Content.Stack.Length);
        Assert.Equal("Cue 128", show.Content.MemoryNotes[127]);
        Assert.Contains("Left out, because Lumenet has 128 memories: 2 cues, 1 submaster and 1 group.", show.Report);
    }

    [Fact]
    public void CueNumberTheStackCannotHaveGetsTheNextFreeOne()
    {
        ImportedShow show = Import("Cue 1\nCue 1.25\nCue 1.3\nCue 2");

        Assert.Equal(["1.0", "1.1", "1.3", "2.0"], show.Content.Stack.Select(step => step.Cue.ToString()).ToArray());
        Assert.Equal("Cue 1.25", show.Content.MemoryNotes[1]);
        Assert.Contains(
            "Cue numbers the stack cannot have: 1. Its numbers go up to 999.9, with one digit after the dot. Those steps got the next free number; the note under the memory keeps the cue's own number.",
            show.Report);
    }

    [Fact]
    public void WithoutAFreeNumberTheStepsAreNumberedInTurn()
    {
        ImportedShow show = Import("Cue 1.2\nCue 1.21\nCue 1.3\nCue 1000");

        Assert.Equal(["1.0", "2.0", "3.0", "4.0"], show.Content.Stack.Select(step => step.Cue.ToString()).ToArray());
        Assert.Contains(show.Report, line => line.StartsWith("Cue numbers the stack cannot have: 2.") && line.Contains("1.0, 2.0 and on"));
    }

    [Fact]
    public void OnlyTheFirstCueListIsImported()
    {
        ImportedShow show = Import("Cue 1 2\nChan 1 FL\nCue 1 1\nChan 2 FL\nCue 2 2\nChan 3 FL");

        Assert.Equal(2, show.Content.Stack.Length);
        Assert.Equal(255, show.Content.Looks[0]![0]);
        Assert.Contains("Cues in other cue lists: 1. Only cue list 2 is imported, the first in the file.", show.Report);
    }

    [Fact]
    public void NotesAreTwoLinesOfElevenCharacters()
    {
        ImportedShow show = Import("""
            Cue 1
            Text Lights up on the garden
            Cue 1234.567
            Text Intermission
            Cue 3
            $$UText Sc&#233;na &#8647;
            Text Scena
            """);

        Assert.Equal(
            ["Cue 1" + SecondLine + "Lights up", "Cue 3" + SecondLine + "Scéna ⇇", "Cue 1234.56" + SecondLine + "Intermissio"],
            show.Content.MemoryNotes[..3]);
    }

    [Fact]
    public void TimesAboveTheLongestStepAreShortened()
    {
        ImportedShow show = Import("Cue 1\nUp 20:00\nDown 5");

        Assert.Equal(999.9, Assert.Single(show.Content.Stack).Step.Up);
        Assert.Contains("Times above 999.9 seconds: 1. They are cut to 999.9 seconds, the longest a step can have.", show.Report);
    }

    [Fact]
    public void ReportSaysWhatTheFileHeldAndWhereItWent()
    {
        ImportedShow show = ShowImport.Read("Hamlet.asc", """
            Manufacturer ETC
            Console Eos
            Cue 1
            Up 2
            Cue 2
            Up 2
            Sub 1
            Chan 1 FL
            $$Param 1 2 3
            Fade 1
            """);

        Assert.Equal(
            [
                "Hamlet.asc, written by ETC Eos: 2 cues and 1 submaster.",
                "Memories 1 to 2 hold the cues and 3 the submaster.",
                "The stack has a step for each cue, with the cue's number and times.",
                "Lines for one maker's consoles: 1. They hold such things as moving lights, effects and macros, and are skipped.",
                "Lines that could not be read: 1 (line 10). They are skipped.",
                "Nothing is saved yet: Save keeps the project.",
            ],
            show.Report);
    }

    [Fact]
    public void FileWithoutCuesSubmastersOrGroupsIsRefused()
    {
        var error = Assert.Throws<DeskException>(() => ShowImport.Read("notes.txt", "Dear diary,\nPatch 1 1<1@100"));

        Assert.Equal(
            "\"notes.txt\" has no cues, submasters or groups. Lumenet imports show files in the USITT ASCII format, the text that theatre consoles write.",
            error.Message);
    }

    [Theory]
    [InlineData("Hamlet.asc", "Hamlet")]
    [InlineData("Act 1: Scene 2.ASC", "Act 1  Scene 2")]
    [InlineData("show", "show")]
    [InlineData(".asc", ShowImport.FallbackName)]
    [InlineData("con.asc", ShowImport.FallbackName)]
    public void ProjectIsNamedAfterTheFile(string fileName, string projectName)
    {
        Assert.Equal(projectName, ShowImport.Read(fileName, "Cue 1").Name);
    }

    [Fact]
    public void ImportedProjectIsAProjectLikeAnyOther()
    {
        ImportedShow show = Import("""
            Patch 1 1<5@100
            Cue 1
            Up 3
            Chan 1 FL
            Text Front
            Sub 1
            Chan 1 50
            """);

        // what the project file keeps of it comes back the same
        using var file = new MemoryStream();
        ProjectFormat.Write(file, show.Content);
        file.Position = 0;
        ProjectContent again = ProjectFormat.Read(file);

        Assert.Equal(show.Content.Looks, again.Looks);
        Assert.Equal(show.Content.MemoryNotes, again.MemoryNotes);
        Assert.Equal(show.Content.ChannelNotes, again.ChannelNotes);
        Assert.Equal(show.Content.Stack, again.Stack);
    }

    [Fact]
    public void SmartFadeMemoriesStayMemoriesAndItsStackPlaysThem()
    {
        ProjectContent content = Import(SmartFadeFile.Text).Content;

        // the console's memories 1, 2 and 25, in that order; a percentage rounded up, as on the console
        Assert.Equal(128, content.Looks[0]![0]);   // 50 % is 127.5
        Assert.Equal(44, content.Looks[0]![1]);    // 17 % is 43.35: the console has 44
        Assert.Equal(255, content.Looks[1]![9]);   // channel 3 is on DMX 10
        Assert.Equal(0, content.Looks[1]![2]);     // DMX 3 has no channel
        Assert.Equal(204, content.Looks[2]![4]);   // 80 %
        Assert.Equal(
            ["Mem 1" + SecondLine + "WARM FRONT", "Mem 2" + SecondLine + "BACK", "Mem 25" + SecondLine + "ČERVEN"],
            content.MemoryNotes[..3]);

        // after them the empty memory 6 that a step plays, and the steps with levels of their own
        Assert.Null(content.Looks[3]);
        Assert.Equal("Mem 6", content.MemoryNotes[3]);
        Assert.Equal("Cue 3" + SecondLine + "STORM", content.MemoryNotes[4]);
        Assert.Equal(102, content.Looks[4]![3]);   // 40 %
        Assert.Equal("Ch 4", content.MemoryNotes[5]);
        Assert.Equal(255, content.Looks[5]![3]);

        // each step brings up the memory it plays on the console: memory 1 twice. A wait of 0 waits
        // for Play; 1:30 is 90 seconds once the step is up. The down is that of the step after.
        Assert.Equal(
            [
                new ProjectStep(new CueNumber(1, 0), new Step(Step.Memory, 1, 255, 3, 0, 4)),
                new ProjectStep(new CueNumber(2, 0), new Step(Step.Memory, 2, 255, 5, 90, 0)),
                new ProjectStep(new CueNumber(2, 5), new Step(Step.Memory, 1, 255, 0, 0, 1)),
                new ProjectStep(new CueNumber(3, 0), new Step(Step.Memory, 5, 255, 1, 0, 6)),
                new ProjectStep(new CueNumber(4, 0), new Step(Step.Memory, 4, 255, 2, 10, 2)),
            ],
            content.Stack);
    }

    [Fact]
    public void SmartFadeSequencesBecomeMemoriesThatHoldThem()
    {
        ImportedShow show = Import(SmartFadeFile.Text);
        ProjectContent content = show.Content;

        // the steps in the order of the file, memory 25 twice; a wait of 0 goes on at once
        Sequence fire = content.Sequences[6]!;
        Assert.True(fire.Loop);
        Assert.Equal(
            [new Step(Step.Memory, 3, 255, 1, 0, 0.5), new Step(Step.Memory, 1, 255, 0.5, 2, 1), new Step(Step.Memory, 3, 255, 1, 0, 1)],
            fire.Steps);
        Assert.Equal("Seq 1" + SecondLine + "FIRE", content.MemoryNotes[6]);

        // One Shot runs once; its step of a single channel plays the memory made for it
        Sequence once = content.Sequences[7]!;
        Assert.False(once.Loop);
        Assert.Equal(new Step(Step.Memory, 6, 255, 1, 0, 1), Assert.Single(once.Steps));

        // a sequence stepped by hand runs in a loop here
        Assert.True(content.Sequences[8]!.Loop);
        Assert.Equal("Seq 3", content.MemoryNotes[8]);
        Assert.All(content.Looks[6..9], look => Assert.Null(look));
    }

    [Fact]
    public void SmartFadeChannelNotesAndIndependents()
    {
        ImportedShow show = Import(SmartFadeFile.Text);
        string[] notes = show.Content.ChannelNotes;

        Assert.Equal("Ch 3" + SecondLine + "CYC BLUE", notes[9]);   // DMX 10 has the console's channel 3
        Assert.Equal("", notes[0]);                                 // channel 1 is on DMX 1 and has no label
        Assert.Equal("IND 1" + SecondLine + "SMOKE", notes[96]);
        Assert.Equal("IND 2", notes[97]);
        Assert.Equal([97, 98, 3, 4, 5, 6, 7, 8, 9, 10], show.Content.Independents);
    }

    [Fact]
    public void SmartFadeReportSaysWhereEverythingWent()
    {
        Assert.Equal(
            [
                "show.asc, written by AVAB SMARTFADE: a stack of 5 steps, 3 memories and 3 sequences.",
                "Memories 1 to 3 hold the console's memories, 4 an empty memory that a step plays, 5 to 6 steps with levels of their own and 7 to 9 the sequences.",
                "The note under a memory gives its number on the console, as \"Mem 25\", and its label.",
                "The stack has the console's steps, with their numbers and times; each brings up the memory it plays on the console. " +
                "A wait of 0 waits for Play, as on the console; a longer one starts the next step that long after this one is up.",
                "Sequences in Manual mode: 1. A sequence here cannot be stepped by hand, so they run in a loop.",
                "Independent keys 1 and 2 switch the console's independents: IND 1 on DMX 97 and IND 2 on DMX 98.",
                "Nothing is saved yet: Save keeps the project.",
            ],
            Import(SmartFadeFile.Text).Report);
    }

    [Fact]
    public void SmartFadeIndependentOnTwoAddressesHasItsKeyOnTheFirst()
    {
        ImportedShow show = Import("CONSOLE SMARTFADE\nGROUP 1\nCHAN 1/100\nPATCH 1 1/1/100 8/49/100 97/49/100");

        Assert.Equal(8, show.Content.Independents[0]);
        Assert.Contains(
            "Independent key 1 switches the console's independent: IND 1 on DMX 8. " +
            "IND 1 also drives DMX 97, which the key does not switch: a key has one channel.",
            show.Report);
    }

    [Fact]
    public void SmartFadeShowComesBackWholeFromAProjectFile()
    {
        ProjectContent imported = Import(SmartFadeFile.Text).Content;

        using var file = new MemoryStream();
        ProjectFormat.Write(file, imported);
        file.Position = 0;
        ProjectContent loaded = ProjectFormat.Read(file);

        Assert.Equal(imported.Looks, loaded.Looks);
        Assert.Equal(imported.MemoryNotes, loaded.MemoryNotes);
        Assert.Equal(imported.ChannelNotes, loaded.ChannelNotes);
        Assert.Equal(imported.Independents, loaded.Independents);
        Assert.Equal(imported.Stack, loaded.Stack);
        Assert.Equal(imported.Sequences.Select(sequence => sequence?.Loop), loaded.Sequences.Select(sequence => sequence?.Loop));
        Assert.Equal(imported.Sequences.Select(sequence => sequence?.Steps), loaded.Sequences.Select(sequence => sequence?.Steps));
    }

    [Fact]
    public void SmartFadeFileWithoutAShowIsRefused()
    {
        var refusal = Assert.Throws<DeskException>(() => Import("MANUFACTURER AVAB\nCONSOLE SMARTFADE\nCLEAR ALL\nPATCH 1 1/1/100\nENDDATA"));

        Assert.Equal(
            "\"show.asc\" is the show of a SmartFade, but it has no stack, memories or sequences: there is nothing to import.",
            refusal.Message);
    }

    private static ImportedShow Import(string text)
    {
        return ShowImport.Read("show.asc", text);
    }
}
