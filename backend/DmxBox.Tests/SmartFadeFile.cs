namespace DmxBox.Tests;

/// <summary>
/// A show file in the shape an ETC (AVAB) SmartFade writes: its memories as GROUP blocks, a stack
/// whose steps name the memory they play, sequences of "$CUE" steps, labels in lines of up to six
/// letters, and a patch with the DMX address first.
/// </summary>
internal static class SmartFadeFile
{
    /// <summary>
    /// A small show: memories 1, 2 and 25; a stack of five steps (one a "state" with levels of
    /// its own, one that plays the empty memory 6); a sequence in a loop that plays memory 25
    /// twice, one that runs once with a step of a single channel, and one stepped by hand.
    /// Console channel 3 is on DMX 10, DMX 3 has no channel, the independents are on DMX 97 and 98.
    /// </summary>
    public const string Text = """
        MANUFACTURER AVAB
        CONSOLE SMARTFADE
        $SOFTWAREVERSION 3.0.2 43
        IDENT 3.0

        CLEAR ALL

        ! Main Sequence
        $SEQUENCE 48 2 0 256 256

        CUE  1.0
        $$MEMORY 0 0
        $$CUEPARAMS 1
        UP 3.0   0
        DOWN 2.0   0
        FOLLOWON 0
        CHAN 1/50 2/17

        CUE  2.0
        $$MEMORY 0 1
        $$CUEPARAMS 1
        UP 5.0   0
        DOWN 4.0   0
        FOLLOWON 1:30
        CHAN 3/100

        CUE  2.5
        $$MEMORY 0 0
        $$CUEPARAMS 1
        UP 0     0
        DOWN 0     0
        FOLLOWON 0
        CHAN 1/50 2/17

        CUE  3.0
        $$CUEPARAMS 2
        UP 1.0   0
        DOWN 1.0   0
        FOLLOWON 0
        CHAN 4/40
        Text STORM
        $$Text 1 STORM
        $$Text 2
        $$Text 3

        CUE  4.0
        $$MEMORY 0 5
        $$CUEPARAMS 1
        UP 2.0   0
        DOWN 6.0   0
        FOLLOWON 10

        ! Sequences
        $SEQUENCE 0 0 0 256 256

        Text FIRE
        $$Text 1 FIRE
        $$Text 2
        $$Text 3

        $CUE 25.0
        $$MEMORY 1 0
        $$CUEPARAMS 1
        UP 1.0   0
        DOWN 1.0   0
        FOLLOWON 0
        CHAN 5/80

        $CUE  1.0
        $$MEMORY 0 0
        $$CUEPARAMS 1
        UP 0.5   0
        DOWN 0.5   0
        FOLLOWON 2.0
        CHAN 1/50 2/17

        $CUE 25.0
        $$MEMORY 1 0
        $$CUEPARAMS 1
        UP 1.0   0
        DOWN 1.0   0
        FOLLOWON 0
        CHAN 5/80

        $SEQUENCE 1 1 0 256 256

        $CUE  4.0
        $$CUEPARAMS 0
        UP 1.0   0
        DOWN 1.0   0
        FOLLOWON 0
        CHAN 4/100

        $SEQUENCE 2 2 0 256 256

        $CUE  2.0
        $$MEMORY 0 1
        $$CUEPARAMS 1
        UP 1.0   0
        DOWN 1.0   0
        FOLLOWON 0
        CHAN 3/100


        ! Memories
        GROUP 1
        CHAN 1/50 2/17
        Text WARM
        $$Text 1 WARM
        $$Text 2 FRONT
        $$Text 3


        GROUP 2
        CHAN 3/100
        Text
        $$Text 1
        $$Text 2 BACK
        $$Text 3


        GROUP 25
        CHAN 5/80
        Text ?ERVEN
        $$Text 1 &#268;ERVEN
        $$Text 2
        $$Text 3


        ! Patch
        PATCH 1 1/1/100 2/2/100 3/0/100 4/4/100 5/5/100
        PATCH 1 10/3/100 97/49/100 98/50/100

        ! Independents
        $INDEPENDENT 1 0 255 0 1
        Text SMOKE
        $$Text 1 SMOKE
        $$Text 2
        $$Text 3

        $INDEPENDENT 2 0 255 0 1
        Text
        $$Text 1
        $$Text 2
        $$Text 3

        ! Channel Info
        $CHANNEL 3
        Text CYC
        $$Text 1 CYC
        $$Text 2 BLUE
        $$Text 3

        ! Pages
        $PAGE 1
        Text ACT 1
        $$Text 1 ACT 1
        $$Text 2
        $$Text 3


        ! Settings
        SET Language 0
        SET MIDIActive 1

        ENDDATA
        """;
}
