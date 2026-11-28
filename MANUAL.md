# Lumenet manual

How to use the Lumenet desk: what every view, key and setting does. The [README](README.md) says what Lumenet
is and how to get it; [DEV.md](DEV.md) is for building the box and changing the code.

## Starting it

* **Lumenet.exe** (Windows 10 and 11, 64-bit): download it from the [Releases](../../releases) page and start
  it, see [Lumenet.exe](#lumenetexe) below.
* **From the sources** (the .NET 10 SDK and Node.js 22): `run.cmd` starts the desk program and the page; then
  open http://localhost:4200. Ctrl+C stops both. The top bar shows the version a release of the same commit
  has (0.1 and the number of commits, as Lumenet.exe does); without git it shows 0.1.0.

The desk also works without a box: it shows "Outputs connected: 0" and sends the whole state as soon as a box
answers. Besides the dmxbox it plays on Art-Net nodes, as sACN and on DMX USB interfaces of the kind of the
Enttec DMX USB Pro, see [Universes and outputs](#universes-and-outputs). To try them without the hardware,
`sim.cmd` (from the sources) starts a simulated box, Art-Net node, sACN receiver or USB interface, and the
desk finds it as if it were plugged in: see [DEV.md](DEV.md#simulated-devices-simcmd).

### The first start

The first time the page opens on a device (the PC, a phone, a tablet), it asks what kind of shows you run:

| Setup | The views in the top bar | Rows on Main |
|---|---|---|
| **Theatre**: light and sound in one stack of cues, played with GO | Main, Stack, Stage, Sounds | sounds |
| **Theatre with moving lights**: the same, with fixtures, effects, memories and sequences | all but Notes and MIDI | effects, sounds |
| **Concert**: lights played live to the music | Main, Sequences, Stage, Fixtures, Effects, MIDI | effects |
| **Everything** | all but Notes | effects, sounds |

Settings is always there too, and Notes is in none of them: Settings shows it with a tick, for a show whose
notes are written there. **Use this setup** sets them on this device, and then offers the first steps:
**Outputs** (the Outputs tab of Settings, where the dmxbox or another output is set up), **Lights** (the Stage view, where the
lights are put on the plan) and **Examples** (the Load window with the example shows). **Later** changes
nothing. Either way the device is not asked again; **Setup…** in Settings (General, beside Views in the top bar)
offers the setups any time, and Settings changes each view and row on its own.

## Lumenet.exe

The whole desk in one file, for Windows 10 and 11 (64-bit), with nothing to install: the desk program, the
page and the examples are inside it, and so is .NET. Every release on the [Releases](../../releases) page has it.

![The menu of Lumenet's icon in the taskbar: Open Lumenet, Show the log and Exit](docs/screenshots/tray-menu.png)

![The log window: what the desk program writes, a warning in yellow](docs/screenshots/log-window.png)

* Started, it opens the page in the browser (at http://localhost:5080, the desk program's own address) and
  sits in the notification area of the taskbar; Windows 11 may put the icon among the hidden ones, under ^. A
  click on the icon opens the page again. Its menu has **Open Lumenet**, **Show the log** and **Exit**.
* **Show the log** shows what the desk program writes, as its console would (there is no console): warnings
  in yellow, errors in red. The window can be closed (Esc or ✕) as often as wanted; only Exit ends Lumenet.
  The page itself has no output of its own here: it is built into the exe and comes from the desk program.
* Started again while it runs, Lumenet only opens the page. While the desk of `run.cmd` runs, it says that
  the port is taken and does not start: only one desk at a time may talk to the box.
* It keeps everything where the desk of `run.cmd` keeps it (the projects in My Documents, the autosave and
  the LAN password in `%LOCALAPPDATA%\Lumenet`), so both work on the same things. The examples are written to
  `%LOCALAPPDATA%\Lumenet\examples` at every start. Settings and the keyboard belong to the browser and the
  address: those of http://localhost:4200 are not those of http://localhost:5080 (Export and Import take
  them over).
* Settings are given as for the desk program, on the command line (`Lumenet.exe --Box:Host=127.0.0.1`) or
  in an `appsettings.json` beside the exe; `--Tray:OpenPage=false` starts it without opening the page.
* **Updates:** the About tab of Settings says when a newer release is out, and a press puts it in place of
  this exe and starts it (see [Updates](#updates)).
* The exe is not signed: Windows may say that it does not know it (More info, then Run anyway), and the
  firewall asks once whether it may be reached from the network (for LAN).

## What is on the page

The top bar has a link for each view: **Main**, **Memories**, **Sequences**, **Stack**, **Stage**, **Fixtures**,
**Effects**, **Sounds**, **MIDI** and **Settings**, and **Notes** once Settings shows it (in this order until Settings gives them another). Main is the desk itself. Memories shows what is stored, and changes it with a double click. Sequences, Effects and Stack
are for the things that run by themselves (see below), Stage shows the lights of the rig in their colors,
Fixtures plays them as fixtures, Notes holds the instructions for the show, MIDI sets up the MIDI controllers
of the PC (see [MIDI controllers](#midi-controllers)), Settings is
for how this device shows the desk and for its keyboard shortcuts (see [Settings](#settings) and
[Keyboard](#keyboard)). Under the name the
top bar says how many outputs play (Settings shows them, see [Universes and outputs](#universes-and-outputs));
beside it stand **LAN**, **Undo** and **Redo**, at the right the keys of the project. Every window of the page
(Load, Save, Import, Export and the others) closes with **✕** at its top right, with a press outside it, or
with Esc; a drag that begins inside it and ends outside leaves it open. The text of the page can be selected
and copied, as on any page; the keys, the faders, the links of the top bar and the things that are dragged
(the stage plan, a color field, a position pad) cannot, so that a press, a double click or a drag there never
selects text. At the right of Main the
**Stack** lies across the top, and under it stand four columns: the **Master** section (Black out and the
master fader with its level) over **Record**; the **General** section (Clear, Solo up, Solo down, Rate, the
tempo fader with the tempo on top, and Tempo under it); the **Sound** section (MUTE, LISTEN and the volume
of all sounds); and at the right edge the **Independents** (unless Settings shows none of
their keys). On a phone the master section, the general section, the sound section and the stack lie across
the top, one line each.

![The Main view on a PC: channel and memory faders, the master, general, record and stack sections, and the independents](docs/screenshots/main.png)

* **Channels:** two rows of faders (or one, as Settings says). The keys at the right choose which of the 512
  channels of a universe they show, in one column, as big as every key. When they do not fit into it, the
  column scrolls (with the wheel, a finger, the mouse held down on it and pulled, or the keyboard) and keeps
  the lit key in sight. In a project of several universes a key for each, U1, U2 and on, stands
  above the faders and chooses the universe (see [Universes and outputs](#universes-and-outputs)).
* **Memories:** one row of faders (or two), with page keys for the 128 memories. A memory holds a look:
  the levels the channel faders were set to at the moment it was saved. Its fader plays that look at the
  fader's level. For every channel the highest value wins, whether it comes from the channel fader or from
  a memory.
* **A memory fader moves the channel faders it uses.** A channel fader shows what goes out for its channel
  (before the master): its own level, or what the memories give it when that is higher. The slot is lit green
  up to the fader's own level and red above it, where a memory holds the channel. The own level is kept:
  bring the memory down, and the fader goes back to it. While a memory holds a channel up, its fader can
  still be moved; the handle stays where the memory holds it, and the green part shows what you set.
  Save mem stores the faders' own levels, not the part a memory adds.
* **Keys under the faders:** a press puts the fader to full, and back to zero when it is at full already (a
  fader somewhere between goes to full). It works the fader's own level: a channel that a memory holds at
  full still goes to full by its key. While a record key waits for a memory, the memory keys are for that
  (see Record); while a sequence is recorded, the channel keys put in steps. A channel key is lit when the
  channel is above zero. A memory key is half lit when something is stored in it and fully lit when its
  fader is up. A page key is fully lit for the page that is shown and half lit for another page with
  something on it.
* **Notes:** under each key are two lines to type into: which light or which look it is (11 characters per
  line). Each line stays where it was typed; the second can be used without the first.
* **Record** (under the master section) has Save mem, Edit mem, Rec seq, Copy and Del: press one, and the
  keys that can be used blink between their normal light and full.
  * **Save mem:** press a memory key, and the channel faders are stored there as a look.
  * **Edit mem:** press a memory that holds a look, and it is loaded into the channel faders. Change it, press
    Edit mem again, and it is stored back. A memory that holds a sequence does not blink: it is changed in
    the Sequences view.
  * **Rec seq** records a sequence key by key. Press it, then the memory for the sequence: an empty one, or
    one that holds a sequence already, which is then continued behind its last step. From then on every
    channel key, and every key of a memory that holds a look, puts in one more step: that channel or memory
    at full, with 1 second up, 1 second wait and 1 second down. A key shows the numbers of the steps it is in,
    small and yellow in its top left corner (the same channel can be used several times: 1, 5, 14). The keys
    of the Fixtures view put in steps of their fixture or group, in the color it shows at that moment.
    Press Rec seq again when the sequence is complete. A new sequence is played in a loop.
  * **Copy:** press the memory to copy from, then the memory to copy to. A sequence is copied like a look.
    The keys of the effects and of the sounds work the same way: an effect onto an effect, a sound onto a
    sound.
  * **Del:** press the memory to empty, whatever it holds; or an effect or a sound, to take it out.
  * Pressing the lit function again cancels it.
* **Clear** cancels an unfinished function and brings every channel fader, memory fader and effect fader to
  zero, so every effect stops too.
* **Black out** darkens every light but leaves the desk as it is; the key blinks while it is active. The
  attributes of the fixtures of the stage plan (a pan, a tilt, a wheel) stay as they are, so no moving head
  moves. It is the same on every output: a box, an Art-Net node, sACN, a USB interface. The blackout button on
  a box switches Black out as the key does: the box goes dark by itself at once, and within a tenth of a
  second the desk takes over, with pan and tilt where they were. While no desk program runs, the button
  darkens the box by itself, pan and tilt too.
* **Master** dims the light, not the attributes of the fixtures.
* **Solo up** and **Solo down** (white, in General under Clear) change what a key under a fader or of the
  independents does while it is held; so does its shortcut. Lighting one darkens the other.
  * **Solo up:** the key puts its fader to full and every other light goes dark: only that channel (or that
    memory's look or sequence, at full) shines. Let go, the fader is back where it was and every light as
    before.
  * **Solo down:** the key takes only its own fader down, to zero; every other light stays. A channel goes
    dark even where a memory holds it up. Let go, it is back.
  * A solo changes what goes out, like Black out: the other faders do not move. The master still dims what a
    solo lets out, and Black out still darkens it. Several keys can be held at once; a key held on a phone
    counts for the whole desk, and the desk lets it go if that phone loses its connection. While a record
    key waits for a memory, the memory keys are for that, and while a sequence is recorded the channel keys
    put in steps, as always. Both dark again: the keys switch on and off as before. A solo is for the light:
    the key of an attribute of a fixture (a pan) does nothing while one is lit.
* **Rate** (in General, under Solo down) is a tempo for the sequences, in beats per minute (BPM). Dark,
  every sequence plays its own times; lit, every sequence takes one step per beat of the tempo. The steps
  of a sequence keep their proportions (on average a step, its fade and its wait, lasts one beat), and its
  own speed counts the steps of a beat: ×2 takes two steps per beat, ×0.5 one every two beats. The fader
  under the key sets the tempo, from 30 BPM at the bottom to 300 at the top in whole beats, with a line at
  120; the tempo stands on top of the fader (120 BPM), and a double click puts it back to 120. While Rate is
  dark the fader is dim; it can still be set, for the next time. A sequence goes on from where it is when
  the tempo changes. Like the master fader, the tempo is not saved with the project, and the other pages see
  it at once.
* **Tempo** (under the rate fader, only while Rate is lit) is tapped in time with the music: from the second
  tap on, the tempo is the beat of the taps, the average of the last four beats, and the fader moves to it.
  A pause of more than two seconds starts the count afresh. Its shortcut taps as well.
* **Undo** and **Redo** (top left) take back the last change of what the project holds, and make it again:
  storing, copying and emptying memories, sequences and their speed, the effects, the cues of the stack,
  notes and the channels, memories and effects of the independent keys. Their tooltip says what they would
  do, for example "Undo storing memory 5", and the top bar says what they did. The faders, Clear, the
  master, the playbacks of the effects and the settings are no steps; changes of one kind in quick
  succession (the moves of a speed fader) are one.
  Up to 100 steps are kept, for the whole desk, whichever device made a change. Loading a project, a new
  one and an import start afresh, and while the stack is live they are locked. Ctrl + Z and Ctrl + Y are
  their shortcuts until the Keyboard tab of Settings gives them others.
* **Stack** (across the top of the three columns at the right) is the stack at hand on Main: its keys stand
  in a column at the left, over the master section. Stack takes it live, Back, GO, Pause and Next go
  through it, as on the Stack view. Beside the keys, over the other two columns, stands a wheel of the
  stack's cues, each number with its note beside it (the note under the fader of its channel or memory, the
  name of its fixture or group, or for a cue without one what it is, "Channel 5 (U1)"): the one that is on
  in the middle (green, with an arrow beside it, at the height of GO), the cue before it above (blue),
  the one GO goes to below (yellow), the others grey, their numbers about half as high as a key is wide
  (15 to 22 pixels). It shows 7 cues, or 1, 3, 5 or 9 as Settings says,
  and rolls on when another cue comes on. The stack goes round, and so does the wheel, but a cue stands in
  it once only. While no cue is on, the middle is empty and the cue GO goes to stands under it. On a low
  window the keys of the column get flatter and the wheel shows fewer cues, always the one that is on.
* **The whole screen:** Stack, pressed to take the stack live, puts the page on the whole screen of this
  device, and pressed again to switch it off, takes it back (also on the Stack and Stage views, and with its
  shortcut). Esc leaves the whole screen at any time; the stack stays live. A stack switched on or off on
  another device leaves this screen as it is.
* **Independents:** keys in a column at the right edge, under the stack: green ones for channels, red ones
  for memories, violet ones for effects, and cyan ones for sounds; ten green and ten red and none of
  the others unless Settings says otherwise (0 to 32 of each; with none of any the column is left out). A
  green key switches its channel between zero and full, and that channel's fader follows; a red key does the
  same with the fader of its memory; a violet key starts and stops its effect, as the effect's own key does
  (see [Effects](#effects)); a sound key starts and stops its sound, as the sound's own key does (see
  [Sounds](#sounds)). The keys start on channels 1, 2, 3 ...,
  memories 1, 2, 3 ..., effects 1, 2, 3 ... and sounds 1, 2, 3 ...
  Press Edit there, type other numbers onto the keys (a channel of another universe as 113 U2), and press
  Edit again. In a
  project of several universes a green key shows its channel's universe under the number. A green key is lit when its channel's own fader is up (a
  memory that holds the channel does not light it); a red key is lit like the key under its memory's fader,
  and runs from red to yellow for a sequence; a violet key is half lit for an effect that is there, fully
  lit while it runs, and blinks while it fades in or out. Keys that do not fit into the column go on in
  another one; Edit then stands alone in its row above them.
* **Effects:** when Settings shows it, a row of effect faders under the memories, with page keys for the
  64 effects: the fader, the key under it and the effect's name under the key, as on the Effects view.
* **Sounds:** when Settings shows it, a row of sound faders under them, likewise (see [Sounds](#sounds)).

![The Main view with the row of effects under the memories and six effect keys among the independents](docs/screenshots/main-effects.png)

![The Memories view: the levels of memory 3 for every channel](docs/screenshots/memories.png)

* **The Memories view** shows what is stored. The memory keys along the top choose a memory, each with the
  two lines of its note under it, and the table under them shows the level that memory holds for each of
  the 512 channels of a universe (the channel's number small in the corner of its cell), with each channel's
  note under its level, in rows of 32 (16 in a narrower window); in a project of several universes, U keys
  beside the title choose the universe (half lit where the memory gives a channel a level). All cells have
  the same size: when channels have notes, every cell keeps room for two lines of one. The page keys at the
  right choose which memories the keys show. This view and the Sequences view go by the window only: what
  Settings says about Main does not change them. Choosing a memory changes nothing.
  * **Record** under the page keys has **Copy** and **Del** for looks: Copy, the memory to copy, then the memory
    for the copy (one with a look, or an empty one), which the view then shows; Del, then the memory to
    empty. A memory that holds a sequence belongs to the Sequences view: its key says so here.
  * **A double click changes it.** On the note under a memory key: two lines to type into, as under a fader
    on Main (here and on the Sequences view). On a cell of the table: fields over the cell for the level
    the memory gives that channel (0 to 255) and, under it, the two lines of the channel's note; the one
    double-clicked has the cursor. Enter, or a click anywhere else, keeps what was typed; Esc changes
    nothing. An empty memory gets a look with that one channel; the level of a memory that holds a sequence
    is changed on the Sequences view, so only the note can change here. Each change is a step of Undo, and
    while the stack is live nothing changes.

![The Stage view: the stack of the example concert at the left, live on step 5.0, the rig in its live colors, the LED bar chosen and its faders in the panel](docs/screenshots/stage.png)

* **The Stage view** is a plan of the stage seen from above, for programming without the rig: a grid of
  squares (16 by 10 at first), the audience below it, and the lights of the rig on it, each in the color
  its channels give it at this moment, as it goes out: memories, sequences and the stack included, after
  the master, and dark during blackout. A light glows around itself, and where lights meet their light adds
  up. Every window shows the same desk, so the plan can stay open in a second window, on a second screen or
  on a tablet over LAN while you program on Main.
  * **The stack** stands at the left of the plan (under it on a phone), to run a show while the plan shows
    its lights: the keys of the Stack view (Stack, Back, GO, Pause, Next), its cards Previous, Current and
    Next one under the other, each with the number of its cue big at the right; under them the wave of
    the sound that plays, the master section (Black out and the master fader) and the sound section (MUTE,
    LISTEN and the volume of all sounds) in a line each; and its steps with their notes and times, the
    current one green and the next one with an amber edge. When the list is longer
    than its room, it follows the stack: when the step that comes on or the next one is out of sight, the
    list scrolls so that the step before it stands at the top. Nothing about the steps can be changed there:
    the Stack view does that. The panel at the right is as wide as the stack, so the plan stands in the
    middle. **Light** (top right) hides the panel, and the plan takes its room; pressed again it is back.
    While Edit is lit the panel is there anyway, as Edit works in it. This device keeps the choice.
  * **Shutter and strobe:** a light whose shutter is closed is dark. A light that strobes flashes, as its
    strobe channel (S) says: a strobe flashes, a pulse swells and fades, a ramp rises or falls, and a random
    strobe flashes at uneven moments. A light of a library says in which range of its strobe channel it
    stands, and often how fast ("Strobe, 0.3Hz to 19Hz": by the place of the value in the range); a light
    of letters strobes above 9, slow to fast. The screen flashes as fast as the light up to 2 flashes a
    second, faster settings flash faster but never more than three times a second (more can set off a
    seizure in people with photosensitive epilepsy), and a mark over the light gives the real speed: ⚡ 13.3
    Hz, or ⚡ alone when the light does not say it. When the system asks for less motion, the mark alone
    shows it.
  * **The panel** at the right (under the plan on a phone) is there unless Light hides it, so the plan keeps
    its place and size until Light is pressed. **A click on a light** shows it there: its name, its kind and its channels, and a fader
    for each of its channels (R red, D dimmer, ... under it, then the channel's number), colored as its
    channel is. They are the channel faders of Main: moving one there moves it here, and a memory that
    holds a channel up holds its fader up here too. A press beside the lights, Close or Esc lets the light
    go, and the panel says again what a click does. Its fields stand one in a row, the label at the left.
  * **A double click on a light** puts its faders to full, and the next one back to zero: those of its
    dimmer and its colors, not a strobe or a mode (of a device that gives no light: all its channels).
    **Clear** (top right) is the Clear of Main, with the same shortcut: it cancels an unfinished function,
    brings every channel fader and memory fader to zero and takes the stack off its cue (a live stack stays
    live, and GO starts it at its first cue again).
  * **Edit** (top right) places the lights: **Add light** puts one on the plan, on the channel after the
    last light, named after the note of that channel. Drag a light by its middle to move it (in half
    squares; the arrow keys move it too, with Shift by two squares; Delete takes it away), and by an edge
    or a corner to size it, in whole squares, with the edge across from it staying where it is; the cursor
    shows what a press does there. Click a light to change it in the panel, over its faders: its name, its
    address (first channel), its kind, its shape and size, and how it is turned (45° a step). While no light
    is chosen the panel sets the size of the stage and the picture under it. Lights can also hang beside
    the stage, behind it and over the audience.
  * **Shape and size:** a light is round (a circle, or an ellipse when it is wider than deep) or square (a
    square, or an oblong over several squares of the grid), 1 to 32 whole squares wide and deep. A square
    light lies with its edges on the lines of the grid (unless it is turned askew) and moves by whole squares.
    The size is not the cells: a bar four squares wide with one RGB cell turns all red with its red channel;
    with four cells, each quarter has its own color. Cells lie along the longer side.
  * **Picture:** a photo or a drawing of the stage under the grid and the lights (Edit, with no light chosen:
    Choose a picture). The page makes it smaller first (at most 1600 pixels on its longer side, as a JPEG),
    and the stage takes its proportions: its depth changes so that the picture fits. It lies under the stage
    or under the whole plan, shows through from 10 to 100 % (60 % at first), and Fit the stage to it and
    Remove the picture do what they say. It is saved with the project.
  * **Kinds of lights:** a dimmer of one color (you pick its color, like a gel; warm white until then),
    RGB, RGBW, RGBA, RGBWA, RGBW + UV and RGBWA + UV, each with or without a dimmer channel (first or last)
    and in 1 to 16 cells (a bar of four RGB cells is 12 channels from its address; each cell shows its own
    color), a device that gives no light (a hazer, a mirror ball: it shows the level of its channel), the
    channels typed as letters for any other fixture mode (D dimmer, R red, G green, B blue, W white,
    A amber, U UV, P pan, T tilt, and any other letter for a channel that gives no light, such as S for a
    strobe; a D dims the cell it stands in; one D in a light of several cells dims all of them), or a
    light **from the fixture library** (see the Fixtures view). **Limit** (1 to 100 per cent) and **Copy the
    light** are there too.
  * The plan belongs to the project: it is saved with it, and a project that is loaded brings its own. A
    new project, one from before plans and an imported show have an empty stage (no lights, no picture,
    16 by 10 squares). Changes of the plan are steps of Undo, and like everything stored the plan cannot be
    changed while the stack is live.

![The Fixtures view: a fader for every light and group, and the panel of a moving head with its colors, where it points and its settings](docs/screenshots/fixtures.png)

* **The Fixtures view** plays the lights of the stage plan as fixtures instead of loose channels. Each light
  has a strip, in the order of their addresses: its color on top, its intensity as a number and a fader,
  its key (to full, and to zero) and its note. The note is as the one under a channel fader on Main: two
  lines to type into, in white as typed; where nothing was typed, the name from the stage stands there,
  dimmed. The note belongs to the fixture (a group has its own too) and is saved with the stage plan; the
  name on the Stage view stays as it is. The fader moves its dimmer; a light without one, such as an
  RGB bar, keeps the mix of its colors, and comes up from dark in the color it had last (white at first); a
  moving head opens its shutter when it comes up. **Groups** stand under the fixtures, each with a fader that
  moves all its lights to its level, each in its own color; **New group** makes one of the fixture chosen,
  and its lights are ticked in the panel. Everything here moves the channel faders, so a memory saved
  afterwards holds it, and a memory that holds a light up holds its strip up too.
  * **The panel** at the right (under the strips on a phone) is always there; a press on the color on top
    of a strip shows that fixture or group in it. For a fixture: twelve colors and a field of every hue
    (white at the bottom) for a light that mixes its color, a white channel taking the white part of it; the
    colors of a moving head's color wheel; a pad for where a moving head points (pan across, tilt down; in
    16 bits with fine channels), with a slider for each that the keyboard moves too; a list for each channel
    that does several things (a gobo wheel, a shutter, a mode), each choice with its values, like "Open (32
    to 63)"; and the faders of all its channels. What the library says a channel does is there too, so its
    manual is seldom needed: the pointer on a fader's letter lists all its values ("0 to 31: Closed",
    "32 to 63: Open"), and under the faders a line says what the fader moved last does at its value
    ("S (8) at 32: Open (32 to 63)"); a screen reader hears it with the fader's value. A choice that comes
    more than once, like the four Opens of an ADJ strobe, is the same at other values: makers put steady
    light between the effects of a strobe channel. When a fixture gets a color, its shutter is opened at
    the first value of its first Open. For a group: its name, its lights and a color for all of them. The
    faders of a light, with their letters and the line under them, are the same in the Stage view.
  * **Moving heads:** a pan, a tilt, a wheel, a shutter and every other channel of a light that is not a
    dimmer or a color is an attribute. Latest takes precedence for it: the fader or the memory that took
    it last gives it, at the value it holds, not scaled by any fader. A channel fader takes its channel when
    it moves; a memory fader takes the lights its memory holds when it leaves zero (a look holds the lights
    it gives any level, a sequence those its steps use; the stack does it through the faders it moves);
    when it goes back to zero, the one that took them before gives them again. The master, Black out and a
    solo act on the light only, so a head stays where it points. Dimmers and colors still add up.
  * **The fixture libraries:** in Edit on the Stage view, the kind **From the fixture library…** searches two
    open libraries at once, each read from its copy on GitHub (the browser keeps their lists for a week):
    the Open Fixture Library (open-fixture-library.org, MIT license, about 670 lights) and the library of
    QLC+ (qlcplus.org, Apache 2.0 license, about 1,800 lights; its list is FixturesMap.xml). Each light of
    the list says which library it is from (OFL or QLC+); a model that is in both is there twice. The
    search finds every word in the maker and the model: ADJ finds American DJ, as the libraries still call
    it (HES and Claypaky work the same way), and a word may leave out spaces and dashes ("flatpro" finds
    Flat Pro). When a library cannot be reached, the list has the lights of the other and says so. The
    window shows the modes of the model chosen with their channels. The light gets the channels of the mode
    chosen, with their names and what they do at their values (wheel slots with their colors, a shutter
    that is open or closed: QLC+'s "shutter open" becomes a range called Open, which a step opens; a strobe
    keeps how fast and whether at random, like "Strobe, slow to fast" and "Random strobe"). The
    project keeps all of it, with the maker, the model, the mode and where the light is in its library,
    so the library is needed only while lights are set up. A light that is in neither is set up with
    letters, as before. **Copy the light** puts the next one of the same kind on the next free channels.
  * **Limit:** a light limited to, say, 60 per cent (a hazer) gives at most that: its dimmers and the
    channels of a device stop there, and the colors of a cell without a dimmer are dimmed together, so
    that the color stays.
  * On Main the note fields of a fixture's channels show, dimmed, what the plan says each channel is
    ("Spot 1", "Pan") where no note was typed; the lists of steps name the channels so too.

![The Notes view with the notes of the example concert](docs/screenshots/notes.png)

* **The Notes view** holds the notes of the project: instructions for the show, what it is and how to run
  it. It is left out of the top bar at first: Settings shows it with a tick. They are read there; **Edit** (faint while off, full while on) makes them a text to write in, with
  the tool keys at the left: **B** (bold), **U** (underline), the **size** (small, normal, large, huge), a
  bulleted and a numbered **list**, and a **color** (the six colors of the desk, and **A** for the usual
  one). A tool key lights up when the text at the cursor has its mark, and works on the text that is
  chosen; Ctrl + B and Ctrl + U work as everywhere. Pasted text comes in as plain text. What is written goes
  to the desk program as soon as the typing rests, and when Edit goes off; every page shows it. The notes
  belong to the project: saved with it, a step of Undo (typing that goes on is one step), and locked while
  the stack is live. The page shows only those marks, whatever a file holds: anything else (a script, a
  picture, a link) is taken out, and its text stays.
* **Project:** New, Save, Load, Import and Export at the top right. A project is one JSON file in
  `Documents\Lumenet`. It holds the memories (looks and sequences), the effects, the stack, the notes under
  the faders and of the Notes view, the stage plan and what the independent keys are bound to, not the fader
  positions or what runs, so loading a project never switches a light on by itself. New empties the desk; it asks first.
  A project keeps 32 channel keys, 32 memory keys, 32 effect keys and 32 sound keys of the independents, however many the
  page shows. A project with effects is a file of a newer version, which an older Lumenet does not open. An
  older project keeps the keys it has (10 of each, or 16 channel keys from before there were memory keys);
  the others are where they are on a new desk. The layout, the colors of the keys and the shortcuts belong
  to each device, not to the project (see [Settings](#settings)); a project file of an earlier version that
  holds those of the PC is loaded without them, and Import can take them from it.
* **Autosave:** what is not saved yet is kept every five seconds in a file of its own on this PC
  (`%LOCALAPPDATA%\Lumenet\autosave.json`, the setting `Autosave:File`), and that file goes once the
  project is saved. If the desk program stops before that (a crash, Windows restarting, the window
  closed), it brings the work back when it starts: the project under its name and file, marked "not saved",
  and the top bar says so. Saving keeps it; loading the project again drops it. The faders are not brought
  back: a desk program that starts switches no light on by itself. Settings switches the autosave off and
  on and sets how often it keeps the work (see [Settings](#settings)).
* **Examples:** the Load window lists them under the projects, in the order of their numbers. One opens as
  a project of its name, without a file: the example itself stays as it is, and Save asks for a name. Each
  has notes on what it shows and how to use it, in the Notes view (Settings shows it with a tick), and none
  brings settings or shortcuts. The first four show one thing each, the next two all of it together, then
  come the effects, the sounds, a show of light and sound cues, and cues that come on the music:
  * **1 Looks and cues:** eight lamps of a small theatre, six looks in memories and a stack of nine cues,
    one of which goes on by itself.
  * **2 Chases and sequences:** a row of six lamps, two washes and a blinder: a chase, a bounce, a random sparkle at
    1.5×, a sequence of memories, an intro that runs once and two slow breaths at 0.5×.
  * **3 LED lights and groups:** RGBW and RGB pars, an LED bar and a hazer, in three groups. The stack is
    made of lights and groups of the stage only, each cue with its color; memory 1 holds a chase of light
    steps.
  * **4 Moving heads:** two moving heads from the fixture library (American DJ Inno Pocket Spot, 9 channels),
    a front wash and a hazer: positions with colors and gobos in memories, a swing between two of them.
  * **5 A whole theatre show:** the whole show of a small theatre on 17 channels: two red lights in a pot, a front
    wash, four blue back lights, a spot, an RGBW LED bar, a hazer, a mirror ball and the house light. Notes on
    every light and memory, ten looks, eleven sequences (every template, some made by hand and some recorded
    with Rec seq; in a loop, once and at random; at speeds from 0.5× to 2×), all twenty independent keys
    bound, and a stack of 14 cues with cues put in between (3.5, 4.1), waits that go on by themselves and a
    channel step for the hazer. Its stage plan lies over a drawing of the stage: a wooden floor, the back
    curtain, masking at the sides, a riser, the fire pot between its two red lights and tape marks.
  * **6 A whole concert:** two moving heads, front and back pars, an LED bar, blinders, a hazer and the house light,
    in five groups: looks and positions in memories, sequences of lights and of memories, and a stack that
    mixes memories, groups, a light and a channel.
  * **7 Effects:** four moving heads, eight RGBW pars and two LED bars of eight cells, with ten effects: a
    circle and a fan of the heads, a chase and a pulse of the pars, a rainbow and red and blue, and bars, a
    ripple, sparkle and plasma over the cells of the bars. Memories point the heads and light the pars, and a
    stack plays the effects with Add and a release.
  * **8 Playing sounds:** a stormy night in sound and light, on the stage of example 3: rain and wind in loops,
    thunder near and far (one file played two ways), a church bell with an echo, applause and a short piece
    of music, which a second sound plays slower at the same pitch; four looks and a lightning effect. The
    sounds were made for Lumenet from noise and tones alone, so they are free to use; they lie in the folder
    "8 Playing sounds sounds" beside the example, and come along when it is opened.
  * **9 Light and sound cues:** the night of example 8 as a show of 15 cues: looks and sounds together,
    the music, the rain and the wind with Continue through the scenes after them until a release stops each,
    the thunder, the bell and the applause with Add, and the lightning.
  * **10 Cues on the music:** a dance of the puppets on the stage of example 3. GO starts its music, "Puppet
    dance" (40 seconds at 120 BPM, made for Lumenet as well), and the cues come by themselves at its moments:
    the verse at 0:08, the chorus at 0:16, the break at 0:24 with a flash on each clap at 0:26 and 0:27, the
    last chorus at 0:28 and the final chord at 0:36. Two effects at the tempo of the music, a skipped cue for
    haze, and a release that ends the show.

  They are the files in `examples/`; the desk program takes them along when it is built, and the setting
  `Projects:Examples` names another folder for them.

![The Export window with its five checkboxes, none checked](docs/screenshots/export.png)

* **Export** puts parts of the desk into one ZIP on this device: **Project** (everything the project holds,
  as its file has it, with its sounds), **Stage** (the stage alone: its lights, their kinds, the groups and
  the picture), **Settings** (those of this device), **Keyboard** (its shortcuts) and **MIDI** (the MIDI
  controllers of the PC and what their controls do; on the PC only, see [MIDI controllers](#midi-controllers)).
  Each has a checkbox, none checked at first. In the ZIP each part is a JSON file of its own (`project.json`,
  `stage.json` and so on; `lumenet-export.json` says which it holds), and the sound files are in `sounds`.
  Chrome and Edge on the PC ask where to save the file; other browsers put it among the downloads.
* **Import** takes parts back from such a ZIP, here or on another device: **File** first, then the window
  lists the parts the file holds, each with a checkbox, none checked at first (a part it does not have says
  "not in this file"), and **Import**. It also takes a backup (see [Backup](#backup)), a file of Export of
  before 2026-11-26 (one JSON file, or a ZIP of it with its sounds) and a project file of the projects folder
  (its project and its stage). A ZIP goes to the desk program as it is chosen, which keeps its sounds (they
  change nothing until a project uses them). A project comes in place of the open one, as with Load, called
  after the file and not saved until Save; a stage comes in place of the project's stage (a step of Undo), and
  a step that played light 3 of the old stage plays light 3 of the new one; settings and shortcuts become this
  device's, and the MIDI the PC's. The window then says what came in. The show of a theatre console is a file
  of its own, taken whole: a file in the USITT ASCII format (`.asc` or `.txt`, up to 8 MB), the plain text in
  which consoles such as ETC Eos, Ion and Express, the ETC (AVAB) SmartFade or the Strand consoles hand a show
  on (on an Eos: File, Export, USITT ASCII; a SmartFade saves its shows in this format). It becomes the
  project, like Load replacing the open one, and the window warns when that has changes that are not saved
  and then says what came across and what was left out.
  * **Cues:** every cue of the first cue list becomes a memory with its look, and a cue of the stack that
    brings that memory up: with the cue's number, its up time, and as down the down time of the cue after it
    (the stack takes the old cue down while the new one comes up). A follow time becomes the cue's wait,
    which counts from the moment the cue is up. A cue in parts becomes one fade, as long as its slowest
    part; a delay is added to the fade time. A cue without a time fades in 5 seconds. A last cue that links
    to the first goes round, as the stack does; other links are left out.
  * **Submasters and groups** become memories too, after the cues, as long as memories are left. There are 128
    in all: what does not fit is left out, and the window says so.
  * **Patch:** the levels go through the file's patch onto the DMX addresses, which are Lumenet's channels;
    a file without a patch has channel 1 on address 1. The project gets as many universes as the highest
    address needs (address 600 is in the second), up to 8: addresses above 4096 are left out, and the window
    says so. The note under a channel names the console channel on that address ("Ch 7"; under it the share,
    if the patch gives less than 100 %).
  * **Notes under the memories:** the cue, submaster or group and the start of its label, like "Cue 4.5" over
    "Spot on the".
  * **Not taken over:** lines that only one maker's consoles understand (they begin with `$`: moving lights,
    effects, macros), cue lists after the first, and cue numbers the stack cannot have (more than one digit
    after the dot, or above 999.9): those steps get the next free number, and the note keeps the cue's own.
    Of cues, groups, submasters, sequence steps and labels the import keeps at most 10,000 of each kind, far
    more than a show has; a file with more is still read in well under a second.
  * **The fades:** as on the console, every light fades straight from its level in one cue to its level in
    the next: up in the up time of the cue, down in its down time. A light that stays on from one cue to the
    next does not dip.
  * **ETC SmartFade:** its files have a dialect of their own, and its show keeps its shape. Its memories become
    memories, in the order of their numbers, with a note like "Mem 25" over the memory's label. The steps of
    its stack bring those memories up, so a memory that several steps play is there once; only a step with
    levels of its own (a "state", a single channel) gets a memory of its own, and a memory that a step plays
    but the file does not hold stays empty, as on the console. Its sequences become memories that hold
    sequences: Loop and One Shot as on the console, Manual in a loop. A wait counts from the moment a step is
    up, as on the console, and a wait of 0 waits for GO. Its patch has the DMX address first; the labels of
    its channels become the notes under them, and its independents IND 1 and IND 2 get independent keys 1
    and 2. A percentage becomes a DMX value rounded up, as the console has it (17 % is 44).
  * `examples/Example theatre show.asc` is a short play for the lights of the example "5 A whole theatre show",
    written by hand to show all of this: a patch, a blackout, a cue in two parts with a delay, a storm whose
    cues follow on by themselves, a fade of 1:30, submasters and groups. Import it from the `examples` folder
    with .ASC checked.
* **LAN** (top left, beside the name) lets a phone or a tablet in the same network use the desk; see below.

## Universes and outputs

A universe is the 512 channels one DMX cable carries. A project has 1 to 8 of them (Settings, Universes; the
project keeps the number, and a project from before has one). The channels of every universe are numbered 1
to 512, as on the cable.

![The channel faders of the second universe, with U1 and U2 above them, and two outputs playing](docs/screenshots/universes.png)

* **U keys:** in a project of several universes, a key for each stands above the channel faders: U2 shows the
  channels of the second universe, and the page keys are then its pages. The key of the universe shown is
  lit; another is half lit while a channel of it is above zero. The Memories view has U keys of its own.
* **Channel numbers:** every channel says its universe, "113 (U2)": in the lists of steps, on the Stage and
  Fixtures views, in the Memories view, in the messages of the desk and in what screen readers hear. The
  keys under the faders and the page keys print the number alone; the lit U key above them says the
  universe.
* **Typing a channel** (on a key of the independents, the address of a light): its number with the universe
  beside it, 113 U2, 113 (U2) or U2 113. A number alone stays in the universe the key or the light is in, so
  in a project of one universe the number alone is enough. The Template window has a list of the universes
  beside the channel numbers.
* **Fewer universes** are refused while a step of a sequence or a cue of the stack, a light of the stage plan or a
  key of the independents uses a channel of a universe that would go; the top bar says which. The levels the
  memories hold there and the notes of those channels go with them; Undo brings them back.
* A light of the stage plan has all its channels in one universe: an address that would take it past the
  end of its universe moves it down until it ends there.

### The outputs

Every universe goes out on one or more outputs: a dmxbox, an **Art-Net** node, **sACN** (E1.31) or a **DMX
USB interface** of the kind of the Enttec DMX USB Pro. Every output plays a universe of the project, and two
may play the same one, as a splitter would. Master, Black out, Solo and the limits of the lights give the
same levels on every kind of output, and so does a box: its own master stays at full.

* The top left says how many outputs play: "Outputs connected: 2", or "Outputs connected: 2 of 4" once the
  project lists boxes or has outputs besides them. A box counts while it answers, an Art-Net node or a USB
  interface while it answers, sACN and an Art-Net broadcast while they go out. Its light is green while they
  play, amber while one of the project's does not. The pointer on it says what each plays. The outputs
  themselves are in **Settings**, in the **Outputs** tab; the shortcut of Outputs opens Settings
  there.

![The Outputs tab of Settings with the simulated devices of sim.cmd: two boxes, an Art-Net node found by the search, sACN to a receiver and a USB interface at SIM1](docs/screenshots/outputs.png)

* **The table** has a line for every output of a box and every port of the other outputs: the output (its
  name, and under it its address), its **kind** (dmxbox, Art-Net, sACN, USB DMX), its **state** (with what it
  said, or why it does not answer), what it **plays** (U1 to the project's last universe, or Off, which sends
  nothing) and where that goes **on the cable**: an output of a box, the Port-Address of an Art-Net port, the
  sACN universe of a port, the DMX socket of a USB interface. ✕ takes a port out, or an output with one port;
  **+ Port** gives an Art-Net or sACN output another port.
* **Found on the network (Art-Net poll):** the Art-Net nodes that answered the search, which starts when
  Settings opens; **Search again** asks once more. **Add** puts a node into the table with a port for each of
  its own.
* **Add an output:** an Art-Net node by its address, an Art-Net broadcast, sACN by multicast or to the address
  of a receiver, a USB DMX interface at a free serial port, or a dmxbox by its address; with how many ports
  it has (a box: how many outputs). The sentence under it says what the kind asks for. One that is in the
  list already cannot be added again: Add stays dark, and the sentence says so.
* Under them, marked ⚠: a universe of the project that reaches no cable (and which of the outputs that
  would play it do not answer), a universe that goes out more than once, as a splitter would give it, and an
  Art-Net node that is a dmxbox.
* The outputs are part of the project and their changes go into Undo; New keeps them, like the universes and
  the boxes, since they belong to the rig. While the stack is live they cannot be changed. Nothing goes out on
  the network until an output is added here.

**dmxbox.** A box has an output, a DMX socket, for each universe it sends, and says how many when the desk
finds it. The box with the Pico has one, at 192.168.7.1. A box with a number switch answers at 10.1.n.1 for
the number n on its switch, and the desk finds it by itself on the PC's network adapters. dmxbox 4 is such a
box, with one output: its switch gives 0 to 255, and every box on one PC needs its own number. Its lights with
RDM are in the RDM tab of Settings (see [RDM](#rdm)), and its MIDI sockets are a MIDI device of the PC.

* **Automatic:** while the project lists no boxes, every box that answers plays by itself: the box with the
  Pico first, then the others in the order of the numbers on their switches, each output the next universe
  (U1, U2 and on). Outputs beyond the project's universes play none.
* **The project's list:** the first change of a box (another universe on an output, a box added or taken
  off) makes the boxes that answer the project's list, with what they play then and the change on top. From
  then on the project keeps which box plays which universe, also on another PC.
* A box can be added by its address (10.1.2.1) and its number of outputs, also before it is there: it shows
  as not answering until it answers. A box that answers but is not on the list is **new**: it plays nothing
  (its outputs send zeros) until **Add** in its line puts it on the list. ✕ takes a box off the list.
  **Auto**, at the top right of the panel, empties the list: the boxes play by themselves again.

**Art-Net.** Art-Net 4 to a node at its address, on UDP port 6454.

* The search asks every node on the networks of this PC to answer. A dmxbox answers too, but cannot be
  added: it plays as a dmxbox already, and Art-Net as well would drive it twice. A node in another network,
  which the search does not reach but a router does, is added by its address.
* **Ports:** each plays a universe of the project (or Off, which sends nothing) at a Port-Address: the Net
  (0 to 127), Sub-Net (0 to 15) and Universe (0 to 15) set on the node, which most nodes show as 0:0:0 for
  their first port.
* **A broadcast** sends to every node of a network instead, for older gear; the list under its name chooses
  the network (Automatic is the one that leads to the router). The networks of the boxes' cables are never in
  it.
* **States:** a node is asked every 2 seconds whether it is there. **Connected** shows its name and
  firmware, and says when no port of the node listens to a Port-Address it gets. **Not answering**: no answer
  came; the frames go out anyway (a firewall on the PC may hold the answers back). A broadcast says
  **Sending**. Art-Net to the address of a dmxbox is refused, and a node that calls itself a dmxbox gets
  nothing (**A dmxbox**): the desk plays a box with its own protocol already, and two at once would fight.

**sACN.** Streaming ACN (E1.31) on UDP port 5568, from the source "Lumenet".

* **Multicast** sends each universe to its own multicast address (239.255.0.1 for sACN universe 1), which
  every receiver in the network can take; the list under its name chooses the network. sACN to a receiver
  goes to its address only.
* **Ports:** each plays a universe of the project as an sACN universe, 1 to 63999. **Priority**, 0 to 200
  (100 unless changed), on the first line of the output: of two sources of one universe, a receiver takes the
  one with the higher priority.
* A receiver cannot answer, so sACN says **Sending** while it goes out. A universe that stops (its port set to
  Off, its output taken out, the desk program ending) is ended with the packets that say so, and its
  receivers let go of it at once. The desk is the same source every time it runs: its CID is kept in
  `%LOCALAPPDATA%\Lumenet\sacn-cid.txt`, next to the LAN password.

**USB interfaces.** An Enttec DMX USB Pro, or an interface that speaks its protocol (DMXking ultraDMX Micro
and others), at a serial port of this PC: each plays one universe, or Off, which keeps its cable dark.

* **Add an output** with **USB DMX interface** lists the serial ports of the PC that no interface has; the
  list follows what is plugged in while Settings is open. A simulated interface of `sim.cmd` is at SIM1
  (SIM2 and on).
* **Connected** shows its serial number and firmware. An interface pulled out shows **Not answering**, and
  plays again on its own once it is plugged back in. A port that another program holds, or a device that does
  not answer as a DMX USB Pro does, says so.
* The interface repeats the last frame on its cable by itself, also when the desk program ends. An Open DMX
  USB, and other interfaces without a processor of their own, do not work here yet.

### RDM

A light with RDM tells its dmxbox what it is, and takes its DMX address, its mode and its name from the desk,
so nobody has to climb to it. dmxbox 4 looks for the lights with RDM on its DMX line when it starts. They are in
**Settings**, in the **RDM** tab, a table for each box that answers.

* **The table:** each light with its name (the one it was given, else its model), its maker, model and RDM
  ID, its **address** with the channels it takes ("5 to 20"), its **mode** (the light's own names of its modes,
  with their channels) and its **name**. A change goes to the light at once, and the table shows what the
  light then says. What a light refuses (an address it cannot take, say) is written under it, and the field
  shows the light's value again.
* **Identify** makes the light show where it is (it blinks, a head moves, as its maker chose); its key is lit
  meanwhile. A light still identifying stops when the tab is left.
* **Search** has the box look for the lights on its line again, after lights were plugged in or out. The
  table fills in as the lights answer, one after the other.
* Under the table, marked in amber: two lights that take the same channels, and a light that goes beyond
  channel 512.
* A box without RDM (the box with the Pico) says so, and a box that does not answer too. Lights without RDM
  play as before; they only do not show here. The names are plain letters: RDM keeps no accents, so "Predný"
  goes to the light as "Predny". The project does not change: RDM settings live in the lights, and Undo does
  not take them back.

## Sequences and the stack

### Steps

A sequence and the stack are both made of steps, and both are played the same way. In the stack a step is
called a **cue**, as in other programs that play a show (QLab, the desks of ETC), and GO plays the next one. A step stands for a
channel, a memory, a **fixture** or a **group** of fixtures (see the Fixtures view), has the value it brings
that to (0 to 255), and three times in seconds: **up** (from zero to the value), **wait** (how long it stays)
and **down** (back to zero when a step takes it down). A step can also play an **effect**, take another step
down (**Release**) or come up on top of what is on (**Add**): see the Stack view, and the Sequences view for
how a sequence does it. The old step goes down while the new one goes up, and every light
moves straight from its level in the old step to its level in the new one: a light that both steps give at
the same level stays where it is, without a dip; one that rises follows the up time of the new step, one that
falls the down time of the old step (down to zero if the new step does not give it). A light at 120 in the old
step and 150 in the new one never goes below 120: it rises to 150 in the new step's up time. At 150 and then
120 it goes down to 120 in the old step's down time and stays there. The views show it the same way. A step played earlier
that is still on its way down, because its down time is longer than the steps after it, keeps its lights up
the same way, from where the newer steps have them. In the stack, a fader that both steps use moves straight
from the one value to the other in the new step's up time.

A step of a fixture or a group has a **color** too: the box before its name in the Notes column. A press on
it opens a window with the colors of the Fixtures view, the field of every hue, and White (no color): a press
or a drag there only chooses, the window shows the color chosen with its code, and **Use** gives it to the
step; Cancel or Esc leave the step as it was. Its value is the intensity of its lights: the dimmer,
and the colors mixing the step's color (a light without a dimmer mixes it at that level); a moving head's
shutter opens, and its color wheel turns to the slot nearest the color, at once. Where a head points stays
with the memories, which hold whole looks: a memory step does that. In the stack, a step of a fixture or a
group moves the channel faders of its lights. A step follows its fixture when the fixture gets another
address; a fixture that is taken off the plan leaves its steps doing nothing ("Gone").

In both tables the **kind** of a step says what it stands for: **Channel (U1)**, **Channel (U2)** and so on, one
for each universe of the project, then Memory, Fixture, Group, **Effect** and **Release**. A channel step that is switched to another
universe keeps its number there (113 of U1 becomes 113 of U2). The **number** of a step is chosen from a list.
Each line of the list reads like `5: Front left`: the number, then the note that stands under that fader in
the main view (its two lines with a space between them); a fader without a note shows its number alone. For a
channel the list has the 512 channels of its universe; for a memory only the memories that hold something; for
a fixture every light of the stage plan, by its address (`18 (U1): Spot 1`, the closed field showing the
address), for a group every group (`G1: Back`).
The **Notes** column beside the number shows the note of the channel or memory the step uses, or the name
of the fixture or group. It is read there, not written: a note is changed under its fader. **Add step**
(**Add cue** on the Stack view) puts in a step like the last one, on the next channel, memory, fixture (by
address) or group.

### The Sequences view

A memory can hold a sequence in place of a look; its key is then red running into
yellow. A sequence runs while the fader of its memory is up: it starts at its first step when the fader
leaves zero, and the fader dims what it gives, like a look. It moves no fader; the channel faders show what
it gives in red.

![The Sequences view: the rainbow of the LED bar, six steps of a fixture in a loop, step 5 running](docs/screenshots/sequences.png)

* Choose a memory with the keys along the top; the two lines of its note stand under each. Add step puts
  in a step like the last one that is no release, on the next channel or memory. Every change goes to the
  desk at once. Rec seq in the main view is the other way to put in steps: by pressing keys.
* **Record** under the page keys has **Copy** and **Del** for sequences: Copy, the memory with the sequence, then
  the memory for the copy (one with a sequence, or an empty one), which the view then works on; Del, then
  the memory to empty. A memory that holds a look belongs to the Memories view: its key says so here.
* **Once**, **Loop** or **Random:** played once, the last step stays on; in a loop the first step follows
  the last. Random loops too, but plays all steps on every round in a new order, so that a few steps never
  look like a loop (the same step never comes twice in a row between two rounds). A new sequence is played
  in a loop until you choose another.
* **Speed:** a lying fader above the table, with 1× in the middle: to the left down to 0.1× (ten times
  slower), to the right up to 10× (ten times faster), in steps of 0.05. It divides every time of the
  sequence, so 2× plays it twice as fast. A running sequence goes on from where it is; it does not start
  again. The 1× key puts it back. The arrow keys move it by 0.05 left of the middle and by 0.5 right of it.
  The speed is kept with the sequence in the project. While Rate is lit (Main, General), the sequence takes
  one step per beat of the tempo instead, and its speed counts the steps of a beat: 2× is two a beat.
* **Template** fills the sequence from a pattern, for the lights you name (channels like `1, 2` or `1-4`,
  memories, or the fixtures of a group, in the order of their addresses and in a color you choose). The
  steps it makes replace the old ones and can be changed in the table like any others.
  * **Fire:** every light flickers on its own, always between Low and High (now and then near Low, where a
    flame ducks), played at random. Low and High both at 255 give a steady full light.
    It looks best over a glow: set the faders of those lights to about 80.
  * **Chase:** one light after the other. **Wave:** each light swells and fades in turn; a single light
    breathes. **Strobe:** short flashes, light after light. For several lights at once, name a memory
    that holds them.
  * **Storm:** strikes up to High and faint flickers from Low, with dark pauses between, at random. The
    pauses are steps with the value 0: they give no light at all.
* A wait of 0 means the next step starts as soon as this one is up.
* **A sequence plays itself the way the stack is played** (see the Stack view): **Add** (the box after the
  step number) brings a step up on top of what is on; the next step without Add takes everything down, each
  in its own down time, and so does every new round of a loop, whose first step starts afresh. Played once,
  what is on after the last step stays on. The step played last is green, every other step that is on a
  paler green. Where two steps that are on use the same channel, memory or effect, the later one's value
  counts, as in the stack.
* **Release** takes one step of the sequence down, in the release's own down time, and leaves the rest. Its
  number is the step it takes down, chosen from the other steps (`2: Channel 5 (U1)`). Moved, a step keeps
  its release, which follows it to its new number; taken out, its release takes down none. At random, Add
  and Release follow the order of each round: a release that comes before its step in a round takes nothing
  down.
* **Effect** plays an effect as strongly as its step is up, scaled by the fader of the sequence's memory,
  and moves no fader: the effect's own fader stays where it is, and the higher of the two counts. The effect
  starts when its step begins to come up and stops when neither its fader nor a sequence plays it.
* **Sound** plays a sound as loud as its step is up, scaled by the fader of the sequence's memory, and moves
  no fader, like Effect. With **Continue** (the box after Add, only for a sound) the sound plays on through
  the steps after it, also into a new round of a loop, until it ends by itself or a release names its step;
  the memory's fader at zero stops it too. A step of a sound that still plays does not start it again; one
  whose sound is over plays it again from its start.
* **Disabled** (the box at the end of a row) skips a step: the sequence leaves it out of its rounds and
  plays the other steps. It can be switched while the stack is live too, but not for the step a running
  sequence plays just then; a running sequence goes on, its rounds shorter or longer from then on.
* What still differs from the stack: many sequences can run at once, each dimmed by its memory's fader; a
  sequence never moves a fader on the page; and a wait of 0 goes on at once, where the stack waits for GO.
  A project whose sequences add, release or play effects is saved as version 8 of the project file, which an
  older Lumenet refuses with a message instead of playing it wrong; one with sounds or steps of sounds as
  version 9.
* A step that stands for a memory plays that memory's look. A memory that holds a sequence itself gives
  nothing there: sequences are not played inside sequences. So the list for a memory step offers only the
  memories that hold a look.
* A memory that holds a look is not turned into a sequence: empty it first with Del (on Main or the
  Memories view). Save mem over a
  sequence replaces it by a look; Copy and Del work on sequences as on looks; Edit mem does not.

### The Stack view

The project has one stack: a list of cues for a show. The stack moves the real faders:
going to a cue moves the fader of its channel or memory up to the cue's value, leaving it moves that fader
back to zero. A memory that holds a sequence starts running, because its fader goes up.

![The Stack view, live: the previous, the current and the next cue over the table of the 14 cues](docs/screenshots/stack.png)

* **Cue sheet** (at the right of the view's title) shows the stack as a sheet to print, for rehearsals and
  for whoever calls the cues: every cue with its number, what it plays and its note, its value, Add,
  Continue and Mute, its up, wait and down (a wait of 0 says GO: the cue waits for GO), the moment of a sound it
  comes at (At), skipped cues struck through, and a column **Called on** left empty to write in. **Print** hands it to the browser's printing, which
  can save it as a PDF too; the rest of the desk is left out, black on white.
* **Stack** is the switch of the stack. Pressed, it is fully lit and the stack is **live**: every channel
  fader and memory fader goes to zero, and the first cue starts. Pressed again, the stack stops and the
  faders stay where they are. While the stack is live, the link to the Stack view in the top bar carries a
  red **Live** tag, in every view.
* **While the stack is live, nothing that is stored can be changed:** not the cues of the stack, not a
  memory (Save mem, Edit mem, Rec seq, Copy and Del are locked), not a sequence, not an effect, not a note,
  not the channels of the independent keys, and New and Load are locked too. The faders, the independent
  keys themselves, the playbacks of the effects (their keys, faders and live values), Clear, Black out, the
  master and saving the project work as always. On the Sounds view a sound's level, pan, speed, pitch, echo
  and reverb can still be changed: one sound that the audience drowns can be made louder on its own.
* While the stack is not live it does not run: GO, Pause, Next and Back are dark. That is the time to
  change the cues.
* **GO** goes to the next cue, or goes on after Pause. **Pause** holds a fade or a wait where it is, and
  the sounds of the cues that are on (or on their way down) where they are, silent; GO, Next, Back and
  Stack off let them go on. Sounds played by hand or by a sequence play on.
  **Next** and **Back** go to the next or the previous cue at once, and without the cues' times: the
  new cue is there at once, the cues that go are gone at once, and so are their sounds. A fade that runs
  ends where it goes. After Back the stack stays on that cue until GO, also when the cue has a wait:
  a cue played by mistake (GO pressed too soon) is taken back with Back, its music stops at once, and it
  does not come again by itself.
* **The stack goes round:** after the last cue comes the first, for GO, for Next and when the stack goes
  on by itself. Back from the first cue goes to the last.
* A wait of 0 means the stack stays on the cue until GO. With a wait above 0 it goes on by itself that
  many seconds after the cue is up.
* The view shows all cues, the **current** one in green, the **next** one with an amber edge, the
  **previous** one (the cue before the current one, where Back goes) in blue and every other cue that is
  **on** (see Add) in a paler green, and for the current one how far it is. Over the table three cards say
  what the previous, the current and the next cue are, each with the number of its cue big at its right,
  in the card's color. Back, GO, Pause and Next are bigger than the other keys of the desk, GO the
  biggest. Stack also puts the page on the whole screen (see Main).
* **Master and Sound** lie in a row under the keys and the cards: the master section (Black out and the
  master fader) and the sound section (MUTE, LISTEN and the volume of all sounds), as on Main. Under them
  stands the wave of the sound that plays (see [The Sounds view](#the-sounds-view)).
* **Add** (the box after the cue number): a cue with Add comes up on top of what is on and takes nothing
  down. A cue without Add takes down everything that is on and that it does not give itself, each in its own
  down time: the cue before it and every cue that added since. A light that both give stays where it is.
  For example: cue 1.0 is the scene; cue 2.0 is a special light, with Add: it comes up and the scene stays.
  Cue 3.0 is the scene again, without Add: the special light goes down in its own down time, and the scene
  does not move.
* **Release** is a kind of cue (and a step of a sequence): it takes one cue that is on down, in the release's own down
  time, and leaves everything else as it is. Its number is the cue it takes down, chosen from the other
  cues of the stack (`2.0: Memory 5 (Special)`); it has no value and no up time, its wait counts as usual.
  In the example, a release of 2.0 as cue 3.0 takes the special light down and leaves the scene, even when
  the special light is a sequence. A release of a cue that is not on does nothing, and when the cue it
  names is taken out of the stack, it names none. Where two cues that are on use the same fader, the later
  one's value counts; when it goes, the earlier one's counts again.
* **Effect** is a kind of cue too: its number is an effect, chosen from the 64 (`3: Chase`). Going to it
  brings the effect's fader up to the cue's value in its up time, which starts the effect; leaving it brings
  the fader back to zero in its down time, which stops the effect. Add and a release work on it as on any
  cue. The stack uses the cue's times, not the effect's own fade times, and moves the effect's real fader,
  so the Effects view and the row on Main show it. (A sequence plays the effect without moving the fader:
  see the Sequences view.)
* **Sound** is a kind of cue too, chosen from the 64 sounds (`2: Rain`): going to it brings the sound's
  fader up to the cue's value in its up time, which starts the sound; leaving it brings the fader down in
  its down time, which stops it. **Continue** (the box after Add, only for a sound) keeps the cue on
  through every cue after it, with Add or without, until the sound ends by itself (a sound in a loop never
  does) or a release names the cue: rain can fall through five scenes and stop at the sixth. Stack off and
  Clear stop such a sound too; the other faders stay where they are when the stack goes off, as always. A
  cue of a sound that still plays does not start it again, one whose sound is over plays it again; and
  Back starts no sound but that of the cue it goes to: a cue of another sound comes along only while its
  sound still plays. While a Soundcraft Ui mixer answers, the list of numbers offers, after the sounds,
  the tracks of its playlist that are no sound yet: one picked becomes a sound in the first free number
  (see [Tracks of a Soundcraft Ui mixer](#tracks-of-a-soundcraft-ui-mixer)). The same on the Sequences view.
* **Mute** (the box after Continue, only for a sound): the cue plays its sound, but nothing of it is heard;
  it runs as always, its moments for At too. It can be switched while the stack is live. A later cue of the
  same sound without Mute is heard; the muted one stays unheard on its way down, and Stack off stops it. A
  project with a muted cue is a file of version 11.
* **At** (the column after Down) lets a cue come at a moment of a sound instead of GO. Press it, choose
  the sound and type the moment (`1:15.5`, or `75.5` in seconds), or press **Now** while the sound plays to
  take where it is; **Use** gives the cue that moment, **None** takes it away. While the stack is live and
  the cue before is on, the cue comes the moment the sound gets there, as if GO was pressed then. GO still
  goes earlier, and the next cue with a moment then waits for its own. A wait does not take the stack on
  to a cue with a moment. Past several moments at once (a double click further on in the wave), the stack
  goes straight to the last of them; sent back (a double click further back, a loop that begins again),
  it goes back to the cue of the moment the sound is at now. While the sound is quiet its cues wait for
  GO, and the stack never goes round from the last cue by a moment. At works while the stack is live
  too: the moments are set in a rehearsal. The wave of the sound that plays has a thin amber line at the
  moment of each of its cues, and the Current card says which moment the stack waits for. A project with
  a moment is a file of version 10.
* **Learn** (beside Add cue) sets the moments in a rehearsal with the music: while it blinks, each GO
  stores where the sound that plays is (of several, the one that started last) into the cue it goes to,
  and goes on also while a fade or a wait runs, so GO can be pressed on the beat. Press Learn again to
  stop; Stack off stops it too. A GO while no sound plays leaves the cue's moment as it was, and Undo takes
  a learned moment back.
* **Disabled** (the box at the end of a row) skips a cue without taking it out: the stack passes over it
  with GO, Next, Back and when it goes on by itself, and going live starts at the first cue that is not
  skipped. It can be switched while the stack is live too, as a way out when something goes wrong, but not
  for the cue that is on. A skipped cue is faint and its number struck through, on Main's wheel too, and a
  project with one is a file of version 9.
* **The kind and the number** of a cue are in the color of its kind, as a key that is half lit: channels
  and fixtures green, memories red, groups amber, effects violet, sounds cyan, releases white. So a long
  stack reads at a glance, also while it is live. The same on the
  Sequences view.
* **Back** sets up the cues that are on as if the stack had come to that cue from the top: from the last
  cue before it without Add, through the cues that add. Round from the last cue, the first one starts
  afresh, with nothing on under it.
* A fader the stack is moving and that you move by hand is yours from then on. Clear brings every fader down
  and takes the stack off its cue; it stays live, and GO starts with the first cue again.
* **Cue numbers** look like 1.0, 2.0, 2.1. A new cue gets the next whole number. Drag a cue by its grip
  (or use the arrows at the end of its row): it gets the number of the cue it now follows with the digit
  after the dot one higher, and the others keep theirs. Moved between 2.0 and 3.0, a cue becomes 2.1.
  Type a number to change it: if another cue has it, that one gives way (3.0 becomes 4.0, 3.1 becomes 4.1).
* **+** at the end of a row puts a new cue right after that one: the cue Add cue would make after it
  (the next channel, memory, fixture or group, with the same times), numbered to fit there. After 2.0, with
  3.0 behind it, it becomes 2.1; if 2.1 is there already, that one gives way to 2.2.

## Effects

An effect is a shape the desk works out anew all the time, on lights of the stage plan, instead of steps
that are stored: the heads go round in a circle, a sine runs through a row of lights, the lights go through a
rainbow, bars move over the cells of LED bars. It works on the lights of a group or on lights chosen on the
plan; a pixel effect works on every cell of its lights, by where the cell is on the stage. A project has up to
64 effects. The desk program plays them, so every output, every device and the Stage view get the same.

![The Effects view: the effects of the example 7 Effects, four of them running, and effect 1, a circle of the heads, in the editor with its preview](docs/screenshots/effects.png)

### The Effects view

* **The row of effects** at the top: a fader for each effect, its key under it and its name under the key,
  as many as the window has room for, with page keys at the right (1-16, 17-32 and on).
  * **The fader** says how much of the effect there is: how big a movement, how deep an intensity, how much
    of a color (for a movement around a center of its own, also how far the heads have gone there). Raised
    from zero, it starts the effect at once; down at zero, the effect stops.
  * **The key** moves the fader: up to full in the effect's fade-in time, which starts it, and pressed again
    down to zero in its fade-out time, which stops it. Pressed while the fader is on its way, it turns back
    from where it is. It is faint where there is no effect, half lit for an effect that is out, fully lit
    while it runs, and blinks while it moves the fader.
  * **A sequence** can play an effect too, with an effect step (see the Sequences view): as strongly as the
    step is up, dimmed by the fader of the sequence's memory. It leaves the effect's fader where it is, and
    the higher of the two counts. The key is lit while a sequence plays the effect, and the line at the top
    of the view says "played by a sequence"; the key still brings the fader up and down.
  * **The name** under a key chooses the effect for the editor; the chosen one is framed. A number without an
    effect shows a dash.
* **Record** beside the row has **Copy** and **Del**, as on Main: Copy, the effect to copy, then the number
  for the copy (which the editor then shows); Del, then the effect to take out. Undo brings it back.
* **To make an effect,** press the key (or the dash) of a number without one: the view offers Movement,
  Intensity, Color and Pixel, and the effect is made with the first shape of its kind, on every light of the
  plan that can do it, at a speed that suits it. (On Main, the key of a number without an effect only says
  so.)
* **Live** (over the editor): the speed, size and spread of this run, on lying faders. They change the run
  only, not the effect, and work also while the stack is live; nothing of it is saved or undone. **↺**
  takes the effect's own values again, **Keep** stores these in the effect. The speed fader goes in
  octaves: twice as fast is always the same way further. While Rate is lit, a line says how fast the effect
  really runs.
* **The editor** changes the effect itself: its name, kind and shape; its lights (**These**: a list of the
  lights of the plan that can do what it does, with All and None; or **A group**); its speed, size, spread,
  order, blocks, wings, direction and fade times; and what its shape needs besides: how long a pulse is on,
  how wide bars are, an angle, a center, a point, colors. Every change is stored at once, and is a step of
  Undo. An effect still called after its shape takes the name of a shape chosen next. (Record beside the
  row copies an effect and takes it out.) While the stack is live the editor is locked.
* **The preview** (at the right) is one cycle of the effect as the desk program plays it: a movement as the
  path of each head over its pan (across) and tilt (down) with a dot where it is now; an intensity as a bar
  for each light; a color as a swatch for each light; a pattern as the cells of its lights where they are on
  the plan. While the effect runs, the preview goes with it, at the live speed, size and spread. For a
  movement around a center of its own a cross shows the center, and a press or a drag on the preview puts it
  there (not while the stack is live). It never flashes more than 3 times a second: a faster effect is shown
  slower, and the line under it says so. With reduced motion (a setting of the device) it stands still.

### Kinds and shapes

* **Movement** (heads, lights with a pan or a tilt): circle, figure 8, pan swing, tilt swing, line (at an
  angle: 0 pans only, 90 tilts only) and random. **Size** is how big, as a share of each head's own range
  (10 % of a pan of 540° is 54°). It moves around where the head points, or with **Center: its own** around
  a pan and a tilt of its own, in per cent of the range: a new movement goes around the middle (50 and 50),
  and a press or a drag on its preview moves the center. In 16 bits where the head's kind from the fixture
  library has fine channels.
* **Intensity** (every light that gives light): sine, ramp up, ramp down, pulse (on for a share of each
  cycle) and flicker (a new random level every cycle). **Size** is how deep the wave goes: 100 from dark to
  full, 20 from 80 % to full.
* **Color** (lights with color channels, or with a color wheel): rainbow (round the color circle), chase
  (from one color of its list to the next at once) and fade (from one to the next, gently). **Size** is how
  deep the colors are: 100 full colors, 0 white. Without colors of its own a chase or a fade goes between
  red and blue.
* **Pixel** (the cells of LED bars and of any light, by where they are on the plan): gradient, ripple (rings
  from the middle of its lights, or **From: a point** of its own), plasma, rainbow, bars and sparkle.
  **Size** is how big the pattern is, as a share of the lights it covers; for sparkle, how many cells
  flash. An **angle** says which way a gradient, bars and a rainbow run: 0 to the right, 90 to the front.
  The first color is the pattern's, the second what lies under it: bars of the first color on the second,
  or on black with one color only.
* A device such as a hazer takes no part in effects.

### Speed, spread and order

* **Speed** in BPM, cycles a minute: 0.5 to 600 (60 is one cycle a second).
* **Spread:** 0 % all lights together, 100 % spread evenly over one cycle: a chase.
* **Order** of the lights: by address, from the left of the stage, from the middle outwards (lights as far
  out go together), or at random (the same order every time; **Choose anew** picks another, and other random
  shapes).
* **Blocks:** lights next to each other in the order that move as one; 2 makes pairs.
* **Wings:** the lights split into parts, every second one the other way round; 2 runs from both ends to the
  middle, or out from it.
* **Direction:** forward, or back: the other way through the lights, the other way round a circle.
* **Fade in** and **Fade out:** the seconds the key takes to move the fader from zero to full and back. A
  movement grows from where the heads point, and shrinks back to it.
* **Rate** (Main, General): while it is lit, every effect runs at its speed times the tempo over 120 BPM. At
  120 BPM nothing changes; an effect of 120 BPM then makes one cycle per beat, one of 60 BPM one every two
  beats. An effect goes on from where it is when the tempo changes; the fade times stay.

A pixel effect has no spread, order, blocks or wings: its pattern lies on the plan.

### How an effect mixes with the rest

The desk works out what goes out in this order, many times a second:

1. The faders, the memories, the sequences and the stack give their light, as always: the highest wins.
2. The effects work on that light, light by light (cell by cell for a pattern):
   * **An intensity effect: the highest wins.** A light gives what the rest gives it or what the effect gives
     it, whichever is higher, like one more fader that moves by itself. Over a look at 60 % a sine at full
     goes from 60 % up to full and back; on a dark light from dark to full. A light's intensity is its
     dimmer, or for a light without one its colors together, which keep their mix; a light that has no color
     yet comes up white. The closed shutter of a head opens while the effect lights it. Of two intensity
     effects on one light, the higher wins.
   * **A color effect takes over the color.** Its fader goes from the color the rest gives (at zero) to the
     effect's color (at full), mixing the two in between. A light that nothing else lights, the effect
     lights itself, as bright as its fader: a rainbow alone lights its pars, at full with its fader at full.
     A light the rest lights keeps its brightness and changes only its color (one that is nearly dark comes
     up to the fader smoothly, without a jump). On a light an intensity effect plays, it gives only the
     color: the intensity effect gives the brightness, so a chase under a rainbow runs in the rainbow's
     colors. The closed shutter of a head opens where the effect gives the light. The colors are mixed as on
     the Fixtures view: a white channel takes the white part, an amber one the warm
     part, ultraviolet none, so UV goes out as the effect takes over; a color wheel turns to its nearest slot.
     Of two color effects on one light, the one started last takes it.
   * **A pixel effect** gives every cell a color and a brightness: the brightness like an intensity effect,
     so it lights dark cells itself, and the color like a color effect, but only as much as the pattern is
     bright there. Where the pattern is black, a cell stays as the rest has it: red bars over a blue look are
     red bars on blue.
3. A solo, each light's limit, the master and Black out act on the result, on light only. With SOLO UP held
   only those keys' lights shine, so every effect elsewhere is dark; a channel held with SOLO DOWN is dark,
   effects included.
4. Pan, tilt and the wheels come from whatever took them last (see the Fixtures view above), and **a movement
   effect moves a head from there**, its fader scaling the size. Of two movements on one head, the one
   started last takes each axis it uses: a tilt swing started after a circle takes the tilt, the circle
   keeps the pan.

Black out, the master and a solo never stop a head or make it jump, and an effect runs on under them, so it
comes back in step. The effect keys are not solo keys: a solo leaves them as they are.

### Effects on the Stage view

![The Stage view with effects: the pars in a rainbow over the blue of memory 2, red bars on both LED bars, the heads lit by memory 1 going round in a circle, with their beams](docs/screenshots/effects-stage.png)

The Stage view shows what the effects give, as for any light, about 30 times a second while an effect runs.
A head with pan and tilt shows a short beam: turned by its pan (with the pan in the middle it points to the
audience, and the 540° of a usual head turn it round one and a half times) and longer the more it tilts, in
the color of its light, dashed while the light is dark. It is a sketch, not the real beam (the plan does not
know how a head hangs): the preview of the Effects view shows a movement exactly. A change of a light in a
running effect shows at once and then stays at least a sixth of a second, so a light never flashes more than
3 times a second there; a light whose effect flashes faster gets the mark ⚡ with how many times a second it
really does, and flashes on the screen at most twice a second, as a strobe does.

### From another program

The playback of every effect can also be worked from outside the page: start and stop, the fader, the speed,
the size and the spread, as "effect 3 speed = 120 BPM". A MIDI controller does it with its keys and faders
(see [MIDI controllers](#midi-controllers)); another program as [DEV.md](DEV.md#effects-from-outside) says.

## Sounds

A sound is a file (music, a noise, a voice) that the desk program plays on a device of its PC: MP3, WAV,
AIFF, FLAC, M4A, AAC or WMA. A project has up to 64 sounds, numbered like the effects. The desk program mixes
them itself, as many at once as are up, and plays them out of one device; the files stay with the desk
program and are saved with the project (see [The files](#the-files)). Sounds play on Windows; on another
system the desk program plays them without a sound. A sound can also be a track of the USB stick of a
Soundcraft Ui mixer, played by the mixer's own player beside the files (see
[Tracks of a Soundcraft Ui mixer](#tracks-of-a-soundcraft-ui-mixer)).

### The Sounds view

* **The row of sounds** at the top: a fader for each sound, its key under it and its name under the key, as
  on the Effects view, all of them cyan, as the page keys are.
  * **The fader** is the sound's level: at full it plays at the sound's own level. Raised from zero, it
    starts the sound from its start; down at zero, the sound stops. A sound without a loop takes its fader
    down with its end: in its fade-out time before its end, as its key would, so that it fades out, and at
    its end without one. Its key, or its fader from zero, plays it again. Where a cue of the stack holds
    the fader, or a sequence plays the sound, the fader stays up at the end, and the stack or the sequence
    decides (see the Stack view).
  * **The key** moves the fader: up to full in the sound's fade-in time, which starts it, and pressed again
    down to zero in its fade-out time, which stops it. It is faint where there is no sound, half lit for a
    quiet one, fully lit while it plays, and blinks while it moves the fader.
  * **The name** under a key chooses the sound for the editor; the chosen one is framed. A number without a
    sound shows a dash.
* **To add a sound,** choose a number without one (its key or its dash) and press **Choose files…**, or drop
  files onto the view. Each file goes to the desk program, and becomes a sound named after the file;
  several files fill the next empty numbers too. While the stack is live nothing can be added. While a
  Soundcraft Ui mixer answers, an empty number also takes a **track of the mixer's USB stick** (see
  [Tracks of a Soundcraft Ui mixer](#tracks-of-a-soundcraft-ui-mixer)); without the mixer there is no such
  field.
* **Record** beside the row has **Copy** and **Del**, as on Main: Copy, the sound to copy, then the number
  for the copy (which the editor then shows); Del, then the sound to take out. Undo brings it back.
* **Sound** beside them has MUTE, LISTEN and the volume of all sounds, as on Main (see
  [Sound on Main](#sound-on-main)).
* **The editor** changes the sound itself. Every change is stored at once and is a step of Undo; a sound
  that plays goes on with it, and follows a slider while it moves. The sliders are lying faders, as on the
  Effects view: a drag, the mouse wheel (with Shift one step at a time) and the arrow keys move them. While
  the stack is live only the level, pan, speed, pitch, echo with its repeats and reverb with its room size
  can be changed; the rest is locked.
  * **Name:** what the key, the steps and the cards call it.
  * **File:** its name and how long it is. **Another file…** puts another file in its place and keeps the
    rest; a sound that plays starts again with it.
  * **Plays from** and **to**, in seconds of the file: empty "to" is the end of the file. **Loop**: after
    its end it starts again from its start, until it is stopped.
  * **Level:** its own level in decibels, from -100 dB (nothing) to 0 dB (the file as it is, at first),
    to a tenth of a decibel: typed, or with the slider, where half way is -12 dB (the gain goes with the
    square of its place, as on the fader of all sounds). A double click on the slider puts it back to 0 dB.
  * **Fade in** and **Fade out:** the seconds its key takes to bring it up and to take it down.
  * **Pan:** from -100 (all left) to 100 (all right); a double click on the slider puts it in the middle.
  * **Speed:** 0.5 to 2 times, with 1× in the middle of its slider; its pitch stays. **Pitch:** -12 to 12
    semitones; its speed stays. A double click on a slider puts it back to 1× or 0.
  * **Echo:** how loud its echo is (0 for none), and its repeats: every so many seconds, each so many per
    cent of the one before. **Reverb:** how much of a room's sound it gets (0 for none), and its **room
    size**: from a small room (0) to a big hall (100), which rings longer. A double click on the sliders of
    echo and reverb takes them off, on Room size it puts the middle.
  * **A track of the mixer** has only its name, the track (playlist and name), its level and its fade
    times: the mixer's player plays it as it is, and while it plays its level comes on top of PLAYER L/R
    on the mixer (see [Tracks of a Soundcraft Ui mixer](#tracks-of-a-soundcraft-ui-mixer)). A new track is at 0 dB, as every
    new sound: the volume of all sounds keeps it down.
* **The wave** over the editor is the sound's whole file: at the left where the sound is (where it starts,
  while it is quiet), at the right how long the file is, and a line that glides along while it plays. Before
  From and after To the wave is dim; what the sound has played is brighter than what is still to come.
  A track of the mixer has an empty wave, a flat line, with the line where it is and its length as the
  mixer tells while it plays. The pointer over the wave shows the time in the file where it points.
* **To play from elsewhere,** double click the wave of a sound that plays: it goes on from that place.
  With the focus on the wave, the arrows go 5 seconds back and on, Page Up and Page Down 30, Home to its
  start. A fade at its end that had begun is taken back. The Stack view and the Stage view show the wave
  of the sound that plays over their steps, with the same double click: the one that started last, and
  while none plays the one that played last.

### The device

The **Sound** tab of Settings chooses the device the sounds play on: one
at a time. **Windows' default device** follows Windows, also when that changes. A device of more than two
outputs (a sound card of four, say) also shows **Outputs**: the two of them the sounds go out on (1 and 2,
3 and 4 and on); a device of two has no such field. A device chosen that is not there plays nothing until
it is back; Settings says so. The device and the outputs
belong to the PC, not to the project, and only a page on that PC can change them; a page on a phone or a
tablet says where they are chosen. To play through a mixing desk, a cable from the device's output to the
mixer's line input is enough.

**LISTEN** plays what goes out once more on a second device of the PC, in stereo: headphones or the
speakers of the PC, while the device of the sound feeds a mixing desk. Choose that device in the Sound tab
of Settings (**Listen on**); the key LISTEN of the sound section (Main, the Sounds view, the Stage view and
the Stack view) switches it on and off, also from a phone: lit while it plays there, half lit while that
device is not there. It plays every sound, under the volume of all sounds and MUTE, a moment after the
device of the sound. The device of the sound itself is not opened twice (Settings says that it plays there
already), and a device that is not there is looked for again. Like the device, it belongs to the PC.

### Tracks of a Soundcraft Ui mixer

A Soundcraft Ui mixer (Ui12, Ui16, Ui24R) has a player of its own for the tracks of a USB stick in it. A
sound can be such a track: it plays on the mixer's player, while the sounds of files play on the PC beside
it, and each sound knows which it is. The desk works the mixer over the network (its Wi-Fi or a cable):

* **Mixer** in the Sound tab of Settings: the mixer's IP address (10.10.1.1 on its own Wi-Fi, else the one
  your network gave it; a port after a colon when it is not 80). **Find** looks through the networks of the
  PC for it, in about three seconds: one mixer found is taken at once, of several one is chosen, and when
  none answers Settings says so (the address can still be typed). Settings says when the mixer answers, and
  what it is; one that does not answer is looked for again every 2 seconds. The mixer belongs to the PC,
  like the device.
* While it answers:
  * its tracks play on its player, **one at a time**: a track that starts ends the one before. The sounds
    of files play on beside them. Without the mixer a track does not play, and the line at the top of the
    Sounds view says so.
  * the mixer's **PLAYER L/R** are the sound section of the desk, both ways: their fader is the volume of
    all sounds, their mute MUTE. When the desk finds the mixer, it takes them as they stand. Moved or muted
    on the mixer (its own page, a tablet), the SOUND fader of the desk follows and MUTE lights; moved on the
    desk, PLAYER L/R follow on the mixer.
  * the track that plays adds its own level (see the editor) and its fader on top: with the volume at
    -20 dB, a track at -6 dB with its fader at full plays at -26 dB. So its key's fades, the up and down
    times of the stack and the fade at its end move PLAYER L/R on the mixer. When the desk stops a track,
    PLAYER L/R go to the bottom first and stay there while the player sounds on (it does, a moment after it
    is told to stop), about 1.5 seconds; then they stand at the volume again. A track that starts meanwhile
    gets its level as soon as the mixer plays it. A hand on PLAYER L/R while a track plays sets the volume to what is left after the
    track's level and its fader. After the network to the mixer was gone a moment, the desk gives the
    mixer what the desk has. A double click on the wave jumps in the track.
  * the player plays a track and stops at its end: the desk sets it to manual, not one track after another.
  * keys, faders, the cues of the stack and the steps of sequences start and stop a track as they do a file. A
    step offers every sound, and after them the tracks of the playlist that are no sound yet. Pause of the
    stack pauses the mixer's player too.
* **Playlist** (in the Sound part, while the mixer answers): All tracks at first, or a folder of the stick,
  or Tracks outside the folders: the tracks the steps and the Sounds view offer. **Add all its tracks**
  makes every track of it a sound named after the track, in the free numbers from 1 on; a track that is a
  sound already is left out.
* **To add a track,** choose an empty sound on the Sounds view, then under Choose files… a track of the
  mixer: it becomes a sound named after the track. Its playlist and name stay as the mixer has them; a
  project keeps those, and on a stick without the track it stays quiet, and the line at the top of the
  Sounds view says so.
* The mixer's player plays on by itself: when the network to the mixer is gone a moment, the desk takes the
  track as playing until the mixer answers again; when the desk program is closed, the track plays on.

### Sound on Main

* **Sound** stands at the right of General: **MUTE** and **LISTEN** over the volume of all sounds. MUTE
  blinks while it is on: no sound goes out, and the sounds play on unheard. LISTEN: see
  [The device](#the-device). The volume goes from -100 dB (nothing) at the bottom to 0 dB at the top, where
  every sound plays at its own level; half way is -12 dB, as the gain goes with the square of the place. It
  is -20 dB at first, so that nothing starts loud; the volume stands on top, and a double click puts it back
  to -20 dB. Both belong to the evening, like the master fader: a project keeps neither. While a Soundcraft
  Ui mixer answers they are its PLAYER L/R, both ways (see
  [Tracks of a Soundcraft Ui mixer](#tracks-of-a-soundcraft-ui-mixer)).
* **The sound section** is also on the Sounds view, on the Stage view (in a line under the master
  section) and on the Stack view (lying, beside the master section).
* **Sound keys** of the independents: when Settings shows them, keys that start and stop their sounds (see
  the independents above).
* **The row of sounds:** when Settings shows it, a row of sound faders under the memories (and the
  effects), with page keys for the 64 sounds, as on the Sounds view.
* **Copy and Del** of Record work on the keys of the sounds and of the effects too, as on the memories: a
  sound goes onto a sound and an effect onto an effect; a memory key pressed instead says so.

### Sounds in the stack and in sequences

A cue of the stack or a step of a sequence can play a sound (its kind **Sound**), at a level from 0 to 255
(255: the sound's own level), in its up time and down time instead of the sound's fade times. **Continue**
lets it play on through the cues or steps after it until its end or a release; without it, the next one
without Add takes it down, as any other. See the Stack view and the Sequences view.

### The files

A file added on the Sounds view is kept by the desk program (in the folder sounds beside its LAN password,
under its contents: the same file added twice is kept once). Save writes the project's sounds into a folder
beside the project's file: "Show.json" gets "Show sounds" with the files under their names. Load takes them
from there, so a project with its folder can go to another PC. A project with sounds is a file of version 9.
**Export** of the project takes its sounds into the ZIP (in `sounds`), and **Import** brings them back with
the project, also on another PC.
A sound whose file is missing stays silent: played, the line at the top of the Sounds view says that its file
cannot be played. A track of the mixer has no file: a project keeps its playlist and its name
("playlist" in the file), and the ZIP of Export has nothing of it.

### Keys and MIDI

The sound keys (1 to 64), the sound pages, MUTE and LISTEN have shortcuts, and a MIDI controller can press
them. Its faders can move the sound faders (fader.sound.1 to 64) and the volume of all sounds
(fader.sound-volume: -100 dB at the bottom, 0 dB at the top, -12 dB half way, as on the page).

## Screens, phones and tablets

### Window width

A window at least 1850 pixels wide shows 32 faders in a row, so 64 channels and 32 memories
at a time. A narrower window shows 16 in a row (32 channels and 16 memories) and twice as many page keys.
Settings can fix another number, and the rows, for Main. The page keys stand in one column at the right, as big
as every key; when they are too many for the height, the column scrolls: with the wheel, a finger, or the
mouse held down on it and pulled (a press that does not move presses its key). The rows of faders fit the
window and never scroll sideways (on a low window, where the independents take two columns, the faders get
a little narrower), unless a channel size chosen in Settings makes them wider than it. What has no 32 faders
to fit in a row keeps the size of a narrower window on a wider one too: the strips of the Fixtures view, and
the memory keys at the top of the Memories and Sequences views (as big as their place in the line allows, a
little room between them). The ranges on the page keys ("33-64") are at least 12 pixels high; a long one
breaks after its dash.

### Small screens

In a window narrower than 1200 pixels or lower than 600 pixels (a phone, a tablet) the sections stand under
each other and the page scrolls. The page keys lie under the section titles, in as many lines as they need;
on the Memories and Sequences views a line parts them from the memory keys under them. The stack keeps its
five keys in a column beside its wheel, also on a phone, and the independents and Record lie down, their
keys at the left under the title (Edit first). Narrower than 800 pixels (a phone), a row of 16 faders is
shown as two lines of 8, and the top bar takes three lines (up to 470 pixels, its ten links stand in two
rows of five). The numbering does not change: a page is still 32 channels or 16 memories. Narrower than 1366 pixels the top bar takes two lines, the links to the views under the rest.
The limits are in `frontend/src/app/desk/desk-types.ts`, and only there; the two rows of links are the top
bar's own. They count the page's pixels: at a zoom of 120 % (Settings) a window of 1440 pixels has 1200.

<img src="docs/screenshots/main-phone.png" alt="The Main view on a phone: the sections under each other" width="260">

### A phone or a tablet (LAN)

The desk can be used from another device in the same network as the PC.

* Press **LAN** in the page on the PC. It is lit, and under the boxes a line appears with the address and
  the password, for example `LAN 192.168.1.20:5080 Password K7M2Q`.
* On the phone, open that address in the browser (`http://192.168.1.20:5080`). The page asks for the
  password once; the phone remembers it. Small letters and spaces do not matter.
* Press LAN again and it is off: every other device loses the desk at once, its connections are refused, and
  no password is shown. LAN is off whenever the desk starts. Only the PC can switch it; the page on a phone
  has no LAN key.
* The password has 5 letters and digits (no 0, O, 1, I or L, which are easily mixed up). It is made once and
  kept in `%LOCALAPPDATA%\Lumenet\lan-password.txt`, so a phone that knows it gets in again next time.
  Settings can type another one (5 to 20 letters and digits) or make a new one; delete that file for a new
  one too. Every phone then has to give the new password, and the connected ones lose the desk.
* When the PC is in more than one network (a cable and Wi-Fi), Settings can choose the address that phones
  use: the desk then lets devices in at that address only. Without a choice it lets them in at every address
  and shows the first. A chosen address that the PC no longer has (the router gave it another one) counts as
  no choice until it is back. The choice is kept in the same file.
* A phone is remembered by a cookie that holds a long random key, not the password, so the cookie cannot be
  guessed. Guessing the password is slowed down instead: after 5 wrong passwords within a minute a device
  has to wait, and after 20 from all devices together every device has to wait. So someone in the network
  who keeps sending wrong passwords can stop a new phone from getting in; a phone that got in before is not
  asked again, and the PC never needs the password.
* The first time the desk program starts after this change, Windows asks whether `DmxBox.Server` may use the
  network. Allow it for **private** networks only. If that window was closed with "Cancel", Windows blocks the
  program; then allow it under Windows Security, Firewall, "Allow an app through firewall".
* The phone gets a built copy of the page from the desk program (port 5080). `run.cmd` builds that copy when
  the page's files have changed since the last time; while the desk runs, `npm run build` in `frontend`
  brings it up to date. The PC itself keeps using http://localhost:4200, which shows every change at once.

### Moving a fader

Drag the handle, or press in the slot to bring the handle there. The mouse wheel moves it
in steps of 5 (with Shift: 1). With the keyboard: the arrows, Page Up, Page Down, Home and End.
With a finger a fader moves only by its handle: a touch in the slot does nothing, so a thumb that scrolls the
page cannot move a light. In the small-screen layout the mouse wheel scrolls the page and leaves the faders alone.

## Settings

The settings and the keyboard shortcuts belong to the device: its browser keeps them, every project shows
them, and a project that is loaded leaves them as they are. **Export** takes them to another device, and
**Import** brings them from one (see above). The number of universes belongs to the project, the autosave,
LAN and the backups to the desk program.

![The General tab of Settings](docs/screenshots/settings.png)

Settings has tabs, in the middle of the window: **General** with two sections side by side (the main view
with the universes, the zoom and the autosave, and the views in the top bar; on a phone one under the
other), and each of the others one section of the same width: **LAN** (the address and the password for
phones and tablets), **Sound** (the device of the sounds and its outputs, the device to listen on, and a
Soundcraft Ui mixer, typed or found with Find, with its playlist; see [The device](#the-device) and
[Tracks of a Soundcraft Ui mixer](#tracks-of-a-soundcraft-ui-mixer); on the PC only, a phone says where it is
set up), **Colors** (the colors of the keys), **Keyboard** (see [Keyboard](#keyboard)), **Outputs**
(the outputs of the project, see [The outputs](#the-outputs)), **RDM** (the lights with RDM on the lines of
the boxes, see [RDM](#rdm)), **Backup** (see [Backup](#backup)) and
**About** (the version, an update, and what Lumenet is made with; see [Updates](#updates)). The address names
the tab (`/settings#keyboard`), so a reload keeps it, and the arrow keys go from tab to tab. Every label
stands beside its field. The MIDI controllers and OSC have a view of their own (see
[MIDI controllers](#midi-controllers)).

* **Channels on a page** (Main): Auto (a row of 32 or 16 by the window, as above), or 8, 16, 32, 64 or 128.
* **Rows of channel faders** and **rows of memory faders** (Main): 1 or 2 of each, at first 2 and 1. A
  channel size is spread over the rows of channels, and a row of memories is as long as a row of channels:
  with 8 in two rows, rows of 4, and 4 memories on a page, so 64 channel pages and 32 memory pages. The two
  sections share the height in the proportion of their rows. The faders keep the width the window gives
  them; when the rows are then wider than the window, they scroll sideways, the channels and the memories
  each on their own, with a big scrollbar. The Memories and Sequences views keep the rows of the window.
* **Steps the stack wheel shows** (Main): 1, 3, 5, 7 or 9; at first 7. (A browser that kept its settings
  before 7 was the first keeps the 3 it had; choose 7 here once.)
* **Row of effect faders** (Main): None (at first), or 1: a row under the memories, as high as a row of
  memories (see [Effects](#effects)).
* **Row of sound faders** (Main): None (at first), or 1: a row under the memories and the effects (see
  [Sounds](#sounds)).
* **Keys of the independents:** how many green, red, violet and sound keys the column shows, 0 to 32 of
  each (at first 10, 10, none and none); with 0 of all, Main has no independents.
* **Universes of the project:** 1 to 8, of 512 channels each (see [Universes and outputs](#universes-and-outputs)).
  The project keeps it, and the list shows the project's number until the desk program has taken a new one.
  Locked while the stack is live.
* **Zoom:** the minus and plus keys make the whole page smaller or bigger, from 50 % to 200 % in steps of
  10 %, as the browser's own zoom does, on this device only; Reset goes back to 100 %. A zoomed page has
  fewer of its pixels in the window, and lays itself out for that: at 120 % a window of 1440 pixels is
  laid out as one of 1200 (see [Small screens](#small-screens)).
* **Autosave:** On or Off, and how often: every 5 seconds (at first), 30 seconds, 1 minute or 5 minutes. Off,
  the desk program keeps nothing and takes away the file it kept. The desk program keeps the choice in
  `autosave-options.json` beside the autosave file. On the PC only.
* **LAN:** while LAN is on, the address phones use and the password; see above. On the PC only.
* **Views in the top bar:** the views in the order of their links, each with a tick; a view that is not
  ticked leaves the top bar (its keyboard shortcut still opens it). Settings is always ticked, so that the
  others can come back. ▲ and ▼ move a view one place to the left or right in the top bar (the keyboard focus
  stays on the arrow), and ↺ beside the title brings back the views of the start, every one but Notes, in the
  usual order. **Setup…** offers the setups of
  [the first start](#the-first-start) again: the views and rows of Main for a kind of show. A desk without Main
  in its top bar opens on the first view that is. A view that came later than the order kept (Notes,
  Effects, MIDI) stands after the view it follows in the usual order.
* **Colors of the keys:** a color for each kind (channels, memories, the color a memory with a
  sequence runs into, effects and sounds), which is that of all its keys, page keys and faders, and one for each other key, group
  by group: Black out; Clear, Solo up, Solo down, Rate (with its fader) and Tempo; Stack, Back, GO, Pause, Next and Add cue (GO also colors the
  cue that is on, Next the cue GO goes to); the keys of Record, of the top bar, Edit of the independents,
  the keys of the Sequences view, of the Effects view, of the Stage view and of the Notes view. A press on a color opens the colors the keys have at
  first (green, red, yellow, amber, white, blue, violet and cyan, the one it has ringed) to take one of them, and Other for
  any color; while that one is picked, the desk shows it at once. A press anywhere else, or Esc, closes them.
  Each has a key back to its usual color, and ↺ beside the title takes all of them back. At first Clear, Solo
  and Pause are white, Black out blue, GO green, Stack and Mute red, the effects violet, the sounds cyan
  and the others amber.

The keys that take something out, ✕ at the end of a step in the Sequences and Stack views and of a shortcut
in the Keyboard tab, are red.

### Backup

A backup is one ZIP of everything Export takes: the project with its sounds, the stage, the settings and the
keyboard of the PC, and the MIDI. The Backup tab sets where they go and when:

* **Folder:** a whole path on the PC. A folder that [Google Drive for desktop](https://www.google.com/drive/download/)
  syncs (in "My Drive") takes every backup to Google Drive by itself, and so do the folders of OneDrive and
  Dropbox; Lumenet signs in to nothing. Where Google Drive for desktop is found, **Use its folder** fills in
  `My Drive\Lumenet backups`.
* **Back up when the project is saved:** On (at first) makes one with every Save on the PC; the line at the
  top says "saved and backed up", or why it was not.
* **Backups to keep:** 30 at first, 1 to 1000; older ones go. A backup is named after the project and the
  time ("Lumenet backup - Concert - 2026-11-26 14.05.33.zip"), and only files named so are ever deleted.
* **Back up now** makes one at once. Under it the tab says how many the folder holds and when the newest
  was made.

Import brings a backup back, part by part. The desk program keeps the setup in `backup.json` beside the LAN
password. On the PC only.

### Updates

The About tab shows the version of Lumenet, and asks GitHub for the newest release when it opens, never
otherwise. A newer one shows what it brings (the messages of its commits) and **Update to** its version:
after a question, Lumenet.exe downloads it, checks it against the size and the SHA-256 that GitHub gives,
puts it in its own place and starts it, and the page loads again once the new one answers. The desk stops
for a few seconds and starts with every fader down, so the lights go out: update between shows, with the
project saved. The file of the old version stays beside it as `Lumenet.exe.old` until the new one runs.

* The desk of run.cmd says that it runs from the source code: git pull and run.cmd bring the new version.
* A Lumenet.exe made on a PC (publish.cmd) does not know where its releases are, and says so.
* A Lumenet.exe in a folder it may not write (Program Files) says so; the new file then goes there by hand.

On the PC only. The tab also lists what Lumenet is made with and their licenses; the license of Lumenet
itself is not chosen yet.

## Keyboard

The Keyboard tab of Settings gives a shortcut to any key of the desk: the view links, New, Save, Load,
Import, Export, LAN, the boxes, Clear, Solo up, Solo down, Black out, Rate, Tempo, the keys of the stack and of
Record, those of the Sequences view, of the Effects view and of the Stage view, Edit of the Notes view, Edit
and every key of the independents, every page key and U key, the key under every channel, memory and effect
fader, Undo and Redo. It lists
the channel keys and U keys of the project's universes; the shortcuts of the others are kept for a project
that has them. The shortcuts belong to this device, as the settings do. (Until 2026-11-24 the shortcuts had a
view of their own, Keyboard, with tabs; its address now opens Settings.)

Some keys have a shortcut from the start, as in other programs:

| Key | Shortcut |
|---|---|
| GO of the stack | the space bar, as in QLab and most programs that play cues |
| Pause, Next, Back | P, N, Backspace |
| Stack (live) | S |
| Black out | B |
| MUTE, LISTEN | M, L |
| Tempo (tap) | T |
| Save, Load | Ctrl + S, Ctrl + O |
| Undo, Redo | Ctrl + Z, Ctrl + Y |

Each keeps its shortcut until it gets another (or none: then it stays without), and a usual shortcut that
another key took stays with that key. The space bar is GO also while a key that was clicked last has the
focus; a box or a choice that has the focus keeps its space bar and ticks. **↺** beside the title of the panel
gives every key the shortcut it has at first and takes away those set on this device: the first press asks (↺
turns amber), and a second one within four seconds does it.

![The Keyboard tab of Settings: the keys of the project and LAN open, Undo, Redo, Save and Load with their usual shortcuts](docs/screenshots/keyboard.png)

* The kinds of keys stand one under the other (Views, Project and LAN, Master, General, Sound, Stack,
  Record, Sequences view, Effects view, Stage view, Notes view, Independents, Channel pages, Memory pages,
  Effect pages, Sound pages, Channel keys, Memory keys, Effect keys, Sound keys), each with the number of its keys and, in brackets, how many
  of them have a shortcut, in green:
  Stack 6 (4). A press on a kind opens its keys under it and closes any other: one is open at a time, and
  none at first; pressed again, it closes. The arrow keys up and down, Home and End go from kind to kind.
  The search field searches all of them: then each kind also shows how many of its keys it finds, in yellow,
  Stack 6 (4) 2; when the kind that is open finds none, the first kind that finds some opens, and when the
  search is emptied, the kind open before it opens again.
* The keys of a kind are listed at the left, their shortcuts at the right. **Set**, then press the
  keys of the shortcut (with Ctrl, Alt or Shift as wanted; Esc stops, Tab and Esc cannot be shortcuts). A
  shortcut that another key had moves over, and the page says so.
* Set tells the left Ctrl and Alt from the right ones ("Right Ctrl + B", "Left Alt + C"), and such a shortcut
  is pressed only with that side. A shortcut without a side (Ctrl + Z and Ctrl + Y of Undo and Redo at first,
  and every shortcut set before the sides were told apart) is pressed with either side; when a press fits a
  shortcut with a side and another without, the one with the side takes it. AltGr, the right Alt of many
  keyboards, counts as Right Alt (the Ctrl that Windows adds to it is left out); a shortcut set with AltGr
  before the sides were told apart shows Ctrl + Alt and still answers to it. Shift has no side, and a Ctrl
  or Alt that was already down when the page got the keyboard has none either: the page did not see which
  one went down.
* **Listen** shows the desk; the key you double-click there is not pressed but picked, and Settings comes
  back with its kind open in the Keyboard tab and the key chosen and scrolled into sight (Esc stops
  listening).
  A single click works as always, a moment later: it opens another view or page of faders, or presses the
  key, so the key to pick can be found first. Enter on a key picks it at once.
* A shortcut is the physical key, whatever letter the keyboard's language puts on it. It presses its key on
  the page as a click would, and a locked key stays locked; when the key is not on the view that is shown (a
  page of faders that is not shown, another view), it does what that key does, except the keys of the
  Sequences view and of the Stage view and Edit of the independents, which
  work only where they are (Keep and ↺ work on the effect the Effects view shows). Shortcuts rest while a note
  or another field is typed into and while a window such as Load is open.

## MIDI controllers

A MIDI controller plugged into the PC (an Akai APC mini, a Korg nanoKONTROL2, a Behringer X-Touch and many
others) plays the desk: its keys press the desk's keys, its faders and knobs move its faders, and the desk
lights its keys and moves its motorized faders back. The desk program takes the controller, not the page, so it
works whichever pages are open, on Windows and macOS (on Linux only the simulators below). A controller with a
DIN cable goes into MIDI IN of a dmxbox 4: the box is a MIDI device of the PC ("dmxbox 4"), and what comes
in there reaches the desk as from any other device; what the desk sends to it goes out of the box's MIDI OUT.

![The MIDI view: two simulated controllers in use and the messages that came in at the left, the faders of the pages shown with their controls at the right](docs/screenshots/midi.png)

The **MIDI** view (**MIDI** in the top bar) sets it up; only the PC can, as the controllers belong to it. At
its left stand the controllers and the messages that came in, at its right every key and fader of the desk.

* **Devices:** the MIDI devices of the PC. A ticked one the desk uses: it opens it, and on Windows no other
  program can have it meanwhile. A device that is pulled out shows "Not plugged in"; when it is back the
  desk takes it again by its name (it looks every 2 seconds). "No output" means its lights cannot be set.
* **Ready-made mapping:** gives a device the mapping of its controller (see below). It takes the place of
  what that device's controls did before, and the desk uses the device.
* **Incoming messages:** the last messages of the devices in use, the newest first, each with what it did:
  "Memory key 5 pressed", "Channel fader 1 of the page shown to 128". In amber when it did nothing: no
  mapping, no page of the desk open on the PC, a fader waiting until the control reaches the desk's value, or
  a refusal with its reason.
* **Keys and faders:** every key and fader of the desk, kind by kind, as the Keyboard tab lists the keys
  (each kind with the number of its keys and faders, in brackets how many have a control, green when there
  are some, and while the search runs how many it finds, yellow when it finds some: Stack 6 (5) 2), each
  with the controls that work it, **Learn** and **✕**. **Learn**, then press or move a control: from now
  on it works that key or fader, and nothing else. Learn again, Stop or Esc stops waiting. **Listen** shows
  the desk, as Listen of the Keyboard tab does: the key or fader double-clicked there is not pressed or
  moved but shown here, its kind open, its line marked and its Learn ready (a fader does not move at all
  while Listen waits). ✕ takes all controls of that key or fader away. The search finds keys and faders by
  their names, and controls by what they are ("cc 48").
* A key takes a note, a controller (CC, pressed above 0) or a program change. A fader takes a controller, a
  pair of controllers for 14 bits (when the fine one, 32 above, follows at once while learning) or the pitch
  bend (the faders of Mackie controllers). A channel of DMX IN works either (see
  [A console on DMX IN](#a-console-on-dmx-in)). Under a fader's control stand how it moves the fader (a fader or
  knob from 0 to 127, or an endless encoder of one of three kinds), **Takes over at the desk's value** and
  **Motor or lights**; under a key's control **Light**.

What a control works:

* **Keys:** every key the Keyboard tab lists, as its keyboard shortcut does, on the page of the PC that was
  used last (a click, a key, the focus): a locked key stays locked (while the stack is live, Record does
  nothing from a controller either), keys rest while a window is open or a key is being picked, and while
  Solo up or Solo down is lit a key held down holds its fader until it is let go. Without a page of the desk
  open on the PC the keys do nothing; the faders still work. A phone or a tablet never presses them.
* **Keys of the pages shown:** the key under the first, second, ... channel, memory or effect fader of the
  page shown; they follow the page keys. The page keys also have a next and a previous.
* **Faders:** a channel, memory or effect fader by its number, or the first, second, ... of the page shown,
  which follow the page keys, so that 8 faders of a controller play any 8 channels. The master; Rate (the
  tempo, 30 to 300 BPM); the speed of a sequence (the lower half 0.1× to 1×, the upper half 1× to 10×, 1× in
  the middle); the live speed, size and spread of an effect. A control's 0 to 127 is the fader's 0 to 255;
  14 bits and the pitch bend are finer. An endless encoder moves a fader 2 of 255 a click.
* **Takes over at the desk's value** (for a fader without a motor): the control moves the desk's fader only
  once it has reached the desk's value or gone past it, so a light does not jump; the incoming messages say
  "waits" until then. After the desk's fader moved some other way (the page, a phone, the stack), it waits
  again.
* **Motor or lights, Light:** the desk sends back what it has. A key's light follows the desk's key: off, half,
  full, and blinking where a Record key waits for it (a controller that cannot blink by itself is blinked by
  the desk). A motorized fader or a knob's ring of lights follows the desk's fader, wherever that changed: the
  page, a phone, the stack. A fader the hand moves gets nothing back for 0.4 seconds.

The mapping belongs to the PC, not to the project: it is kept in `midi.json` beside the LAN password
(`%LOCALAPPDATA%\Lumenet`). Export and Import take it to another PC with **MIDI** checked.

### Ready-made mappings

The faders of these work the faders of the pages shown, so the page keys choose what they move.

* **Akai APC mini mk2** (its Session mode): the 8 faders are the channel faders of the page shown, the ninth
  is the master, and the keys above them are their channel keys. The pads, from the top row down: Stack, Back,
  GO, Pause, Next, Clear, Solo up and Solo down; channel pages 1 to 8; memory pages 1 to 8; memories 1 to 32
  in four rows; effects 1 to 8. The keys at the right: Save mem, Edit mem, Rec seq, Copy, Del, Rate, Tempo
  and Black out. The pads light in the colors of their kinds, dim, half or full as the desk's keys.
* **Korg nanoKONTROL2** (its CC mode, as it comes): the sliders are the channel faders of the page shown, the
  knobs its memory faders; S, M and R are its channel, memory and effect keys. Play (GO), Stop (Pause), ◀◀ and ▶▶
  (Back and Next), ● (Stack) and Cycle (Black out); Track ◀ ▶ and Marker ◀ ▶ go through the channel and
  memory pages, and Set is Clear. Its lights follow the desk only once its LED mode is "External" (Korg
  Kontrol Editor).
* **Behringer X-Touch Mini** (its Standard mode, layer A): the knobs are the channel faders of the page shown
  and show where those stand; pressed, they are its channel keys. The upper keys are its memory keys, the
  lower ones Stack, GO, Pause, Next and the channel and memory pages back and on. The fader is the master.
* **Behringer X-Touch Compact** (its Standard mode, layer A): the motorized faders follow the channel faders
  of the page shown, the main fader the master; the upper knobs are its memory faders, the knobs at the right
  its effect faders, both showing where those stand. The keys under the faders are its channel keys, the one
  under the main fader Black out; the rows above them the channel pages, the memory pages, and Save mem, Edit
  mem, Rec seq, Copy, Del, Clear, Solo up and Solo down. The transport: Back, Next, Tempo (Loop), Stack (●),
  Pause (■) and GO (▶).

These follow the makers' manuals; the lights of the two X-Touch follow what other programs send them, as
their manuals do not say. They were tried with the simulators below, not yet with the real controllers.

### A console on DMX IN

A lighting console's DMX output goes into DMX IN of a dmxbox 4, the male XLR beside DMX OUT: the console then
plays the desk as a controller does. While the box is plugged in, the MIDI view lists its DMX IN as a device,
"DMX IN (box 3)" for box 3: tick it, and Learn takes the console's channel you move. A channel moves a fader
with its level (0 to 255; **Takes over at the desk's value** as for any fader), and presses a key from half
(128) up, letting it go below. Nothing goes back to a console, so such a control has no Light or Motor. When
the console is plugged in, its first frame only shows where its faders stand: a button held then presses
nothing. The box goes on sending its own DMX OUT meanwhile.

### Without a controller

`sim.cmd` in the source code starts a simulated controller, as if it were plugged into the PC: `sim.cmd
apc-mini-mk2`, `nanokontrol2`, `x-touch-mini` or `x-touch-compact`. The MIDI view lists it as "APC mini mk2
(simulator)", and the browser opens its page: pads and keys held with the mouse, faders and knobs moved, lit
and moved by the desk as the real one would be. U pulls it out and plugs it in again, Q ends it (see
[DEV.md](DEV.md#simulated-devices-simcmd)). `sim.cmd dmxbox --console` gives a simulated box a console on its DMX IN: 24
faders and 8 buttons on channels 1 to 32, on a page of their own.

![A simulated APC mini mk2: the pads lit as the desk's keys, memory 1 up, channel page 2 shown, the first fader at the top](docs/screenshots/midi-simulator.png)

### OSC from other programs

Other programs can work the desk with OSC (Open Sound Control) over the network: a sound program such as
QLab that fires the light cues with its own, TouchOSC on a tablet, a show controller. **OSC** on the MIDI
view switches it on (it is off at first) and sets the UDP port the desk listens on, 8000 at first; the
other program sends to this PC at that port. It belongs to the PC, as MIDI does.

* **Addresses:** every key and fader of the list at the right, its name with slashes after `/lumenet`:
  `/lumenet/stack/play` is GO, `/lumenet/stack/previous` Back, `/lumenet/memory/5` the key of memory 5,
  `/lumenet/fader/master` the master, `/lumenet/fader/channel/12` the fader of channel 12,
  `/lumenet/fader/slot/memory/3` the third memory fader of the page shown. `/lumenet` may be left out.
  While OSC is on, the list shows the address of each key and fader under its name, and the search finds
  them by it.
* **Keys:** a message without a value presses the key and lets it go, as a network cue of QLab sends it;
  1 presses it and 0 lets it go, as the buttons of TouchOSC send them (true and false the same). As for a
  controller, the page of the desk on this PC used last presses them: one has to be open there.
* **Faders:** a number with a point from 0 to 1, as the faders of TouchOSC send it, or a whole number from 0
  to 255, as the desk's faders show it. The fader goes there at once.
* **From another computer** OSC counts only while LAN is on (see
  [A phone or a tablet (LAN)](#a-phone-or-a-tablet-lan)); a message that does not count is listed as
  refused. Programs on this PC send to 127.0.0.1 and always count. The view says the address other computers
  send to. Windows may ask once whether Lumenet may use the network: allow it for private networks, or the
  messages of other computers do not arrive.
* **Incoming messages** lists every message, from "OSC from 192.168.1.30", with what it did; a key the desk
  does not have (a slip in the address) is said so.
* A port that another program has is said in red: choose another one, in both programs.
* Windows may hold the very first message of a program back for a moment and lose those it sends right
  behind it; after that every message arrives. A test from a new program is best begun with one message.

The desk sends nothing back over OSC yet: a layout of TouchOSC does not see the desk's levels.

Not yet: MIDI clock, MIDI timecode and MIDI Show Control (with beats and the sound later), and MIDI sent to
other programs.
