# Lumenet desk: the page

The Angular app that shows the fader desk. It holds no state of its own: the desk program in
`../backend/DmxBox.Server` does, and the page talks to it over SignalR (`/hub/desk`).

What the desk does and how to use it is described in the [manual](../MANUAL.md); how it is built, in
[DEV.md](../DEV.md#4-the-desk-how-it-is-built).

## Run

From the repository root, `run.cmd` starts the backend and this app together. Then open http://localhost:4200.

Only the page, when the backend is already running:

```bash
npm start
```

The dev server passes `/hub` and `/api` on to the backend at http://localhost:5080 (`proxy.conf.json`).

## Test and build

```bash
npm test -- --watch=false     # the tests of the page (Vitest)
npm run build                 # the finished files, in dist/lumenet
```

## Where things are

The app is cut by what it is about: a folder for each view and for each thing the desk does, with the
components of it (each in a folder of its own) and the plain TypeScript beside them.

| Folder in `src/app` | What it is |
|---|---|
| `desk/` | What every part uses: the state of the desk as the page sees it (`desk-store.ts`), the connection to the backend, the types of the messages, the actions behind the keys and their shortcuts, the settings of this device, the rules of the four memory functions (`function-mode.ts`) and of the lamps (`lamps.ts`), universes, tempo and notes. |
| `shared/` | Small parts used by many views: a fader, a key lit from behind (`lamp-button`), a fader with its level, key and note (`fader-strip`), the color picker, the Record section, and helpers for scrolling, dragging and windows. |
| `top-bar/` | The bar at the top: the links to the views, the project's name, New, Load, Save, Import, Export, Undo, Redo and LAN. |
| `main/` | The Main view and its sections: channels, memories, General, Master and the independents. |
| `memories/`, `sequences/`, `stack/` | Those views, with what they share: the stack's steps (`step-row`, `step-choices.ts`) are the sequences' too. |
| `stage/`, `fixtures/` | The stage plan and the lights on it; the fixtures, their library and their faders. |
| `effects/`, `sounds/`, `notes/`, `midi/` | Those views with their editors, and the rules behind them (`effects.ts`, `sounds.ts` ...). |
| `settings/`, `outputs/` | The Settings view and its panels; the outputs and their status in the top bar. |
| `project/` | Load, Save, Import and Export, and the show files of other desks. |

Every component has its own `.ts`, `.html` and `.css` file, and its tests beside it as `.spec.ts`;
`app.spec.ts` tests the whole page. Sizes and colors shared by all of them are in `src/styles.css`.
