# Lumenet desk: the page

The Angular app that shows the fader desk. It holds no state of its own: the desk program in
`../backend/DmxBox.Server` does, and the page talks to it over SignalR (`/hub/desk`).

What the desk does and how to use it is described in the [main README](../README.md#4-lumenet-the-fader-desk).

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

| Folder in `src/app` | What it is |
|---|---|
| `desk/` | The state of the desk as the page sees it (`desk-store.ts`), the connection to the backend, the rules of the four memory functions (`function-mode.ts`) and of the lamps (`lamps.ts`). |
| `fader/` | One fader: drag, mouse wheel, keyboard. |
| `lamp-button/` | A key that is lit from behind, in three levels, steady or blinking. |
| `fader-strip/` | A fader with its level number, its key and its note. |
| `channel-section/`, `memory-section/` | The rows of faders with their page keys, and the four memory functions. |
| `top-bar/`, `project-dialog/` | Name, Clear, Black out, Master, and saving and loading a project. |

Every component has its own `.ts`, `.html` and `.css` file. Sizes and colours shared by all of them are
in `src/styles.css`.
