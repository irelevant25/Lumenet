import { ComponentFixture } from '@angular/core/testing';
import { App } from '../app';
import { DeskStore } from '../desk/desk-store';
import { NOTE_LINE_SEPARATOR } from '../desk/notes';
import { deskPageHelpers, FakeConnection, startDeskPage } from '../testing/desk-page.testing';

describe('the desk page: the Memories view', () => {
  let connection: FakeConnection;
  let fixture: ComponentFixture<App>;
  let page: HTMLElement;
  let store: DeskStore;
  const { show, button, press, stackStands } = deskPageHelpers(() => ({
    connection,
    fixture,
    page,
    store,
  }));

  beforeEach(async () => {
    ({ connection, fixture, page, store } = await startDeskPage());
  });

  it('the memory keys of the memories view and the sequences view carry the notes of their memories', async () => {
    connection.emit('MemoryNoteChanged', 4, 'Chase' + NOTE_LINE_SEPARATOR + 'blue');
    await fixture.whenStable();

    for (const view of ['/memories', '/sequences']) {
      await show(view);
      const lines = (memory: number) =>
        [
          ...(page.querySelectorAll('lm-note-lines')[memory - 1]?.querySelectorAll('.line') ?? []),
        ].map((line) => line.textContent?.trim());
      expect(page.querySelectorAll('.keys lm-note-lines').length).toBe(32);
      expect(lines(2)).toEqual(['Wash', '']);
      expect(lines(4)).toEqual(['Chase', 'blue']);
      expect(lines(1)).toEqual(['', '']);
      expect(page.querySelectorAll('lm-note-lines')[3].getAttribute('title')).toBe(
        'Chase blue (double click to change it)',
      );
      expect(page.querySelectorAll('.keys input').length).toBe(0); // lettering, until a double click
    }
  });

  it('a double click on the note of a memory key in Memories and Sequences changes it there', async () => {
    for (const view of ['/memories', '/sequences']) {
      await show(view);
      const note = () => page.querySelectorAll<HTMLElement>('.keys lm-note-lines')[3];
      const doubleClick = async (target: Element) => {
        target.dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
        await fixture.whenStable();
      };

      // the second line was double-clicked: two fields, the second one to type in
      await doubleClick(note().querySelectorAll('.line')[1]);
      const fields = [...note().querySelectorAll<HTMLInputElement>('input')];
      expect(fields.map((field) => field.getAttribute('aria-label'))).toEqual([
        'Note for memory 4',
        'Note for memory 4, second line',
      ]);
      expect(document.activeElement).toBe(fields[1]);
      fields[0].value = 'Chase';
      fields[1].value = view === '/memories' ? 'red' : 'green';
      fields[1].dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
      await fixture.whenStable();
      expect(note().querySelectorAll('input').length).toBe(0);
      expect(store.memoryNotes()[3]).toBe('Chase' + NOTE_LINE_SEPARATOR + fields[1].value);
      expect(connection.calls).toContainEqual({
        method: 'SetMemoryNote',
        args: [4, 'Chase' + NOTE_LINE_SEPARATOR + fields[1].value],
      });

      // Escape leaves the note as it was
      await doubleClick(note());
      note().querySelector('input')!.value = 'Gone';
      note()
        .querySelector('input')!
        .dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
      await fixture.whenStable();
      expect(store.memoryNotes()[3]).toBe('Chase' + NOTE_LINE_SEPARATOR + fields[1].value);
    }

    // While the stack is live nothing that is stored changes: no fields
    await stackStands({ ...connection.playback.stack, live: true });
    await show('/memories');
    page
      .querySelectorAll('.keys lm-note-lines')[3]
      .dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
    await fixture.whenStable();
    expect(page.querySelectorAll('.keys input').length).toBe(0);
  });

  it('the memories view shows what a memory holds, and looking changes nothing', async () => {
    await show('/memories');
    const cells = () => [...page.querySelectorAll('lm-memories-view table.matrix td')];
    const levelOf = (channel: number) => cells()[channel - 1].textContent?.trim();
    expect(cells().length).toBe(512);
    expect(page.querySelectorAll('lm-memories-view table.matrix tr').length).toBe(16); // 16 rows of 32
    expect(cells()[69].getAttribute('data-channel')).toBe('70');
    // a cell is one element: the table is light enough for the browser's developer tools
    expect(page.querySelectorAll('lm-memories-view table.matrix td *').length).toBe(0);
    expect(page.querySelectorAll('lm-memories-view .content .keys button').length).toBe(32);
    expect(page.querySelectorAll('nav[aria-label="Memory pages"] button').length).toBe(4);

    // memory 1 is looked at first, and it is empty: a double click on a channel stores it
    expect(page.querySelector('.summary')?.textContent).toContain(
      'Memory 1 is empty. A double click on a channel gives it a level and stores the memory.',
    );
    expect(levelOf(5)).toBe('0');

    await press(button('Look at memory 2'));
    expect(button('Look at memory 2').getAttribute('aria-pressed')).toBe('true');
    expect(page.querySelector('.summary')?.textContent).toContain(
      'Memory 2 (Wash): 2 of 512 channels are above zero',
    );
    expect(levelOf(5)).toBe('200');
    expect(levelOf(70)).toBe('255');
    expect(levelOf(6)).toBe('0');
    expect(cells()[4].getAttribute('aria-label')).toBe('Channel 5 (U1): 200');

    expect(connection.methods()).toEqual([]); // looking sends nothing to the desk
    expect(store.channels()[4]).toBe(0);
  });

  it('the cells of the memories view carry the notes of their channels', async () => {
    connection.emit('ChannelNoteChanged', 5, 'Front' + NOTE_LINE_SEPARATOR + 'left');
    connection.emit('ChannelNoteChanged', 6, 'Wash');
    await show('/memories');
    const cell = (channel: number) =>
      page.querySelectorAll('lm-memories-view table.matrix td')[channel - 1];

    // every cell keeps room for a note, so that all have the same size
    expect(page.querySelector('lm-memories-view table.matrix')?.classList).toContain('with-notes');
    expect(cell(5).getAttribute('data-note')).toBe('Front\nleft'); // the style sheet shows it, one line under the other
    expect(cell(5).getAttribute('aria-label')).toMatch(/^Channel 5 \(U1\) \(Front left\): \d+$/);
    expect(cell(6).getAttribute('data-note')).toBe('Wash');
    expect(cell(7).hasAttribute('data-note')).toBe(false);
    expect(cell(7).getAttribute('aria-label')).toMatch(/^Channel 7 \(U1\): \d+$/);
  });

  it('a double click on a cell of the memories view changes the level the memory gives the channel, and its note', async () => {
    connection.emit('ChannelNoteChanged', 6, 'Wash');
    await show('/memories');
    await press(button('Look at memory 2'));
    const cell = (channel: number) =>
      page.querySelectorAll<HTMLElement>('lm-memories-view table.matrix td')[channel - 1];
    const editor = () => page.querySelector('lm-cell-editor');
    const fields = () => [...(editor()?.querySelectorAll<HTMLInputElement>('input') ?? [])];
    const doubleClick = async (channel: number) => {
      cell(channel).dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
      await fixture.whenStable();
    };
    const key = async (field: HTMLInputElement, name: string) => {
      field.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true }));
      await fixture.whenStable();
    };

    // fields over the cell: the level, and the two lines of the channel's note
    await doubleClick(5);
    expect(editor()?.getAttribute('aria-label')).toBe('Channel 5 (U1) in memory 2');
    expect(fields().map((field) => field.value)).toEqual(['200', '', '']);
    expect(document.activeElement).toBe(fields()[0]);
    fields()[0].value = '90';
    fields()[1].value = 'Front';
    await key(fields()[0], 'Enter');
    expect(editor()).toBeNull();
    expect(connection.last('SetMemoryChannel')).toEqual([2, 5, 90]);
    expect(connection.last('SetChannelNote')).toEqual([5, 'Front']);
    expect(cell(5).textContent?.trim()).toBe('90'); // shown at once
    expect(store.memoryLooks()[2][4]).toBe(90);

    // Escape changes nothing
    await doubleClick(6);
    expect(fields().map((field) => field.value)).toEqual(['0', 'Wash', '']);
    fields()[0].value = '17';
    await key(fields()[0], 'Escape');
    expect(editor()).toBeNull();
    expect(connection.calls.filter((call) => call.method === 'SetMemoryChannel').length).toBe(1);

    // A memory that holds a sequence: only the note can change here.
    await press(button('Look at memory 3'));
    await doubleClick(8);
    expect(fields()[0].disabled).toBe(true);
    expect(document.activeElement).toBe(fields()[1]);
    await key(fields()[1], 'Escape');

    // An empty memory gets a look with that channel; a level beyond full is full.
    await press(button('Look at memory 9'));
    await doubleClick(1);
    fields()[0].value = '300';
    await key(fields()[0], 'Enter');
    expect(connection.last('SetMemoryChannel')).toEqual([9, 1, 255]);
    expect(store.memoryStored()[8]).toBe(true);

    // While the stack is live nothing that is stored changes: no fields.
    await stackStands({ ...connection.playback.stack, live: true });
    await doubleClick(1);
    expect(editor()).toBeNull();
  });

  it('Copy and Del of the Memories view keep to looks, and those of the Sequences view to sequences', async () => {
    const record = (view: string) =>
      [...page.querySelectorAll(`${view} lm-record-section button`)].map((item) =>
        item.getAttribute('aria-label'),
      );

    // memory 2 holds a look, memory 3 a sequence
    await show('/memories');
    expect(record('lm-memories-view')).toEqual(['Copy memory', 'Delete memory']);
    await press(button('Copy memory'));
    await press(button('Look at memory 3'));
    expect(store.notice()).toBe(
      'Memory 3 holds a sequence: here Copy copies looks. Sequences are on the Sequences view.',
    );
    await press(button('Look at memory 2'));
    await press(button('Look at memory 3'));
    expect(store.notice()).toBe(
      'Memory 3 holds a sequence: a look is copied onto a look or an empty memory.',
    );
    await press(button('Look at memory 6'));
    expect(connection.last('CopyMemory')).toEqual([2, 6]);
    expect(store.inspectedMemory()).toBe(6); // the copy is looked at

    await press(button('Delete memory'));
    await press(button('Look at memory 3'));
    expect(store.notice()).toBe(
      'Memory 3 holds a sequence: here Del deletes looks. Sequences are on the Sequences view.',
    );
    await press(button('Look at memory 6'));
    expect(connection.last('DeleteMemory')).toEqual([6]);

    // without Copy or Del a key only chooses the memory to look at
    await press(button('Look at memory 2'));
    expect(store.inspectedMemory()).toBe(2);

    await show('/sequences');
    expect(record('lm-sequences-view')).toEqual(['Copy sequence', 'Delete sequence']);
    await press(button('Copy sequence'));
    await press(button('Work on memory 2'));
    expect(store.notice()).toBe(
      'Memory 2 holds a look: here Copy copies sequences. Looks are on the Memories view.',
    );
    await press(button('Work on memory 3'));
    await press(button('Work on memory 2'));
    expect(store.notice()).toBe(
      'Memory 2 holds a look: a sequence is copied onto a sequence or an empty memory.',
    );
    await press(button('Work on memory 8'));
    expect(connection.last('CopyMemory')).toEqual([3, 8]);
    expect(store.inspectedMemory()).toBe(8);

    await press(button('Delete sequence'));
    await press(button('Work on memory 2'));
    expect(store.notice()).toBe(
      'Memory 2 holds a look: here Del deletes sequences. Looks are on the Memories view.',
    );
    await press(button('Work on memory 8'));
    expect(connection.last('DeleteMemory')).toEqual([8]);
  });

  it('RECORD stands under the page keys of the Memories and Sequences views', async () => {
    await show('/memories');
    expect(
      page.querySelector('lm-memories-view .side > nav.rail + lm-record-section'),
    ).toBeTruthy();
    await show('/sequences');
    expect(
      page.querySelector('lm-sequences-view .side > nav.rail + lm-record-section'),
    ).toBeTruthy();
  });
});
