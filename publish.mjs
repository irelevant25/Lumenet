// Makes Lumenet.exe: one file with the desk program, the page and the examples inside it, for
// Windows 10 and 11 on 64 bits, with nothing to install (.NET comes along). It sits in the
// notification area of the taskbar: its icon opens the page, its menu shows the log and exits.
// Used by publish.cmd, and by the release workflow on GitHub (.github/workflows/release.yml).
//
//   node publish.mjs                      publish\Lumenet.exe, version 0.1.<number of commits>
//   node publish.mjs --version 0.1.7      another version
//   node publish.mjs --print-version      only says which version it would give
//
// It builds the page first (frontend\dist\lumenet\browser), then backend\Lumenet.

import { spawnSync } from 'node:child_process';
import { existsSync, readdirSync, readFileSync, rmSync, statSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));
const frontendDir = join(root, 'frontend');
const angularCli = join(frontendDir, 'node_modules', '@angular', 'cli', 'bin', 'ng.js');
const project = join(root, 'backend', 'Lumenet', 'Lumenet.csproj');
const output = join(root, 'publish');
const exe = join(output, 'Lumenet.exe');

const args = process.argv.slice(2);
const version = versionToGive();
if (args.includes('--print-version')) {
  console.log(version);
  process.exit(0);
}

const say = (line) => console.log(`[publish] ${line}`);
say(`Lumenet ${version}`);

// ---- the page ----

if (!existsSync(join(frontendDir, 'node_modules'))) {
  say('Installing the packages of the page (first time only)...');
  run('npm', ['install'], { cwd: frontendDir, shell: true });
}

say('Building the page...');
run(process.execPath, [angularCli, 'build'], {
  cwd: frontendDir,
  env: { ...process.env, NG_CLI_ANALYTICS: 'false' },
});

// ---- the exe ----

say('Making Lumenet.exe (the first time on a PC, this also fetches the .NET that goes into it)...');
rmSync(output, { recursive: true, force: true });
run(
  'dotnet',
  [
    'publish', project,
    '--configuration', 'Release',
    '--runtime', 'win-x64',
    '--self-contained', 'true',
    '--output', output,
    '-p:PublishSingleFile=true',
    '-p:EnableCompressionInSingleFile=true',
    '-p:DebugType=none',
    `-p:Version=${version}`,
    '-nologo',
  ],
  { cwd: root, env: { ...process.env, DOTNET_NOLOGO: '1', DOTNET_CLI_TELEMETRY_OPTOUT: '1' } },
);

// One file, or it is no single exe.
const others = readdirSync(output).filter((name) => name !== 'Lumenet.exe');
if (!existsSync(exe) || others.length > 0) {
  say(`Expected Lumenet.exe alone in ${output}, found: ${readdirSync(output).join(', ') || 'nothing'}`);
  process.exit(1);
}

const megabytes = (statSync(exe).size / 1024 / 1024).toFixed(1);
say(`Done: ${exe} (${megabytes} MB)`);

// ---- helpers ----

function run(command, commandArgs, options) {
  const result = spawnSync(command, commandArgs, { stdio: 'inherit', ...options });
  if (result.error) {
    say(`Could not start ${command}: ${result.error.message}`);
    process.exit(1);
  }
  if (result.status !== 0) process.exit(result.status ?? 1);
}

// The first two numbers from backend\Version.props, the number of commits as the third, so that
// a build of the same commit always gets the same number, here and on GitHub.
function versionToGive() {
  const given = valueOf('--version');
  if (given !== undefined) {
    if (!/^\d+\.\d+\.\d+$/.test(given)) {
      console.error(`A version is three numbers, like 0.1.7, not "${given}".`);
      process.exit(1);
    }
    return given;
  }

  const props = readFileSync(join(root, 'backend', 'Version.props'), 'utf8');
  const match = /<Version>(\d+)\.(\d+)\.\d+<\/Version>/.exec(props);
  if (!match) {
    console.error('backend\\Version.props has no <Version> of three numbers.');
    process.exit(1);
  }

  const count = spawnSync('git', ['rev-list', '--count', 'HEAD'], { cwd: root, encoding: 'utf8' });
  const commits = count.status === 0 ? count.stdout.trim() : '0';
  return `${match[1]}.${match[2]}.${commits}`;
}

function valueOf(name) {
  const at = args.indexOf(name);
  return at >= 0 ? args[at + 1] : undefined;
}
