// Starts the backend (.NET) and the frontend (Angular dev server) together and merges their
// output into the console this script was started from. Used by run.cmd.
//
//   node run.mjs                          the dmxbox on its USB cable (192.168.7.1)
//   node run.mjs --Box:Host=127.0.0.1     another address, for example the simulator in firmware/sim/
//
// Everything after the script name is handed to the backend as a setting.
// Ctrl+C stops both. If one of them exits on its own, the other one is stopped too.
//
// Before that it builds the page once more if its sources changed since the last build. The
// backend hands that built page to phones and tablets (the LAN key in the page lets them in);
// this PC uses the development server, which shows every change at once.

import { spawn, spawnSync } from 'node:child_process';
import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));
const frontendDir = join(root, 'frontend');
const angularCli = join(frontendDir, 'node_modules', '@angular', 'cli', 'bin', 'ng.js');
const builtPage = join(frontendDir, 'dist', 'lumenet', 'browser');
const backendProject = join(root, 'backend', 'DmxBox.Server');
// The built page's folder comes first, so that a "--Ui:Folder=..." of the caller wins.
const backendSettings = [`--Ui:Folder=${builtPage}`, ...process.argv.slice(2)];

const useColor = process.stdout.isTTY && !process.env.NO_COLOR;
const paint = (code, text) => (useColor ? `\x1b[${code}m${text}\x1b[0m` : text);
const tags = {
  run: paint('33', '[run]     '),
  backend: paint('36', '[backend] '),
  frontend: paint('35', '[frontend]'),
};
const log = (tag, line) => process.stdout.write(`${tags[tag]} ${line}\n`);

// ---- first run: install the frontend's packages ----

if (!existsSync(join(frontendDir, 'node_modules'))) {
  log('run', 'Installing frontend packages (first run only)...');
  const install = spawnSync('npm', ['install'], { cwd: frontendDir, stdio: 'inherit', shell: true });
  if (install.status !== 0) process.exit(install.status ?? 1);
}

// ---- the built page, for phones and tablets: only after a change of its sources ----

buildPageIfChanged();

function buildPageIfChanged() {
  const index = join(builtPage, 'index.html');
  const builtAt = existsSync(index) ? statSync(index).mtimeMs : 0;
  const sources = ['src', 'angular.json', 'package.json', 'package-lock.json', 'tsconfig.json', 'tsconfig.app.json']
    .map((name) => join(frontendDir, name))
    .filter((path) => existsSync(path));
  if (Math.max(...sources.map(newestChange)) <= builtAt) return;

  log('run', 'Building the page for phones and tablets (only after a change, about 10 seconds)...');
  const build = spawnSync(process.execPath, [angularCli, 'build'], {
    cwd: frontendDir,
    encoding: 'utf8',
    env: { ...process.env, NG_CLI_ANALYTICS: 'false' },
  });
  for (const line of `${build.stdout ?? ''}${build.stderr ?? ''}`.split(/\r?\n/)) {
    if (line.trim() !== '') log('frontend', line);
  }
  if (build.status !== 0) {
    // Not a reason to stop: this PC uses the development server, which says what is wrong.
    log('run', 'The page for phones could not be built. This PC works as before; phones get no page.');
  }
}

// The time of the newest change of a file, or of anything in a folder.
function newestChange(path) {
  const info = statSync(path);
  if (!info.isDirectory()) return info.mtimeMs;
  return readdirSync(path).reduce((newest, name) => Math.max(newest, newestChange(join(path, name))), info.mtimeMs);
}

// ---- the version: the one the releases on GitHub get for the same commit ----

// The first two numbers from backend\Version.props, the number of commits as the third, as
// publish.mjs gives them; nothing (the props file's own 0.1.0) without git or the file.
function versionToShow() {
  try {
    const props = readFileSync(join(root, 'backend', 'Version.props'), 'utf8');
    const match = /<Version>(\d+)\.(\d+)\.\d+<\/Version>/.exec(props);
    const count = spawnSync('git', ['rev-list', '--count', 'HEAD'], { cwd: root, encoding: 'utf8' });
    if (!match || count.status !== 0) return null;
    return `${match[1]}.${match[2]}.${count.stdout.trim()}`;
  } catch {
    return null;
  }
}

const version = versionToShow();

// ---- the two processes ----

const children = [];
let stopping = false;
let exitCode = 0;
const ready = { backend: false, frontend: false };

function run(name, command, args, options) {
  // No "windowsHide" here: the children must stay attached to this console, otherwise Ctrl+C
  // would never reach them and they could not shut down cleanly.
  const child = spawn(command, args, { ...options, stdio: ['ignore', 'pipe', 'pipe'] });
  children.push({ name, child, exited: false });

  for (const stream of [child.stdout, child.stderr]) {
    createInterface({ input: stream }).on('line', (line) => {
      log(name, line);
      announceWhenReady(name, line);
    });
  }

  child.on('error', (error) => {
    log('run', `Could not start the ${name}: ${error.message}`);
    onExit(name, 1);
  });
  child.on('exit', (code) => onExit(name, code ?? 0));
}

function announceWhenReady(name, line) {
  if (ready[name]) return;
  if (name === 'backend' && line.includes('Now listening on')) ready.backend = true;
  if (name === 'frontend' && line.includes('Local:')) ready.frontend = true;
  if (ready.backend && ready.frontend) {
    log('run', paint('1', 'Ready: open http://localhost:4200  (Ctrl+C stops both)'));
    log('run', 'For a phone or tablet in the same network: press LAN in the page.');
  }
}

function onExit(name, code) {
  const entry = children.find((c) => c.name === name);
  if (!entry || entry.exited) return;
  entry.exited = true;

  if (!stopping) {
    // One side went down by itself (build error, crash): there is no point in keeping the other.
    exitCode = code || 1;
    log('run', `The ${name} exited with code ${code}; stopping the other process.`);
    stopping = true;
    killRemaining();
  }

  if (children.every((c) => c.exited)) process.exit(exitCode);
}

function killRemaining() {
  for (const { child, exited } of children) {
    if (exited || child.pid === undefined) continue;
    if (process.platform === 'win32') {
      // /T takes the whole tree: "dotnet run" and the application it started.
      spawnSync('taskkill', ['/pid', String(child.pid), '/T', '/F'], { stdio: 'ignore' });
    } else {
      child.kill('SIGTERM');
    }
  }
}

function shutdown() {
  if (stopping) return;
  stopping = true;
  log('run', 'Stopping...');
  // Ctrl+C reaches every process in the console, so both are already shutting down by themselves.
  // Anything still alive after a few seconds is killed.
  setTimeout(killRemaining, 5000).unref();
}

process.on('SIGINT', shutdown);
process.on('SIGBREAK', shutdown);
process.on('SIGTERM', () => {
  shutdown();
  killRemaining();
});

log('run', 'Backend http://localhost:5080, frontend http://localhost:4200');

const versionProperty = version ? [`--property:Version=${version}`] : [];
run('backend', 'dotnet', ['run', '-c', 'Release', ...versionProperty, '--project', backendProject, '--', ...backendSettings], {
  cwd: root,
  env: {
    ...process.env,
    DOTNET_NOLOGO: '1',
    ...(useColor ? { DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION: '1' } : {}),
  },
});

run('frontend', process.execPath, [angularCli, 'serve'], {
  cwd: frontendDir,
  env: {
    ...process.env,
    NG_CLI_ANALYTICS: 'false',
    ...(useColor ? { FORCE_COLOR: '1' } : {}),
  },
});
