// The notes of a release on GitHub: the messages of the commits since the release before (the
// newest tag "v..." that the commit before this one reaches), each with its title and its text,
// the lines of a paragraph joined again. Used by .github/workflows/release.yml:
//
//   node .github/release-notes.mjs v0.1.26 > notes.md
//
// The first release lists only its own commit, not the whole history.

import { execFileSync } from 'node:child_process';

const tag = process.argv[2] ?? '';
const git = (...args) => execFileSync('git', args, { encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] });
const tryGit = (...args) => {
  try {
    return git(...args).trim();
  } catch {
    return '';
  }
};

const previous = tryGit('describe', '--tags', '--abbrev=0', '--match', 'v*', 'HEAD^');
const range = previous ? [`${previous}..HEAD`] : ['-1', 'HEAD'];

// Title and text of each commit, the newest first: fields end with \x1f, commits with \x1e.
const commits = git('log', '--format=%s%x1f%b%x1e', ...range)
  .split('\x1e')
  .map((entry) => entry.trim())
  .filter((entry) => entry !== '')
  .map((entry) => {
    const [title, text = ''] = entry.split('\x1f');
    return { title: title.trim(), paragraphs: paragraphsOf(text) };
  });

const lines = [];
for (const commit of commits) {
  lines.push(`## ${commit.title}`, '');
  for (const paragraph of commit.paragraphs) lines.push(paragraph, '');
}

lines.push(
  '---',
  '',
  '**Lumenet.exe** below is the whole desk in one file, for Windows 10 and 11 (64-bit), with nothing to install. ' +
    'Start it: the page opens in the browser, and Lumenet sits in the notification area of the taskbar, where its menu ' +
    'opens the page again, shows the log, and exits. Windows may say that it does not know the program (it is not ' +
    'signed): More info, then Run anyway.',
);

const server = process.env.GITHUB_SERVER_URL ?? 'https://github.com';
const repository = process.env.GITHUB_REPOSITORY;
if (previous && repository && tag) {
  lines.push('', `All changes since ${previous}: ${server}/${repository}/compare/${previous}...${tag}`);
}

process.stdout.write(`${lines.join('\n')}\n`);

// The text of a commit as paragraphs: the lines of each joined again (they are wrapped in the
// commit), a list item on a line of its own, and the trailers such as Co-Authored-By left out.
function paragraphsOf(text) {
  const paragraphs = [];
  let current = '';
  for (const raw of text.split(/\r?\n/)) {
    const line = raw.trim();
    if (/^(Co-Authored-By|Signed-off-by):/i.test(line)) continue;
    if (line === '') {
      if (current) paragraphs.push(current);
      current = '';
    } else if (/^([-*]|\d+\.)\s/.test(line) && current) {
      paragraphs.push(current);
      current = line;
    } else {
      current = current ? `${current} ${line}` : line;
    }
  }
  if (current) paragraphs.push(current);
  return paragraphs;
}
