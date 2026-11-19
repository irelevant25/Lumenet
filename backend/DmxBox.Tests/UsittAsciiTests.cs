using System.Diagnostics;
using System.Text;
using DmxBox.Server;

namespace DmxBox.Tests;

/// <summary>Reading show files in the USITT ASCII format, as theatre consoles write them.</summary>
public class UsittAsciiTests
{
    [Fact]
    public void HugeFileIsReadQuicklyAndKeepsAtMostMaxKeptOfOneKind()
    {
        var text = new StringBuilder();
        for (int cue = 1; cue <= 200_000; cue++)
            text.Append("Cue ").Append(cue).Append("\nChan 1 50\n");
        for (int dimmer = 1; dimmer <= 1000; dimmer++)
            text.Append("Patch 1 1<").Append(dimmer).Append("@100\n");

        var clock = Stopwatch.StartNew();
        AsciiShow show = UsittAscii.Read(text.ToString());

        // A check for numbers that came before, made by looking through every cue kept so far,
        // took minutes on such a file.
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), $"Reading took {clock.Elapsed}.");
        Assert.Equal(UsittAscii.MaxKept, show.Cues.Count);
        Assert.Equal(200_000 - UsittAscii.MaxKept, show.Skipped);
        Assert.Equal(512, show.Patch.Count);       // dimmers 1 to 512
        Assert.Equal(488, show.DimmersAbove);      // the rest are only counted
        Assert.Empty(show.UnreadLines);
    }

    [Fact]
    public void ReadsACueWithItsLevelsTimesAndWhatComesAfterIt()
    {
        // Cue 1 of Strand's "Guide to ASCII Cues"
        AsciiShow show = UsittAscii.Read("""
            Ident 3:0
            Manufacturer Strand
            Console LightPalette90
            Cue 1
            Up 5
            Down 10
            Chan 1=50  2=30  5=100  6=100
            Text curtain warmers
            Followon 15
            Link 8.5
            """);

        AsciiCue cue = Assert.Single(show.Cues);
        Assert.Equal(1m, cue.Number);
        Assert.Null(cue.Page);
        Assert.Equal(new Dictionary<int, double> { [1] = 0.5, [2] = 0.3, [5] = 1, [6] = 1 }, cue.Levels);
        AsciiFade fade = Assert.Single(cue.Fades);
        Assert.Equal<(double?, double?, double?, double?)>((5, null, 10, null), (fade.Up, fade.UpDelay, fade.Down, fade.DownDelay));
        Assert.Equal("curtain warmers", cue.Label);
        Assert.Equal(15, cue.Follow);
        Assert.Equal(8.5m, cue.Link);
        Assert.Equal("Strand", show.Manufacturer);
        Assert.Equal("LightPalette90", show.Console);
        Assert.Empty(show.UnreadLines);
        Assert.Equal(0, show.MakerLines);
    }

    [Fact]
    public void AnySeparatorWillDoAndCaseDoesNotMatter()
    {
        AsciiShow show = UsittAscii.Read("""
            cUe,2.3,,1
            CHAN 1/FL,2<H80   3@50 4>Hff 5=7Fh
            text	Two words,  kept as written
            """);

        AsciiCue cue = Assert.Single(show.Cues);
        Assert.Equal(2.3m, cue.Number);
        Assert.Equal(1, cue.Page);
        Assert.Equal(1, cue.Levels[1]);
        Assert.Equal(128 / 255.0, cue.Levels[2]);
        Assert.Equal(0.5, cue.Levels[3]);
        Assert.Equal(1, cue.Levels[4]);
        Assert.Equal(127 / 255.0, cue.Levels[5]);
        Assert.Equal("Two words,  kept as written", cue.Label);
        Assert.Empty(show.UnreadLines);
    }

    [Theory]
    [InlineData("5", 5)]
    [InlineData("2.5", 2.5)]
    [InlineData(".2", 0.2)]
    [InlineData("1:15", 75)]
    [InlineData("1:02:03.5", 3723.5)]
    public void TimesAreSecondsOrMinutesAndSeconds(string written, double seconds)
    {
        AsciiShow show = UsittAscii.Read($"Cue 1\nUp {written}");

        Assert.Equal(seconds, Assert.Single(show.Cues).Fade.Up!.Value, precision: 9);
    }

    [Fact]
    public void FadeCanWaitBeforeItStarts()
    {
        AsciiShow show = UsittAscii.Read("Cue 2.3\nUp 120 20\nDown 1:15 0:05");

        AsciiFade fade = Assert.Single(show.Cues).Fade;
        Assert.Equal<(double?, double?, double?, double?)>((120, 20, 75, 5), (fade.Up, fade.UpDelay, fade.Down, fade.DownDelay));
    }

    [Fact]
    public void CommentsBlankLinesAndWhatComesAfterEndDataDoNotCount()
    {
        AsciiShow show = UsittAscii.Read("""
            ! a show

            Cue 1   ! the first
            Chan 1 50 ! half
            EndData
            Cue 2
            Chan 2 50
            """);

        AsciiCue cue = Assert.Single(show.Cues);
        Assert.Equal(new Dictionary<int, double> { [1] = 0.5 }, cue.Levels);
        Assert.Empty(show.UnreadLines);
    }

    [Fact]
    public void ByteOrderMarkAndEveryKindOfLineEndAreFine()
    {
        string text = ((char)0xFEFF) + "Cue 1\rChan 1 50\r\nCue 2\nChan 2 FL";

        AsciiShow show = UsittAscii.Read(text);

        Assert.Equal([1m, 2m], show.Cues.Select(cue => cue.Number).ToArray());
        Assert.Empty(show.UnreadLines);
    }

    [Fact]
    public void PartsHaveTheirOwnTimesAndAllTheirLevelsBelongToTheCue()
    {
        AsciiShow show = UsittAscii.Read("""
            Cue 8.5
            Part 1
            Up 3
            Chan 2 100 3 100
            Part 2
            Up 3 1
            Chan 1 50 7 50 8 75
            """);

        AsciiCue cue = Assert.Single(show.Cues);
        Assert.Equal(3, cue.Fades.Count);   // the cue's own, then the two parts
        Assert.Null(cue.Fades[0].Up);
        Assert.Equal<(double?, double?)>((3, null), (cue.Fades[1].Up, cue.Fades[1].UpDelay));
        Assert.Equal<(double?, double?)>((3, 1), (cue.Fades[2].Up, cue.Fades[2].UpDelay));
        Assert.Equal([1, 2, 3, 7, 8], cue.Levels.Keys.Order().ToArray());
    }

    [Fact]
    public void SubmastersAndGroupsHaveLevelsAndLabels()
    {
        AsciiShow show = UsittAscii.Read("""
            Sub 1 5
            Chan 10 100 11 50
            Up 2
            Text lead guitar
            Group 10
            Chan 1 100 2 100
            Text blue wash
            """);

        AsciiLook submaster = Assert.Single(show.Submasters);
        Assert.Equal<(decimal, int?)>((1m, 5), (submaster.Number, submaster.Page));
        Assert.Equal(new Dictionary<int, double> { [10] = 1, [11] = 0.5 }, submaster.Levels);
        Assert.Equal("lead guitar", submaster.Label);
        AsciiLook group = Assert.Single(show.Groups);
        Assert.Equal<(decimal, int?)>((10m, null), (group.Number, group.Page));
        Assert.Equal("blue wash", group.Label);
        Assert.Empty(show.UnreadLines);   // the bump time of the submaster is no mistake
    }

    [Fact]
    public void PatchSaysWhichChannelDrivesWhichDimmer()
    {
        AsciiShow show = UsittAscii.Read("""
            Patch 1 5<8@100 5<9@80
            Patch 2,6,10,FL
            $$DimmerNames 8 FRT 10 BACK
            """);

        Assert.Equal(
            new SortedDictionary<int, AsciiPatch> { [8] = new(5, 1), [9] = new(5, 0.8), [10] = new(6, 1) },
            show.Patch);
        Assert.Equal(new SortedDictionary<int, string> { [8] = "FRT", [10] = "BACK" }, show.DimmerNames);
        Assert.Empty(show.UnreadLines);
        Assert.Equal(0, show.MakerLines);
    }

    [Fact]
    public void KeywordsOfOneMakerAreSkippedWithTheBlocksTheyBegin()
    {
        AsciiShow show = UsittAscii.Read("""
            $Macro 8
            Text time 15 *
            Cue 3
            $$Effect 2
            Chan 1 50
            $Profile 5
            $$Curve 40=50 80=100
            Group 1
            Chan 2 FL
            """);

        Assert.Equal(new Dictionary<int, double> { [1] = 0.5 }, Assert.Single(show.Cues).Levels);
        Assert.Equal(new Dictionary<int, double> { [2] = 1 }, Assert.Single(show.Groups).Levels);
        Assert.Equal(5, show.MakerLines);   // the macro with its text, the effect, the profile with its curve
        Assert.Empty(show.UnreadLines);
    }

    [Fact]
    public void ETCsLabelWithLettersBeyondAsciiReplacesThePlainOne()
    {
        AsciiShow show = UsittAscii.Read("""
            Ident 3:0
            Manufacturer ETC
            Console Eos
            $$Format 3.20
            Cue 1 1
            Text Scena
            $$UText,Sc&#233;na &#8647;
            """);

        AsciiCue cue = Assert.Single(show.Cues);
        Assert.Equal("Scena", cue.Label);
        Assert.Equal("Scéna ⇇", cue.UnicodeLabel);
        Assert.Equal(("ETC", "Eos"), (show.Manufacturer, show.Console));
        Assert.Equal(1, show.MakerLines);   // $$Format
    }

    [Fact]
    public void SetCanNameTheConsoleToo()
    {
        AsciiShow show = UsittAscii.Read("Set Channels 100\nSet Manufacturer Strand\nSet Console LightPalette90\nSet Patch Default");

        Assert.Equal(("Strand", "LightPalette90"), (show.Manufacturer, show.Console));
        Assert.Empty(show.UnreadLines);
    }

    [Fact]
    public void LinesThatCannotBeReadAreNotedAndSkipped()
    {
        AsciiShow show = UsittAscii.Read("""
            Chan 1 50
            Cue 1
            Chan 1
            Up soon
            Fade 5
            Chan 2 50
            Cue later
            Chan 3 50
            Patch 1 5<8
            """);

        Assert.Equal([1, 3, 4, 5, 7, 9], show.UnreadLines.ToArray());   // the lines of cue "later" go with it
        Assert.Equal(new Dictionary<int, double> { [2] = 0.5 }, Assert.Single(show.Cues).Levels);
    }

    [Fact]
    public void NumberThatCameBeforeKeepsItsFirstCue()
    {
        AsciiShow show = UsittAscii.Read("""
            Cue 1
            Chan 1 50
            Cue 1 1
            Chan 2 50
            Cue 1 2
            Chan 3 50
            """);

        Assert.Equal(2, show.Cues.Count);   // no list is list 1; list 2 is another one
        Assert.Equal(new Dictionary<int, double> { [1] = 0.5 }, show.Cues[0].Levels);
        Assert.Equal(1, show.Repeated);
    }

    [Fact]
    public void ClearForgetsWhatCameBeforeIt()
    {
        AsciiShow show = UsittAscii.Read("""
            Cue 1
            Patch 1 1<1@100
            Clear Cues Patch
            Cue 2
            """);

        Assert.Equal(2m, Assert.Single(show.Cues).Number);
        Assert.Empty(show.Patch);
    }

    [Fact]
    public void ZeroInAPatchPatchesNothing()
    {
        AsciiShow show = UsittAscii.Read("Patch 1 5<8@100 0<9@100 6<0@100");

        Assert.Equal(new SortedDictionary<int, AsciiPatch> { [8] = new(5, 1) }, show.Patch);
        Assert.Empty(show.UnreadLines);
    }

    [Fact]
    public void SmartFadeFileIsReadInItsOwnDialect()
    {
        AsciiShow show = UsittAscii.Read(SmartFadeFile.Text);

        Assert.True(show.IsSmartFade);

        // the stack: each step names the memory it plays, and repeats its levels
        Assert.Equal(new[] { 1m, 2m, 2.5m, 3m, 4m }, show.Cues.Select(cue => cue.Number).ToArray());
        Assert.Equal(new int?[] { 1, 2, 1, null, 6 }, show.Cues.Select(cue => cue.Memory).ToArray());
        Assert.Equal(new double?[] { 0, 90, 0, 0, 10 }, show.Cues.Select(cue => cue.Follow).ToArray());
        Assert.Equal(new Dictionary<int, double> { [1] = 0.5, [2] = 0.17 }, show.Cues[0].Levels);
        Assert.Equal("STORM", show.Cues[3].BestLabel());

        // the memories, with their labels in lines of six letters
        Assert.Equal(new[] { 1m, 2m, 25m }, show.Groups.Select(memory => memory.Number).ToArray());
        Assert.Equal(new[] { "WARM FRONT", "BACK", "ČERVEN" }, show.Groups.Select(memory => memory.BestLabel()).ToArray());

        // the sequences: their steps in the order of the file, with the same memory twice
        Assert.Equal(new[] { (0, 0), (1, 1), (2, 2) }, show.Sequences.Select(sequence => (sequence.Index, sequence.Mode)).ToArray());
        Assert.Equal("FIRE", show.Sequences[0].BestLabel());
        Assert.Equal(new int?[] { 25, 1, 25 }, show.Sequences[0].Steps.Select(step => step.Memory).ToArray());
        AsciiCue single = Assert.Single(show.Sequences[1].Steps);
        Assert.Null(single.Memory);
        Assert.Equal(new Dictionary<int, double> { [4] = 1 }, single.Levels);

        // the patch has the address first; channel 0 is none, 49 and 50 are the independents
        Assert.Equal(
            new SortedDictionary<int, AsciiPatch>
            {
                [1] = new(1, 1), [2] = new(2, 1), [4] = new(4, 1), [5] = new(5, 1), [10] = new(3, 1), [97] = new(49, 1), [98] = new(50, 1),
            },
            show.Patch);
        Assert.Equal("CYC BLUE", show.ChannelLabels[3].BestLabel());
        Assert.Equal("SMOKE", show.IndependentLabels[1].BestLabel());
        Assert.Equal("", show.IndependentLabels[2].BestLabel());

        // everything is understood
        Assert.Equal(0, show.MakerLines);
        Assert.Empty(show.UnreadLines);
        Assert.Equal(0, show.Repeated);
    }

    [Fact]
    public void SmartFadeKeywordsMeanNothingSpecialInTheFileOfAnotherConsole()
    {
        AsciiShow show = UsittAscii.Read("""
            Console Eos
            Cue 1
            $$Memory 0 4
            Chan 1 50
            Patch 1 97<49@100
            """);

        Assert.Null(Assert.Single(show.Cues).Memory);
        Assert.Equal(1, show.MakerLines);
        Assert.Equal(new SortedDictionary<int, AsciiPatch> { [49] = new(97, 1) }, show.Patch);   // channel 97 on dimmer 49
    }
}
