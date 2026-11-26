using System.Text;

namespace DmxBox.Tests.Osc;

/// <summary>The packets of OSC: messages with every kind of argument, bundles, and what is no OSC.</summary>
public sealed class OscMessageTests
{
    [Fact]
    public void AMessageIsReadWithItsArgumentsOfEveryKind()
    {
        byte[] packet = OscPacket.Write("/lumenet/fader/master", 0.5f, 7, "go", true, false, null, OscMessage.Impulse, new byte[] { 1, 2, 3 }, 3L, 2.25);

        OscMessage message = Assert.Single(OscPacket.Read(packet));

        Assert.Equal("/lumenet/fader/master", message.Address);
        Assert.Equal(10, message.Arguments.Count);
        Assert.Equal(0.5, message.Arguments[0]);   // a float is read as a double
        Assert.Equal(7L, message.Arguments[1]);    // a whole number as a long
        Assert.Equal("go", message.Arguments[2]);
        Assert.True(message.Arguments[3] is true);
        Assert.True(message.Arguments[4] is false);
        Assert.Null(message.Arguments[5]);
        Assert.Same(OscMessage.Impulse, message.Arguments[6]);
        Assert.Equal(new byte[] { 1, 2, 3 }, Assert.IsType<byte[]>(message.Arguments[7]));
        Assert.Equal(3L, message.Arguments[8]);
        Assert.Equal(2.25, message.Arguments[9]);
    }

    [Fact]
    public void TheBytesAreThoseOfOscFourAtATimeBigEndian()
    {
        // "/a", a zero and a zero more; ",i" and two zeros; 1 as four bytes, the highest first
        byte[] expected = [.. "/a\0\0,i\0\0"u8, 0, 0, 0, 1];
        Assert.Equal(expected, OscPacket.Write("/a", 1));

        // an address of four letters needs four zeros after it: the one that ends it, and three more
        byte[] four = [.. "/abc\0\0\0\0,\0\0\0"u8];
        Assert.Equal(four, OscPacket.Write("/abc"));
    }

    [Fact]
    public void ABundleGivesItsMessagesInOrderAlsoFromABundleInIt()
    {
        byte[] packet = OscPacket.Bundle(
            OscPacket.Write("/lumenet/stack/play"),
            OscPacket.Bundle(OscPacket.Write("/lumenet/fader/master", 1f), OscPacket.Write("/lumenet/memory/5", 1)),
            OscPacket.Write("/lumenet/stack/pause"));

        Assert.Equal(
            ["/lumenet/stack/play", "/lumenet/fader/master", "/lumenet/memory/5", "/lumenet/stack/pause"],
            OscPacket.Read(packet).Select(message => message.Address));
    }

    [Fact]
    public void WhatIsNoOscGivesNoMessage()
    {
        Assert.Empty(OscPacket.Read([]));
        Assert.Empty(OscPacket.Read("hello, desk"u8));
        Assert.Empty(OscPacket.Read("stack/play\0\0,\0\0\0"u8));   // an address begins with a slash
        Assert.Empty(OscPacket.Read("/lumenet/stack/play"u8));     // no zero at the end of the address

        byte[] cut = OscPacket.Write("/a", 1);
        Assert.Empty(OscPacket.Read(cut.AsSpan(0, cut.Length - 2)));   // the number is not whole
        Assert.Empty(OscPacket.Read("/a\0\0,x\0\0\0\0\0\0"u8));          // a kind of argument this desk does not know

        byte[] bundle = OscPacket.Bundle(OscPacket.Write("/a"));
        bundle[19] = 200;   // the size of its element is more than there is
        Assert.Empty(OscPacket.Read(bundle));
    }

    [Fact]
    public void AnOldSenderWithoutTypeTagsAndOneThatLeavesOutTheLastZerosAreUnderstood()
    {
        OscMessage old = Assert.Single(OscPacket.Read("/lumenet/stack/play\0"u8));
        Assert.Equal("/lumenet/stack/play", old.Address);
        Assert.Empty(old.Arguments);

        // ",s" and a text "go" without the zeros that would round it up to four bytes
        OscMessage sloppy = Assert.Single(OscPacket.Read("/a\0\0,s\0\0go\0"u8));
        Assert.Equal("go", Assert.Single(sloppy.Arguments));
    }

    [Fact]
    public void AMessageShowsItselfAsTheListOfIncomingMessagesHasIt()
    {
        Assert.Equal("/lumenet/stack/play", OscPacket.Read(OscPacket.Write("/lumenet/stack/play")).Single().ToString());
        Assert.Equal(
            "/lumenet/fader/master 0.5 255 true nil impulse \"go\"",
            OscPacket.Read(OscPacket.Write("/lumenet/fader/master", 0.5f, 255, true, null, OscMessage.Impulse, "go")).Single().ToString());
        Assert.Equal("/a (3 bytes)", OscPacket.Read(OscPacket.Write("/a", new byte[] { 1, 2, 3 })).Single().ToString());
    }

    [Fact]
    public void TextsAreReadAsUtf8()
    {
        byte[] packet = OscPacket.Write("/a", "Příliš");
        OscMessage message = Assert.Single(OscPacket.Read(packet));
        Assert.Equal("Příliš", message.Arguments[0]);
        Assert.Equal(0, packet.Length % 4);
        Assert.Equal(4 + 4 + ((Encoding.UTF8.GetByteCount("Příliš") / 4) + 1) * 4, packet.Length);
    }
}
