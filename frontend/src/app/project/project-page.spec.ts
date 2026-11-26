import { ComponentFixture, TestBed } from '@angular/core/testing';
import { App } from '../app';
import { DeskSettings } from '../desk/desk-settings';
import { DeskStore } from '../desk/desk-store';
import { deskPageHelpers, FakeConnection, LIVE, startDeskPage } from '../testing/desk-page.testing';

describe('the desk page: the project', () => {
  let connection: FakeConnection;
  let fixture: ComponentFixture<App>;
  let page: HTMLElement;
  let store: DeskStore;
  const { show, button, buttonIn, press, stackStands, pick, check, until, textOf, aSound } =
    deskPageHelpers(() => ({ connection, fixture, page, store }));

  beforeEach(async () => {
    ({ connection, fixture, page, store } = await startDeskPage());
  });

  /** Whether a part in the window of Import or Export is checked. */
  function checked(dialog: 'lm-import-dialog' | 'lm-export-dialog', label: string): boolean {
    return [...page.querySelectorAll<HTMLLabelElement>(`${dialog} .parts label`)]
      .find((item) => item.textContent?.trim().startsWith(label))!
      .querySelector('input')!.checked;
  }

  it('a new project is asked for from the backend and unfinished functions are dropped', async () => {
    await press(button('Copy memory'));
    await store.newProject();
    await fixture.whenStable();
    expect(connection.methods()).toContain('NewProject');
    expect(button('Copy memory').getAttribute('aria-pressed')).toBe('false');
    expect(button('New project')).toBeTruthy();
  });

  it('the outputs, LAN, Undo and Redo stand at the top left; New, Save, Load, Import and Export at the right', () => {
    const labels = (selector: string) =>
      [...page.querySelectorAll(`lm-top-bar ${selector} button`)].map(
        (key) => key.getAttribute('aria-label') ?? key.textContent?.trim(),
      );
    expect(labels('.left')).toEqual(['LAN', 'Undo', 'Redo']);
    expect(textOf('lm-top-bar .left lm-outputs-status')).toBe('Outputs connected: 0'); // a line of text: Settings has the outputs
    expect(labels('.project')).toEqual([
      'New project',
      'Save project',
      'Load project',
      'Import',
      'Export',
    ]);
  });

  // ---------------------------------------------------------------- universes and boxes

  it('undo and redo say what they would do, and do it', async () => {
    expect(button('Undo').disabled).toBe(true); // nothing to undo yet
    expect(button('Redo').disabled).toBe(true);

    connection.emit('HistoryChanged', { undo: 'storing memory 4', redo: null });
    await fixture.whenStable();
    expect(button('Undo').disabled).toBe(false);
    expect(button('Undo').title).toBe('Undo storing memory 4');

    await press(button('Undo'));
    await until(() => store.notice() !== '');
    expect(connection.methods()).toEqual(['Undo']);
    expect(store.notice()).toBe('Undone: storing memory 4.');

    // Ctrl + Z is the usual shortcut; Ctrl + Y for redo.
    connection.emit('HistoryChanged', { undo: null, redo: 'storing memory 4' });
    await fixture.whenStable();
    document.dispatchEvent(
      new KeyboardEvent('keydown', {
        code: 'KeyY',
        key: 'y',
        ctrlKey: true,
        bubbles: true,
        cancelable: true,
      }),
    );
    await fixture.whenStable();
    expect(connection.methods()).toEqual(['Undo', 'Redo']);

    // A live stack locks what is stored, and undo with it.
    connection.emit('HistoryChanged', { undo: 'storing memory 4', redo: null });
    await stackStands(LIVE);
    expect(button('Undo').disabled).toBe(true);
  });

  it('says once that the work which was not saved when the desk program stopped is back', async () => {
    connection.recovered = '2026-11-20T18:42:05+01:00';
    connection.start(); // connects again
    await until(() => store.notice() !== '');
    expect(store.notice()).toContain('(18:42) is back');
  });

  /** The parts the window of Import lists for the file, and whether each can be checked. */
  function offered(): [string, boolean][] {
    return [...page.querySelectorAll<HTMLLabelElement>('lm-import-dialog .parts label')].map(
      (item) => [
        [...item.childNodes]
          .filter((node) => node.nodeType === Node.TEXT_NODE)
          .map((node) => node.textContent)
          .join('')
          .trim(),
        !item.querySelector('input')!.disabled,
      ],
    );
  }

  it('import: a show file (.ASC) becomes the project, and the window says what became of it', async () => {
    connection.emit('ProjectChanged', { name: 'Untitled', path: null, unsaved: true });
    await press(button('Import'));
    const dialog = page.querySelector<HTMLDialogElement>('lm-import-dialog dialog');
    expect(dialog?.open).toBe(true);
    // the file first: no parts before it
    expect(page.querySelector('lm-import-dialog .parts')).toBeNull();
    expect(textOf('lm-import-dialog .file-name')).toBe('No file chosen');
    expect(button('Import the file').disabled).toBe(true);
    expect(
      page.querySelector<HTMLInputElement>('lm-import-dialog input[type="file"]')?.accept,
    ).toBe('.zip,.json,.asc,.txt');

    // .ASC is a file of its own: taken whole
    await pick(new File(['Cue 1\nChan 1 FL\n'], 'Hamlet.asc'));
    expect(textOf('lm-import-dialog .file-name')).toBe('Hamlet.asc');
    expect(page.querySelector('lm-import-dialog .parts')).toBeNull();
    expect(page.querySelector('lm-import-dialog')?.textContent).toContain(
      'The show of a theatre console (USITT ASCII)',
    );
    expect(page.querySelector('lm-import-dialog')?.textContent).toContain(
      'The open project has changes that are not saved.',
    );
    await press(button('Import the file'));
    await until(() => page.querySelector('lm-import-dialog .report') !== null);

    expect(connection.last('ImportProject')).toEqual(['Hamlet.asc', 'Cue 1\nChan 1 FL\n']);
    const report = [...page.querySelectorAll('lm-import-dialog .report p')].map((line) =>
      line.textContent?.trim(),
    );
    expect(report).toEqual(connection.importAnswer.report);
    expect(textOf('lm-top-bar .name strong')).toBe('Hamlet');

    // OK closes the window; Import again starts with no file.
    await press(buttonIn('lm-import-dialog', 'OK'));
    expect(dialog?.open).toBe(false);
    await press(button('Import'));
    expect(textOf('lm-import-dialog .file-name')).toBe('No file chosen');
    expect(page.querySelector('lm-import-dialog')?.textContent).not.toContain('USITT');
    await press(page.querySelector<HTMLButtonElement>('lm-import-dialog .actions button')); // Cancel
    expect(dialog?.open).toBe(false);

    // saving offers the name of the show file
    await press(button('Save project'));
    expect(page.querySelector<HTMLInputElement>('#project-name')?.value).toBe('Hamlet');
  });

  it('import says why the desk refused a file, and another one can be tried', async () => {
    connection.importRefusal = '"notes.txt" has no cues, submasters or groups.';
    await press(button('Import'));
    await pick(new File(['Dear diary,'], 'notes.txt'));
    await press(button('Import the file'));
    await until(() => page.querySelector('lm-import-dialog .error') !== null);

    expect(textOf('lm-import-dialog .error')).toBe(
      '"notes.txt" has no cues, submasters or groups.',
    );
    expect(page.querySelector<HTMLDialogElement>('lm-import-dialog dialog')?.open).toBe(true);
    expect(button('Import the file').disabled).toBe(false);

    // a file that is no file of Lumenet says so as it is chosen
    await pick(new File(['{ not json'], 'photo.json'));
    await until(() => textOf('lm-import-dialog .error')?.includes('photo.json') === true);
    expect(textOf('lm-import-dialog .error')).toContain('"photo.json" is no file of Lumenet');
    expect(button('Import the file').disabled).toBe(true);
  });

  it('import takes no file larger than the desk takes, and sends nothing', async () => {
    await press(button('Import'));
    const file = new File(['Cue 1'], 'huge.asc');
    Object.defineProperty(file, 'size', { value: 9 * 1024 * 1024 });
    await pick(file);

    expect(textOf('lm-import-dialog .error')).toBe(
      '"huge.asc" has 9.0 MB. A file can have up to 8.0 MB.',
    );
    expect(button('Import the file').disabled).toBe(true);
    expect(connection.methods()).not.toContain('ImportProject');
  });

  it('import lists the parts of the file, and takes those that are checked', async () => {
    const file = JSON.stringify({
      format: 'lumenet-export',
      version: 1,
      name: 'Concert',
      project: JSON.parse(connection.projectFile),
      settings: { channelRows: 1, colors: { play: '#123456' } },
      keyboard: { shortcuts: { 'stack.play': 'Space' } },
    });
    await press(button('Import'));
    await pick(new File([file], 'Concert - project, settings, keyboard.json'));
    await until(() => page.querySelector('lm-import-dialog .parts') !== null);
    // the parts of the file, none checked; one it does not have cannot be
    expect(offered()).toEqual([
      ['Project', true],
      ['Stage', true],
      ['Settings', true],
      ['Keyboard', true],
      ['MIDI', false],
    ]);
    expect(textOf('lm-import-dialog .parts label:last-child small')).toBe('not in this file');
    expect(button('Import the file').disabled).toBe(true);

    await check('lm-import-dialog', 'Project');
    await check('lm-import-dialog', 'Settings');
    await check('lm-import-dialog', 'Keyboard');
    await press(button('Import the file'));
    await until(() => page.querySelector('lm-import-dialog .report') !== null);

    expect(connection.last('ImportProjectFile')).toEqual([
      'Concert',
      JSON.stringify(JSON.parse(connection.projectFile)),
    ]);
    expect(textOf('lm-top-bar .name strong')).toBe('Concert');
    const settings = TestBed.inject(DeskSettings);
    expect(settings.channelRows()).toBe(1);
    expect(settings.colors()).toEqual({ play: '#123456' });
    expect(settings.shortcuts()['stack.play']).toBe('Space');
    expect(
      [...page.querySelectorAll('lm-import-dialog .report p')].map((line) =>
        line.textContent?.trim(),
      ),
    ).toEqual([
      'The project "Concert" is open. Save keeps it in the projects folder.',
      'The settings of this device are those of the file.',
      'The keyboard shortcuts of this device are those of the file.',
    ]);

    // A project file: its project and its stage, the stage on its own in place of the project's.
    await press(buttonIn('lm-import-dialog', 'OK'));
    await press(button('Import'));
    await pick(new File([connection.projectFile], 'Evening.json'));
    await until(() => page.querySelector('lm-import-dialog .parts') !== null);
    expect(offered()).toEqual([
      ['Project', true],
      ['Stage', true],
      ['Settings', false],
      ['Keyboard', false],
      ['MIDI', false],
    ]);
    await check('lm-import-dialog', 'Stage');
    await press(button('Import the file'));
    await until(() => page.querySelector('lm-import-dialog .report') !== null);
    expect(JSON.parse(connection.last('ImportStage')![0] as string).lights.length).toBe(1);
    expect(textOf('lm-import-dialog .report')).toContain(
      'The stage is that of the file, with 1 light.',
    );
  });

  it('export puts the checked parts into one ZIP on this device', async () => {
    const chunks: Uint8Array[] = [];
    let suggested = '';
    (globalThis as { showSaveFilePicker?: unknown }).showSaveFilePicker = async (options: {
      suggestedName: string;
    }) => {
      suggested = options.suggestedName;
      return {
        name: options.suggestedName,
        createWritable: async () =>
          new WritableStream<Uint8Array>({ write: (chunk) => void chunks.push(chunk) }),
      };
    };
    const asked: { url: string; body: unknown }[] = [];
    const realFetch = globalThis.fetch;
    globalThis.fetch = (async (url: string, init?: RequestInit) => {
      asked.push({ url, body: init?.body });
      return new Response(new Uint8Array([80, 75, 3, 4]), { status: 200 });
    }) as typeof fetch;
    try {
      TestBed.inject(DeskSettings).setShortcuts({ 'stack.play': 'Space' });
      await press(button('Export'));
      const dialog = page.querySelector<HTMLDialogElement>('lm-export-dialog dialog');
      expect(dialog?.open).toBe(true);
      expect(
        ['Project', 'Stage', 'Settings', 'Keyboard'].map((part) =>
          checked('lm-export-dialog', part),
        ),
      ).toEqual([false, false, false, false]);
      expect(button('Export the file').disabled).toBe(true);

      await check('lm-export-dialog', 'Stage');
      await check('lm-export-dialog', 'Keyboard');
      await press(button('Export the file'));
      await until(
        () => chunks.length > 0 && page.querySelector('lm-export-dialog .report') !== null,
      );

      // the desk program packs the parts it is given, each a file of its own
      expect(suggested).toBe('Untitled - stage, keyboard.zip');
      expect(asked[0].url).toBe('/api/export/pack?name=Untitled%20-%20stage%2C%20keyboard');
      const text = JSON.parse(asked[0].body as string);
      expect(Object.keys(text)).toEqual(['format', 'version', 'name', 'stage', 'keyboard']);
      expect(text.stage.lights[0].name).toBe('Wash');
      expect(text.keyboard.shortcuts['stack.play']).toBe('Space');
      expect([...chunks[0]]).toEqual([80, 75, 3, 4]);
      expect(textOf('lm-export-dialog .report')).toContain(
        'Saved as "Untitled - stage, keyboard.zip"',
      );
      await press(buttonIn('lm-export-dialog', 'OK'));
      expect(dialog?.open).toBe(false);
    } finally {
      globalThis.fetch = realFetch;
      delete (globalThis as { showSaveFilePicker?: unknown }).showSaveFilePicker;
    }
  });

  it('Export takes the sounds of the project along, and Import takes such a ZIP', async () => {
    connection.emit('SoundsChanged', [aSound(1)]);
    const chunks: Uint8Array[] = [];
    (globalThis as { showSaveFilePicker?: unknown }).showSaveFilePicker = async (options: {
      suggestedName: string;
    }) => ({
      name: options.suggestedName,
      createWritable: async () =>
        new WritableStream<Uint8Array>({ write: (chunk) => void chunks.push(chunk) }),
    });
    const asked: { url: string; body: unknown }[] = [];
    const realFetch = globalThis.fetch;
    globalThis.fetch = (async (url: string, init?: RequestInit) => {
      asked.push({ url, body: init?.body });
      if (url.startsWith('/api/export/pack'))
        return new Response(new Uint8Array([80, 75, 3, 4]), { status: 200 });
      // the desk program took the sounds of the ZIP, all but one, and gives back its parts
      const exported = {
        format: 'lumenet-export',
        version: 1,
        name: 'Concert',
        project: JSON.parse(connection.projectFile),
      };
      return new Response(
        JSON.stringify({ text: JSON.stringify(exported), leftOut: ['notes.wav'] }),
        { status: 200 },
      );
    }) as typeof fetch;
    try {
      await press(button('Export'));
      await check('lm-export-dialog', 'Project');
      await press(button('Export the file'));
      await until(
        () => chunks.length > 0 && page.querySelector('lm-export-dialog .report') !== null,
      );
      expect(asked[0].url).toBe('/api/export/pack?name=Untitled%20-%20project');
      expect(JSON.parse(asked[0].body as string).project.format).toBe('lumenet-project');
      await press(buttonIn('lm-export-dialog', 'OK'));

      // a ZIP goes to the desk program as it is chosen, whatever its size: it takes it in pieces
      await press(button('Import'));
      const zip = new File([new Uint8Array([80, 75, 3, 4])], 'Concert - project.zip');
      Object.defineProperty(zip, 'size', { value: 90 * 1024 * 1024 });
      await pick(zip);
      await until(() => page.querySelector('lm-import-dialog .parts') !== null);
      expect(asked[1]).toEqual({ url: '/api/import/unpack', body: zip });
      expect(
        offered()
          .filter(([, can]) => can)
          .map(([part]) => part),
      ).toEqual(['Project', 'Stage']);
      await check('lm-import-dialog', 'Project');
      await press(button('Import the file'));
      await until(() => page.querySelector('lm-import-dialog .report') !== null);
      expect(asked.length).toBe(2); // the ZIP was sent once
      expect(connection.last('ImportProjectFile')).toEqual([
        'Concert',
        JSON.stringify(JSON.parse(connection.projectFile)),
      ]);
      expect(
        [...page.querySelectorAll('lm-import-dialog .report p')].map((line) =>
          line.textContent?.trim(),
        ),
      ).toEqual([
        'These files of the ZIP are no sounds the desk program can play: notes.wav.',
        'The project "Concert" is open. Save keeps it in the projects folder.',
      ]);
    } finally {
      globalThis.fetch = realFetch;
      delete (globalThis as { showSaveFilePicker?: unknown }).showSaveFilePicker;
    }
  });

  it('Notes: the notes of the project are read, written while Edit is lit, and kept by the desk program', async () => {
    connection.notes =
      '<div><b>Cue 1</b> at the bell</div><script>alert(1)</script><img src="x" onerror="alert(1)">';
    connection.emit('DeskChanged', connection.welcome.desk);
    await show('/notes');
    await until(() => page.querySelector('lm-notes-view .text b') !== null);
    const text = page.querySelector<HTMLElement>('lm-notes-view .text')!;
    const edit = button('Edit the notes');

    // Read: only the marks of the notes are shown, and nothing can be changed.
    expect(text.innerHTML).toBe('<div><b>Cue 1</b> at the bell</div>');
    expect(text.getAttribute('contenteditable')).toBe('false');
    expect(edit.className).toContain('level-1');
    expect(edit.getAttribute('aria-pressed')).toBe('false');
    for (const tool of [
      'Bold',
      'Underline',
      'Bulleted list',
      'Numbered list',
      'Usual color',
      'Red',
    ])
      expect(button(tool).disabled).toBe(true);

    // Edit: full, the text can be written in, and the tool keys work on it with the browser's editor.
    const commands: unknown[][] = [];
    const editor = document as unknown as { execCommand: (...args: unknown[]) => boolean };
    editor.execCommand = (...args: unknown[]) => commands.push(args) > 0;
    try {
      await press(edit);
      expect(edit.className).toContain('level-3');
      expect(text.getAttribute('contenteditable')).toBe('true');
      expect(button('Bold').disabled).toBe(false);
      await press(button('Bold'));
      await press(button('Red'));
      expect(commands).toContainEqual(['bold', false, undefined]);
      expect(commands).toContainEqual(['foreColor', false, '#ff5247']);
    } finally {
      delete (editor as { execCommand?: unknown }).execCommand;
    }

    // What is written goes to the desk program, made into the marks of the notes; Edit goes faint again.
    text.innerHTML =
      '<div><b>Cue 1</b> at the bell</div><div>Cue 2 <u>after the song</u><span>!</span></div>';
    text.dispatchEvent(new Event('input'));
    await press(edit);
    expect(connection.last('SetNotes')).toEqual([
      '<div><b>Cue 1</b> at the bell</div><div>Cue 2 <u>after the song</u>!</div>',
    ]);
    expect(text.getAttribute('contenteditable')).toBe('false');
    expect(edit.className).toContain('level-1');

    // Notes from another page come in; while the stack is live, Edit is locked, and a writer is stopped.
    connection.emit('NotesChanged', '<ul><li>Doors at 7</li></ul>');
    await fixture.whenStable();
    expect(text.innerHTML).toBe('<ul><li>Doors at 7</li></ul>');
    await press(edit);
    await stackStands(LIVE);
    expect(text.getAttribute('contenteditable')).toBe('false');
    expect(edit.disabled).toBe(true);
  });

  it('Load offers the examples under the projects; one opens as a project without a file', async () => {
    await press(button('Load project'));
    await until(() => page.querySelector('lm-project-dialog #examples-title') !== null);
    const examples = [
      ...page.querySelectorAll('lm-project-dialog [aria-labelledby="examples-title"] button'),
    ];
    expect(examples.map((entry) => entry.textContent?.trim())).toEqual([
      '1 Looks and cues',
      '6 A whole concert',
    ]);

    await press(examples[1] as HTMLButtonElement);
    await press(page.querySelector<HTMLButtonElement>('lm-project-dialog button[type="submit"]'));
    await until(() => connection.methods().includes('LoadExample'));
    expect(connection.last('LoadExample')).toEqual(['6 A whole concert']);
    expect(connection.methods()).not.toContain('LoadProject');
    expect(textOf('lm-top-bar .name strong')).toBe('6 A whole concert');
  });

  it('LAN lit shows the address and the password under the box light; off shows neither', async () => {
    const line = () => page.querySelector('lm-top-bar .lan');
    expect(button('LAN').getAttribute('aria-pressed')).toBe('false');
    expect(button('LAN').className).toContain('level-1');
    expect(line()).toBeNull();

    await press(button('LAN'));
    expect(connection.last('SetLanAccess')).toEqual([true]);
    expect(button('LAN').className).toContain('level-3'); // lit at once

    // the desk answers with the password
    connection.emit('LanAccessChanged', { ...connection.lan, on: true, password: 'K7M2Q' });
    await fixture.whenStable();
    expect(line()?.textContent?.replace(/\s+/g, ' ').trim()).toBe(
      'LAN 192.168.1.20:5080 Password K7M2Q',
    );
    expect(line()?.getAttribute('title')).toContain('open http://192.168.1.20:5080');

    await press(button('LAN'));
    expect(connection.last('SetLanAccess')).toEqual([false]);
    expect(line()).toBeNull(); // the password goes at once
    expect(button('LAN').getAttribute('aria-pressed')).toBe('false');
  });

  it('a page on a phone has no LAN key and shows no password', async () => {
    connection.emit('LanAccessChanged', { local: false, on: true, addresses: [], password: null });
    await fixture.whenStable();

    expect(page.querySelector('button[aria-label="LAN"]')).toBeNull();
    expect(page.querySelector('lm-top-bar .lan')).toBeNull();
  });
});
