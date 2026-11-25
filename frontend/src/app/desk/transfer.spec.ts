import { TRANSFER_PARTS, TransferPart, exportFileName, exportText, missingParts, pcPartNote, readImportFile } from './transfer';

const PROJECT_FILE = JSON.stringify({
  format: 'lumenet-project',
  version: 4,
  notes: '<b>Doors</b>',
  memories: [],
  stage: { width: 12, depth: 8, lights: [{ name: 'Wash', address: 1, channels: 'RGB', x: 2, y: 2 }] },
});

const chosen = (...parts: TransferPart[]) => new Set<TransferPart>(parts);

describe('Export and Import', () => {
  it('put the chosen parts into one file, and nothing else', () => {
    const parts = { name: 'Concert', projectFile: PROJECT_FILE, settings: { channelSize: 16 }, shortcuts: { 'stack.play': 'Space' } };

    const all = JSON.parse(exportText(chosen('project', 'stage', 'settings', 'keyboard'), parts));
    expect(all.format).toBe('lumenet-export');
    expect(all.version).toBe(1);
    expect(all.name).toBe('Concert');
    expect(all.project.notes).toBe('<b>Doors</b>');
    expect(all.stage.width).toBe(12);
    expect(all.settings).toEqual({ channelSize: 16 });
    expect(all.keyboard).toEqual({ shortcuts: { 'stack.play': 'Space' } });

    const stageOnly = JSON.parse(exportText(chosen('stage'), parts));
    expect(Object.keys(stageOnly)).toEqual(['format', 'version', 'name', 'stage']);
  });

  it('name the file after the project and what it holds', () => {
    expect(exportFileName(chosen('project'), 'Concert')).toBe('Concert - project.json');
    expect(exportFileName(chosen('keyboard', 'stage'), 'A/B: show')).toBe('A-B- show - stage, keyboard.json');
    expect(exportFileName(chosen('settings', 'keyboard'), 'Concert')).toBe('Lumenet - settings, keyboard.json');
  });

  it('read back every part of a file of Export', () => {
    const text = exportText(chosen('project', 'settings', 'keyboard'), {
      name: 'Concert',
      projectFile: PROJECT_FILE,
      settings: { channelSize: 16 },
      shortcuts: { 'stack.play': 'Space' },
    });
    const source = readImportFile('Concert - project.json', text);
    expect(source.name).toBe('Concert');
    expect(JSON.parse(source.project!).notes).toBe('<b>Doors</b>');
    expect(JSON.parse(source.stage!).width).toBe(12); // the project's stage serves as the stage
    expect(source.settings).toEqual({ channelSize: 16 });
    expect(source.shortcuts).toEqual({ 'stack.play': 'Space' });
    expect(missingParts(chosen('project', 'stage', 'settings', 'keyboard'), source)).toBe('');
  });

  it('take a project file of the projects folder, with the settings an earlier version kept in it', () => {
    const earlier = JSON.stringify({ ...JSON.parse(PROJECT_FILE), settings: { channelSize: 8, shortcuts: { 'master.clear': 'KeyC' } } });
    const source = readImportFile('Evening.json', earlier);
    expect(source.name).toBe('Evening');
    expect(source.project).toBe(earlier);
    expect(JSON.parse(source.stage!).lights.length).toBe(1);
    expect(source.settings).toMatchObject({ channelSize: 8 });
    expect(source.shortcuts).toEqual({ 'master.clear': 'KeyC' });

    const now = readImportFile('Evening.json', PROJECT_FILE);
    expect(missingParts(chosen('project', 'settings', 'keyboard'), now)).toBe('the settings and the keyboard');
  });

  it('say what is wrong with a file they cannot use', () => {
    expect(() => readImportFile('photo.json', '{ not json')).toThrow('"photo.json" is no file of Lumenet');
    expect(() => readImportFile('list.json', '[1, 2]')).toThrow('is no file of Lumenet');
    expect(() => readImportFile('other.json', '{"format":"something"}')).toThrow('is no file of Lumenet');
    expect(() => readImportFile('new.json', '{"format":"lumenet-export","version":2}')).toThrow('made by a newer Lumenet');

    const empty = readImportFile('empty.json', '{"format":"lumenet-export","version":1}');
    expect(empty.name).toBe('empty');
    expect(missingParts(chosen('project', 'stage', 'keyboard'), empty)).toBe('the project, the stage and the keyboard');
  });

  it('take the MIDI of the PC out and back, and only on the PC', () => {
    const midi = {
      devices: ['APC mini mk2'],
      mappings: [{ device: 'APC mini mk2', channel: 1, message: 'note', number: 56, target: 'stack.play' }],
    };
    const text = exportText(chosen('midi'), { name: 'Concert', midi });
    expect(JSON.parse(text).midi).toEqual(midi);
    expect(exportFileName(chosen('midi'), 'Concert')).toBe('Lumenet - midi.json');

    const source = readImportFile('Lumenet - midi.json', text);
    expect(source.midi).toEqual(midi);
    expect(missingParts(chosen('midi'), source)).toBe('');
    expect(missingParts(chosen('keyboard', 'midi'), readImportFile('Evening.json', PROJECT_FILE))).toBe('the keyboard and the MIDI');
    // a MIDI part without its two lists is none
    expect(readImportFile('odd.json', '{"format":"lumenet-export","version":1,"midi":{"devices":"APC"}}').midi).toBeNull();

    // its checkbox: on the PC with a desk program that knows MIDI; the other parts anywhere
    const part = TRANSFER_PARTS.find((found) => found.part === 'midi')!;
    expect(pcPartNote(part, true, true)).toBe('');
    expect(pcPartNote(part, false, true)).toContain('only the PC');
    expect(pcPartNote(part, true, false)).toContain('knows no MIDI');
    expect(pcPartNote(TRANSFER_PARTS[0], false, false)).toBe('');
  });
});
