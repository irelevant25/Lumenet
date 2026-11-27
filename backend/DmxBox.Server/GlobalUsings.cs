// Every folder of the desk program is a namespace of its own (DmxBox.Server.Sounds, DmxBox.Server.Midi ...),
// and each of them is known in every file, as when they were all one: a file names the types of another
// folder without a using of its own. Lumenet.exe compiles the files of this project, this one too.
global using DmxBox.Server.Api;
global using DmxBox.Server.Boxes;
global using DmxBox.Server.Core;
global using DmxBox.Server.Effects;
global using DmxBox.Server.Import;
global using DmxBox.Server.Lan;
global using DmxBox.Server.Midi;
global using DmxBox.Server.Osc;
global using DmxBox.Server.Outputs;
global using DmxBox.Server.Projects;
global using DmxBox.Server.Rdm;
global using DmxBox.Server.Sequences;
global using DmxBox.Server.Sounds;
global using DmxBox.Server.Stack;
global using DmxBox.Server.Stage;
global using DmxBox.Server.Update;
