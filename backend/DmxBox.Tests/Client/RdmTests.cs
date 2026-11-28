namespace DmxBox.Tests.Client;

/// <summary>
/// RDM through the client: the simulator has three lights with RDM on its line (firmware/sim/rdm_lights.c),
/// which the box finds by itself and asks for the client, as a dmxbox does on its real line.
/// </summary>
public sealed class RdmTests
{
    private static readonly RdmUid First = new(0x7FF1, 1);

    // The box searches its line when it starts; the list is complete once it no longer searches.
    private static RdmLightList LightsWhenFound(DmxBoxClient box)
    {
        RdmLightList list = box.RdmLights();
        for (int i = 0; i < 100 && list.Searching; i++)
        {
            Thread.Sleep(20);
            list = box.RdmLights();
        }

        Assert.False(list.Searching);
        return list;
    }

    [Fact]
    public void TheBoxFindsTheLightsOnItsLine()
    {
        using var sim = Simulator.Start();
        using var box = sim.CreateClient();

        RdmLightList list = LightsWhenFound(box);

        Assert.Equal([new RdmUid(0x7FF1, 1), new RdmUid(0x7FF1, 2), new RdmUid(0x7FF1, 3)], list.Lights.Order());
    }

    [Fact]
    public void AnotherSearchFindsThemAgain()
    {
        using var sim = Simulator.Start();
        using var box = sim.CreateClient();
        LightsWhenFound(box);

        box.RdmDiscover();

        Assert.Equal(3, LightsWhenFound(box).Lights.Count);
    }

    [Fact]
    public void GetReadsALightsParameter()
    {
        using var sim = Simulator.Start();
        using var box = sim.CreateClient();

        RdmResponse answer = box.RdmRequest(First, RdmCommandClass.Get, RdmParameter.DeviceInfo);

        Assert.Equal(RdmResponseType.Ack, answer.Type);
        Assert.Equal(0x21, answer.CommandClass);
        Assert.Equal(RdmParameter.DeviceInfo, answer.Parameter);
        RdmDeviceInfo info = Assert.IsType<RdmDeviceInfo>(RdmDeviceInfo.Parse(answer.Data));
        Assert.Equal(1, info.StartAddress);
        Assert.Equal(4, info.Footprint);
        Assert.Equal((1, 2), (info.Mode, info.Modes));
    }

    [Fact]
    public void SetChangesTheLight()
    {
        using var sim = Simulator.Start();
        using var box = sim.CreateClient();

        RdmResponse set = box.RdmRequest(First, RdmCommandClass.Set, RdmParameter.DmxStartAddress, [0x01, 0x2C]);
        RdmResponse get = box.RdmRequest(First, RdmCommandClass.Get, RdmParameter.DmxStartAddress);

        Assert.Equal((RdmResponseType.Ack, 0x31), (set.Type, set.CommandClass));
        Assert.Equal([0x01, 0x2C], get.Data);   // 300
    }

    [Fact]
    public void ARefusalComesBackWithItsReason()
    {
        using var sim = Simulator.Start();
        using var box = sim.CreateClient();

        RdmResponse unknown = box.RdmRequest(First, RdmCommandClass.Get, 0x7FFF);
        RdmResponse tooFar = box.RdmRequest(First, RdmCommandClass.Set, RdmParameter.DmxStartAddress, [0x02, 0x01]);   // 513

        Assert.Equal((RdmResponseType.NackReason, (ushort?)0x0000), (unknown.Type, unknown.NackReason));
        Assert.Equal((RdmResponseType.NackReason, (ushort?)0x0006), (tooFar.Type, tooFar.NackReason));
    }

    [Fact]
    public void ALightThatIsNotThereDoesNotAnswer()
    {
        using var sim = Simulator.Start();
        using var box = sim.CreateClient();

        var e = Assert.Throws<DmxBoxException>(() => box.RdmRequest(new RdmUid(0x7FF1, 99), RdmCommandClass.Get, RdmParameter.DeviceInfo));

        Assert.Equal("RDM_TIMEOUT", e.StatusName);
    }

    [Fact]
    public void RequestsAnsweredLaterSurviveLostPackets()
    {
        // 30 % of the packets get lost each way: the client asks again, the box gives the answer it
        // keeps, and never sends the request on the line twice. 20 attempts, as in
        // ReliableOverLossyNetwork: about one run in 10 000 fails by bad luck alone.
        using var sim = Simulator.Start(drop: 0.3);
        using var box = sim.CreateClient(TimeSpan.FromMilliseconds(50), attempts: 20);

        for (int i = 0; i < 10; i++)
        {
            byte[] label = System.Text.Encoding.ASCII.GetBytes($"Light {i}");
            Assert.Equal(RdmResponseType.Ack, box.RdmRequest(First, RdmCommandClass.Set, RdmParameter.DeviceLabel, label).Type);
            Assert.Equal(label, box.RdmRequest(First, RdmCommandClass.Get, RdmParameter.DeviceLabel).Data);
        }
    }

    [Theory]
    [InlineData("7FF1:00000001", 0x7FF1, 1u)]
    [InlineData("7ff1:0000002a", 0x7FF1, 42u)]
    [InlineData(" 4C4D:12345678 ", 0x4C4D, 0x12345678u)]
    public void IdsAreReadAsRdmToolsWriteThem(string text, int maker, uint device)
    {
        Assert.True(RdmUid.TryParse(text, out RdmUid uid));
        Assert.Equal(new RdmUid((ushort)maker, device), uid);
        Assert.Equal(text.Trim().ToUpperInvariant(), uid.ToString());
        Assert.Equal(uid, RdmUid.FromBytes(uid.ToBytes()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("7FF1")]
    [InlineData("7FF1:")]
    [InlineData("17FF1:00000001")]
    [InlineData("7FF1:000000001")]
    [InlineData("XYZ1:00000001")]
    public void OtherTextsAreNoIds(string text)
    {
        Assert.False(RdmUid.TryParse(text, out _));
    }
}
