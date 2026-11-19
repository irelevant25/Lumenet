using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace DmxBox.Tests;

/// <summary>
/// The server through its front door: UIs connect to the hub with WebSockets and get the same
/// JSON a browser would. These tests are the contract the UI is written against: the names of
/// the methods and messages, the shape of the JSON, who is told what, and how errors look.
/// </summary>
public sealed class HubTests
{
    private static readonly JsonSerializerOptions AsOnTheWire = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // The properties of the desk on the wire, in their order.
    private static readonly string[] DeskProperties =
    [
        "channels", "memoryLevels", "memoryStored", "master", "blackout", "channelNotes", "memoryNotes", "independents",
    ];

    [Fact]
    public async Task HelloDescribesTheDeskTheBoxAndTheProject()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();

        JsonElement hello = await ui.InvokeAsync("Hello");

        Assert.Equal(["version", "desk", "box", "project"], NamesIn(hello));
        Assert.Equal(TestServer.Version, hello.GetProperty("version").GetString());

        JsonElement desk = hello.GetProperty("desk");
        Assert.Equal(DeskProperties, NamesIn(desk));
        Assert.Equal(new int[512], Numbers(desk.GetProperty("channels")));
        Assert.Equal(new int[128], Numbers(desk.GetProperty("memoryLevels")));
        Assert.Equal(new bool[128], Flags(desk.GetProperty("memoryStored")));
        Assert.Equal(255, desk.GetProperty("master").GetInt32());
        Assert.False(desk.GetProperty("blackout").GetBoolean());
        Assert.Equal(Enumerable.Repeat("", 512), Texts(desk.GetProperty("channelNotes")));
        Assert.Equal(Enumerable.Repeat("", 128), Texts(desk.GetProperty("memoryNotes")));
        Assert.Equal(Enumerable.Range(1, 16), Numbers(desk.GetProperty("independents")));

        Assert.Equal(
            """{"connected":false,"host":"pretend-box","firmware":null}""",
            hello.GetProperty("box").GetRawText());
        Assert.Equal(
            """{"name":"Untitled","path":null,"unsaved":false}""",
            hello.GetProperty("project").GetRawText());
    }

    [Fact]
    public async Task FaderMovesReachTheOtherUisButNotTheOneThatMoved()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient mover = await server.ConnectAsync();
        await using HubClient watcher = await server.ConnectAsync();

        await mover.InvokeAsync("SetChannel", 5, 200);
        await mover.InvokeAsync("SetMemoryLevel", 3, 100);
        await mover.InvokeAsync("SetMaster", 128);
        await mover.InvokeAsync("SetBlackout", true);   // unlike the faders, this goes to everybody

        await AssertNextAsync(watcher, "ChannelChanged", "[5,200]");
        await AssertNextAsync(watcher, "MemoryLevelChanged", "[3,100]");
        await AssertNextAsync(watcher, "MasterChanged", "[128]");
        await AssertNextAsync(watcher, "BlackoutChanged", "[true]");

        // The first thing the mover hears is the blackout: its own fader moves did not come back.
        await AssertNextAsync(mover, "BlackoutChanged", "[true]");

        JsonElement desk = (await watcher.InvokeAsync("Hello")).GetProperty("desk");
        Assert.Equal(200, desk.GetProperty("channels")[4].GetInt32());
        Assert.Equal(100, desk.GetProperty("memoryLevels")[2].GetInt32());
        Assert.Equal(128, desk.GetProperty("master").GetInt32());
        Assert.True(desk.GetProperty("blackout").GetBoolean());
    }

    [Fact]
    public async Task FaderMoveSentWithoutWaitingForAnAnswerWorksTheSame()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient mover = await server.ConnectAsync();
        await using HubClient watcher = await server.ConnectAsync();

        await mover.SendAsync("SetChannel", 7, 70);

        await AssertNextAsync(watcher, "ChannelChanged", "[7,70]");
    }

    [Fact]
    public async Task MistakesComeBackAsHubExceptionWithAPlainMessage()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();

        await AssertRefusedAsync("The channel must be from 1 to 512, not 600.", ui, "SetChannel", 600, 1);
        await AssertRefusedAsync("The level must be from 0 to 255, not 256.", ui, "SetChannel", 1, 256);
        await AssertRefusedAsync("The memory must be from 1 to 128, not 129.", ui, "SetMemoryLevel", 129, 0);
        await AssertRefusedAsync("The level must be from 0 to 255, not -1.", ui, "SetMaster", -1);
        await AssertRefusedAsync("The memory must be from 1 to 128, not 0.", ui, "SaveMemory", 0);
        await AssertRefusedAsync("Memory 5 is empty.", ui, "LoadMemory", 5);
        await AssertRefusedAsync("Memory 1 is empty.", ui, "CopyMemory", 1, 2);
        await AssertRefusedAsync("Memory 1 cannot be copied onto itself.", ui, "CopyMemory", 1, 1);
        await AssertRefusedAsync("The memory must be from 1 to 128, not 129.", ui, "DeleteMemory", 129);
        await AssertRefusedAsync("A note can have at most 24 characters.", ui, "SetChannelNote", 1, new string('a', 25));
        await AssertRefusedAsync("A note can have at most 24 characters.", ui, "SetMemoryNote", 1, new string('a', 25));
        await AssertRefusedAsync("A note cannot contain a tab, a line break or the like.", ui, "SetChannelNote", 1, "Front\tleft");
        await AssertRefusedAsync("The channel must be from 1 to 512, not 513.", ui, "SetChannelNote", 513, "note");
        await AssertRefusedAsync("The memory must be from 1 to 128, not 0.", ui, "SetMemoryNote", 0, "note");
        await AssertRefusedAsync("The independent must be from 1 to 16, not 17.", ui, "SetIndependent", 17, 1);
        await AssertRefusedAsync("The independent must be from 1 to 16, not 0.", ui, "SetIndependent", 0, 1);
        await AssertRefusedAsync("The channel must be from 1 to 512, not 513.", ui, "SetIndependent", 1, 513);
        await AssertRefusedAsync("This project has no file yet. Save it under a name.", ui, "SaveProject", (object?)null);
        await AssertRefusedAsync("A project name cannot contain the character \"/\".", ui, "SaveProject", "a/b");
        await AssertRefusedAsync("A project needs a name.", ui, "LoadProject", "");
        await AssertRefusedAsync("There is no project \"nothing\".", ui, "LoadProject", "nothing");
    }

    [Fact]
    public async Task RefusedRequestTellsNobodyAnything()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        await using HubClient other = await server.ConnectAsync();

        await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync("SetChannel", 600, 1));
        await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync("LoadMemory", 5));
        await ui.InvokeAsync("SetBlackout", true);

        // The blackout is the first thing either of them hears.
        await AssertNextAsync(ui, "BlackoutChanged", "[true]");
        await AssertNextAsync(other, "BlackoutChanged", "[true]");
    }

    [Fact]
    public async Task ArgumentOfTheWrongKindIsRefusedWithoutDetails()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();

        // A level has to be a whole number. This is a fault in the UI, not a mistake of the
        // user, so SignalR's own short error comes back, without a "HubException".
        var error = await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync("SetChannel", 1, 12.5));

        Assert.DoesNotContain("HubException", error.Message);
    }

    [Fact]
    public async Task MemoryFunctionsSendTheWholeDeskToEveryUi()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        await using HubClient other = await server.ConnectAsync();
        HubClient[] both = [ui, other];

        await ui.InvokeAsync("SetChannel", 1, 9);
        await AssertNextAsync(other, "ChannelChanged", "[1,9]");

        await ui.InvokeAsync("SaveMemory", 2);
        foreach (HubClient client in both)
        {
            JsonElement desk = await NextDeskAsync(client);
            Assert.Equal(9, desk.GetProperty("channels")[0].GetInt32());
            Assert.True(desk.GetProperty("memoryStored")[1].GetBoolean());

            // The first change of a memory also marks the project as unsaved.
            await AssertNextAsync(client, "ProjectChanged", """[{"name":"Untitled","path":null,"unsaved":true}]""");
        }

        await ui.InvokeAsync("Clear");
        foreach (HubClient client in both)
        {
            JsonElement desk = await NextDeskAsync(client);
            Assert.Equal(0, desk.GetProperty("channels")[0].GetInt32());
            Assert.True(desk.GetProperty("memoryStored")[1].GetBoolean());
        }

        await ui.InvokeAsync("LoadMemory", 2);
        foreach (HubClient client in both)
            Assert.Equal(9, (await NextDeskAsync(client)).GetProperty("channels")[0].GetInt32());

        await ui.InvokeAsync("CopyMemory", 2, 3);
        foreach (HubClient client in both)
            Assert.True((await NextDeskAsync(client)).GetProperty("memoryStored")[2].GetBoolean());

        // Every NextDeskAsync above also showed that no second "ProjectChanged" came in
        // between: the project was unsaved already, so there was nothing new to tell.
        await ui.InvokeAsync("DeleteMemory", 2);
        foreach (HubClient client in both)
            Assert.False((await NextDeskAsync(client)).GetProperty("memoryStored")[1].GetBoolean());
    }

    [Fact]
    public async Task DeskChangedCarriesTheWholeDesk()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        await ui.InvokeAsync("SetMemoryLevel", 128, 44);
        await ui.InvokeAsync("SetMaster", 33);

        await ui.InvokeAsync("SaveMemory", 128);

        JsonElement desk = await NextDeskAsync(ui);
        Assert.Equal(DeskProperties, NamesIn(desk));
        Assert.Equal(512, Numbers(desk.GetProperty("channels")).Length);
        Assert.Equal(44, Numbers(desk.GetProperty("memoryLevels"))[127]);
        Assert.True(Flags(desk.GetProperty("memoryStored"))[127]);
        Assert.Equal(33, desk.GetProperty("master").GetInt32());
        Assert.False(desk.GetProperty("blackout").GetBoolean());
    }

    [Fact]
    public async Task GetMemoriesGivesTheLookOfEveryStoredMemoryByItsNumber()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();

        JsonElement none = await ui.InvokeAsync("GetMemories");
        Assert.Empty(none.EnumerateObject());

        await ui.InvokeAsync("SetChannel", 2, 200);
        await ui.InvokeAsync("SaveMemory", 5);

        JsonElement looks = await ui.InvokeAsync("GetMemories");
        Assert.Equal(["5"], NamesIn(looks));
        int[] look = Numbers(looks.GetProperty("5"));
        Assert.Equal(512, look.Length);
        Assert.Equal([0, 200, 0], look[..3]);
    }

    [Fact]
    public async Task NotesReachTheOtherUisButNotTheOneThatTyped()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient writer = await server.ConnectAsync();
        await using HubClient watcher = await server.ConnectAsync();
        string unsavedProject = """[{"name":"Untitled","path":null,"unsaved":true}]""";

        await writer.InvokeAsync("SetChannelNote", 5, "  Front left ");
        await writer.InvokeAsync("SetMemoryNote", 3, "Blue wash");
        await writer.InvokeAsync("SetChannelNote", 5, "");
        await writer.InvokeAsync("SetBlackout", true);

        // The others get the text as it was stored: without the spaces at its ends. The first
        // note also makes the project unsaved; after that there is nothing new to tell about it.
        await AssertNextAsync(watcher, "ChannelNoteChanged", """[5,"Front left"]""");
        await AssertNextAsync(watcher, "ProjectChanged", unsavedProject);
        await AssertNextAsync(watcher, "MemoryNoteChanged", """[3,"Blue wash"]""");
        await AssertNextAsync(watcher, "ChannelNoteChanged", """[5,""]""");
        await AssertNextAsync(watcher, "BlackoutChanged", "[true]");

        // The writer hears about the project like everybody, but its own notes do not come back.
        await AssertNextAsync(writer, "ProjectChanged", unsavedProject);
        await AssertNextAsync(writer, "BlackoutChanged", "[true]");
    }

    [Fact]
    public async Task NoteSentWithoutWaitingForAnAnswerWorksTheSame()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient writer = await server.ConnectAsync();
        await using HubClient watcher = await server.ConnectAsync();

        await writer.SendAsync("SetMemoryNote", 128, "Finale");

        await AssertNextAsync(watcher, "MemoryNoteChanged", """[128,"Finale"]""");
    }

    [Fact]
    public async Task SameNoteAgainDoesNotMarkTheProjectUnsaved()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        await ui.InvokeAsync("SetChannelNote", 1, "Front left");
        await NextAsync(ui, "ProjectChanged");
        await ui.InvokeAsync("SaveProject", "show");
        await NextAsync(ui, "ProjectChanged");

        await ui.InvokeAsync("SetChannelNote", 1, "Front left");
        await ui.InvokeAsync("SetChannelNote", 1, " Front left ");
        await ui.InvokeAsync("SetBlackout", true);

        // The project is still saved: the next message is the blackout.
        await AssertNextAsync(ui, "BlackoutChanged", "[true]");
        Assert.False((await ui.InvokeAsync("Hello")).GetProperty("project").GetProperty("unsaved").GetBoolean());
    }

    [Fact]
    public async Task DeskCarriesTheNotesAndClearLeavesThem()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        await ui.InvokeAsync("SetChannelNote", 512, "Smoke machine");
        await ui.InvokeAsync("SetMemoryNote", 1, "Blue wash");
        await NextAsync(ui, "ProjectChanged");

        JsonElement hello = (await ui.InvokeAsync("Hello")).GetProperty("desk");
        await ui.InvokeAsync("Clear");
        JsonElement cleared = await NextDeskAsync(ui);

        foreach (JsonElement desk in new[] { hello, cleared })
        {
            string[] channelNotes = Texts(desk.GetProperty("channelNotes"));
            string[] memoryNotes = Texts(desk.GetProperty("memoryNotes"));
            Assert.Equal(512, channelNotes.Length);
            Assert.Equal(128, memoryNotes.Length);
            Assert.Equal("Smoke machine", channelNotes[511]);
            Assert.Equal("", channelNotes[0]);
            Assert.Equal("Blue wash", memoryNotes[0]);
        }
    }

    [Fact]
    public async Task LoadedProjectBringsItsNotes()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        await ui.InvokeAsync("SetChannelNote", 1, "Front left");
        await ui.InvokeAsync("SaveProject", "show");
        await ui.InvokeAsync("SetChannelNote", 1, "changed");
        await ui.InvokeAsync("SetMemoryNote", 2, "not in the file");
        for (int message = 0; message < 3; message++)
            await NextAsync(ui, "ProjectChanged");   // unsaved, saved, unsaved again

        await ui.InvokeAsync("LoadProject", "show");

        JsonElement desk = await NextDeskAsync(ui);
        Assert.Equal("Front left", desk.GetProperty("channelNotes")[0].GetString());
        Assert.Equal("", desk.GetProperty("memoryNotes")[1].GetString());
        await AssertNextAsync(ui, "ProjectChanged", $"[{Project("show", Path.Combine(server.ProjectsFolder, "show.json"), unsaved: false)}]");
    }

    [Fact]
    public async Task IndependentKeyReachesTheOtherUisButNotTheOneThatBoundIt()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient binder = await server.ConnectAsync();
        await using HubClient watcher = await server.ConnectAsync();
        string unsavedProject = """[{"name":"Untitled","path":null,"unsaved":true}]""";

        await binder.InvokeAsync("SetIndependent", 3, 300);
        await binder.InvokeAsync("SetIndependent", 16, 512);
        await binder.InvokeAsync("SetIndependent", 3, 300);   // the same again: no change for the project
        await binder.InvokeAsync("SetBlackout", true);

        await AssertNextAsync(watcher, "IndependentChanged", "[3,300]");
        await AssertNextAsync(watcher, "ProjectChanged", unsavedProject);
        await AssertNextAsync(watcher, "IndependentChanged", "[16,512]");
        await AssertNextAsync(watcher, "IndependentChanged", "[3,300]");
        await AssertNextAsync(watcher, "BlackoutChanged", "[true]");

        // The binder hears about the project like everybody, but not about its own keys.
        await AssertNextAsync(binder, "ProjectChanged", unsavedProject);
        await AssertNextAsync(binder, "BlackoutChanged", "[true]");

        int[] independents = Numbers((await watcher.InvokeAsync("Hello")).GetProperty("desk").GetProperty("independents"));
        Assert.Equal(16, independents.Length);
        Assert.Equal(300, independents[2]);
        Assert.Equal(512, independents[15]);
        Assert.Equal(1, independents[0]);
    }

    [Fact]
    public async Task SameChannelAgainDoesNotMarkTheProjectUnsaved()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();

        await ui.InvokeAsync("SetIndependent", 5, 5);   // where key 5 is from the start
        await ui.InvokeAsync("SetBlackout", true);

        await AssertNextAsync(ui, "BlackoutChanged", "[true]");
        Assert.False((await ui.InvokeAsync("Hello")).GetProperty("project").GetProperty("unsaved").GetBoolean());
    }

    [Fact]
    public async Task ClearLeavesTheIndependentKeysAndLoadedProjectBringsItsOwn()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        await ui.InvokeAsync("SetIndependent", 1, 100);
        await ui.InvokeAsync("SaveProject", "show");
        await ui.InvokeAsync("SetIndependent", 1, 200);
        for (int message = 0; message < 3; message++)
            await NextAsync(ui, "ProjectChanged");   // unsaved, saved, unsaved again

        await ui.InvokeAsync("Clear");
        Assert.Equal(200, (await NextDeskAsync(ui)).GetProperty("independents")[0].GetInt32());

        await ui.InvokeAsync("LoadProject", "show");
        Assert.Equal(100, (await NextDeskAsync(ui)).GetProperty("independents")[0].GetInt32());
    }

    [Fact]
    public async Task NewProjectCleansTheDeskForEveryUi()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        await using HubClient other = await server.ConnectAsync();
        await ui.InvokeAsync("SetChannel", 1, 200);
        await ui.InvokeAsync("SetMemoryLevel", 1, 150);
        await ui.InvokeAsync("SetMaster", 100);
        await ui.InvokeAsync("SetChannelNote", 1, "Front left");
        await ui.InvokeAsync("SetIndependent", 1, 300);
        await ui.InvokeAsync("SaveMemory", 1);
        await ui.InvokeAsync("SaveProject", "show");
        await ui.InvokeAsync("SetBlackout", true);
        await NextAsync(ui, "ProjectChanged");     // unsaved, when the note was written
        await NextAsync(ui, "DeskChanged");        // the memory was saved
        await NextAsync(ui, "ProjectChanged");     // saved as "show"
        await NextAsync(ui, "BlackoutChanged");
        while ((await other.NextMessageAsync()).Name != "BlackoutChanged")
        {
            // The other UI has heard all of the above too; what counts starts after the blackout.
        }

        JsonElement answer = await ui.InvokeAsync("NewProject");

        string newProject = """{"name":"Untitled","path":null,"unsaved":false}""";
        Assert.Equal(newProject, answer.GetRawText());
        foreach (HubClient client in new[] { ui, other })
        {
            JsonElement desk = await NextDeskAsync(client);
            Assert.Equal(new int[512], Numbers(desk.GetProperty("channels")));
            Assert.Equal(new int[128], Numbers(desk.GetProperty("memoryLevels")));
            Assert.Equal(new bool[128], Flags(desk.GetProperty("memoryStored")));
            Assert.Equal(Enumerable.Repeat("", 512), Texts(desk.GetProperty("channelNotes")));
            Assert.Equal(Enumerable.Range(1, 16), Numbers(desk.GetProperty("independents")));
            Assert.Equal(100, desk.GetProperty("master").GetInt32());    // master and blackout stay
            Assert.True(desk.GetProperty("blackout").GetBoolean());
            await AssertNextAsync(client, "ProjectChanged", $"[{newProject}]");
        }

        Assert.True(File.Exists(Path.Combine(server.ProjectsFolder, "show.json")));   // the saved project stays on the disk
    }

    [Fact]
    public async Task NewProjectOnAnUntouchedDeskTellsOnlyTheDesk()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();

        await ui.InvokeAsync("NewProject");
        await ui.InvokeAsync("SetBlackout", true);

        // The project was "Untitled", without a file and saved before, and still is: only the
        // desk is sent, and the next message is the blackout.
        await NextDeskAsync(ui);
        await AssertNextAsync(ui, "BlackoutChanged", "[true]");
    }

    [Fact]
    public async Task ProjectIsSavedListedAndLoaded()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        await using HubClient other = await server.ConnectAsync();
        string path = Path.Combine(server.ProjectsFolder, "show.json");
        string savedProject = Project("show", path, unsaved: false);
        string changedProject = Project("show", path, unsaved: true);

        await ui.InvokeAsync("SaveMemory", 1);
        await NextDeskAsync(other);
        await AssertNextAsync(other, "ProjectChanged", """[{"name":"Untitled","path":null,"unsaved":true}]""");

        // Save under a name: the answer and the message to everybody carry the same project.
        JsonElement saved = await ui.InvokeAsync("SaveProject", "show");
        Assert.Equal(savedProject, saved.GetRawText());
        await AssertNextAsync(other, "ProjectChanged", $"[{savedProject}]");
        Assert.True(File.Exists(path));

        // The list of projects.
        JsonElement list = await ui.InvokeAsync("GetProjects");
        Assert.Equal(["folder", "projects"], NamesIn(list));
        Assert.Equal(server.ProjectsFolder, list.GetProperty("folder").GetString());
        JsonElement entry = Assert.Single(list.GetProperty("projects").EnumerateArray());
        Assert.Equal(["name", "path", "modified"], NamesIn(entry));
        Assert.Equal("show", entry.GetProperty("name").GetString());
        Assert.Equal(path, entry.GetProperty("path").GetString());
        AssertIsoTimeOfJustNow(entry.GetProperty("modified").GetString()!);

        // A change after the save, and a save without a name to the same file.
        await ui.InvokeAsync("DeleteMemory", 1);
        await NextDeskAsync(other);
        await AssertNextAsync(other, "ProjectChanged", $"[{changedProject}]");
        JsonElement savedAgain = await ui.InvokeAsync("SaveProject", (object?)null);
        Assert.Equal(savedProject, savedAgain.GetRawText());
        await AssertNextAsync(other, "ProjectChanged", $"[{savedProject}]");

        // Load: the memories of the file replace those of the desk.
        await ui.InvokeAsync("SaveMemory", 5);
        await NextDeskAsync(other);
        await AssertNextAsync(other, "ProjectChanged", $"[{changedProject}]");
        JsonElement loaded = await ui.InvokeAsync("LoadProject", "show");
        Assert.Equal(savedProject, loaded.GetRawText());
        Assert.Equal(new bool[128], Flags((await NextDeskAsync(other)).GetProperty("memoryStored")));
        await AssertNextAsync(other, "ProjectChanged", $"[{savedProject}]");
    }

    [Fact]
    public async Task SavingAgainWithoutAChangeTellsNobody()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        await ui.InvokeAsync("SaveProject", "show");
        await NextAsync(ui, "ProjectChanged");

        await ui.InvokeAsync("SaveProject", "show");
        await ui.InvokeAsync("SetBlackout", true);

        // Name, file and "unsaved" are what they were: the next message is the blackout.
        await AssertNextAsync(ui, "BlackoutChanged", "[true]");
    }

    [Fact]
    public async Task BoxComingAndGoingIsToldToTheUis()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        string connected = """{"connected":true,"host":"pretend-box","firmware":"1.0"}""";

        server.Box.PlugIn();
        await AssertNextAsync(ui, "BoxChanged", $"[{connected}]");
        Assert.Equal(connected, (await ui.InvokeAsync("Hello")).GetProperty("box").GetRawText());

        server.Box.Unplug();
        await AssertNextAsync(ui, "BoxChanged", """[{"connected":false,"host":"pretend-box","firmware":null}]""");
    }

    [Fact]
    public async Task DeskOfTheUiReachesTheBox()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        server.Box.PlugIn();
        await NextAsync(ui, "BoxChanged");

        await ui.InvokeAsync("SetChannel", 1, 255);
        await ui.InvokeAsync("SetMaster", 128);
        await Wait.UntilAsync(() => server.Box.Levels[0] == 128, "The fader move did not reach the box.");

        await ui.InvokeAsync("SetBlackout", true);
        await Wait.UntilAsync(() => server.Box.BlackoutOn, "The blackout did not reach the box.");
    }

    [Fact]
    public async Task BlackoutButtonOnTheBoxReachesTheUis()
    {
        await using TestServer server = await TestServer.StartAsync();
        await using HubClient ui = await server.ConnectAsync();
        server.Box.PlugIn();
        await NextAsync(ui, "BoxChanged");

        server.Box.PressBlackoutButton();

        await AssertNextAsync(ui, "BlackoutChanged", "[true]");
        Assert.True((await ui.InvokeAsync("Hello")).GetProperty("desk").GetProperty("blackout").GetBoolean());
    }

    // The names of an object's properties, in the order they have in the JSON.
    private static string[] NamesIn(JsonElement element)
    {
        return element.EnumerateObject().Select(property => property.Name).ToArray();
    }

    // Fails if the element is not a list of whole numbers: levels must not arrive as base64 text.
    private static int[] Numbers(JsonElement list)
    {
        return list.EnumerateArray().Select(item => item.GetInt32()).ToArray();
    }

    private static bool[] Flags(JsonElement list)
    {
        return list.EnumerateArray().Select(item => item.GetBoolean()).ToArray();
    }

    // Fails if an item is not a text: a fader without a note has to arrive as "", not as null.
    private static string[] Texts(JsonElement list)
    {
        return list.EnumerateArray().Select(TextOf).ToArray();
    }

    private static string TextOf(JsonElement item)
    {
        Assert.Equal(JsonValueKind.String, item.ValueKind);
        return item.GetString()!;
    }

    // A project the way it is on the wire. SignalR writes characters like "+" or letters
    // beyond ASCII as they are; a path in somebody's user folder may have them.
    private static string Project(string name, string path, bool unsaved)
    {
        return JsonSerializer.Serialize(new { name, path, unsaved }, AsOnTheWire);
    }

    private static async Task<HubMessage> NextAsync(HubClient ui, string expectedName)
    {
        HubMessage message = await ui.NextMessageAsync();
        Assert.Equal(expectedName, message.Name);
        return message;
    }

    // Checks the next message of the server: its name, and its arguments as the JSON on the wire.
    private static async Task AssertNextAsync(HubClient ui, string expectedName, string expectedArguments)
    {
        HubMessage message = await NextAsync(ui, expectedName);
        string arguments = "[" + string.Join(",", message.Arguments.Select(argument => argument.GetRawText())) + "]";
        Assert.Equal(expectedArguments, arguments);
    }

    private static async Task<JsonElement> NextDeskAsync(HubClient ui)
    {
        HubMessage message = await NextAsync(ui, "DeskChanged");
        return Assert.Single(message.Arguments);
    }

    // SignalR puts a sentence of its own in front of the message. The UI cuts it off after "HubException: ".
    private static async Task AssertRefusedAsync(
        string expectedMessage, HubClient ui, string method, params object?[] arguments)
    {
        var error = await Assert.ThrowsAsync<HubCallException>(() => ui.InvokeAsync(method, arguments));
        Assert.Equal(
            $"An unexpected error occurred invoking '{method}' on the server. HubException: {expectedMessage}",
            error.Message);
    }

    // ISO 8601 in universal time, like "2026-11-18T15:04:05.1234567Z".
    private static void AssertIsoTimeOfJustNow(string text)
    {
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?Z$", text);
        DateTimeOffset time = DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);
        Assert.InRange(time, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
    }
}
