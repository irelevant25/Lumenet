import { ACTION_GROUPS, DESK_ACTIONS } from '../desk/desk-actions';
import type { MidiMonitorLine } from './midi';
import {
  FADER_TARGETS,
  SLOT_KEYS,
  TARGET_GROUPS,
  controlText,
  isFader,
  resultText,
  targetGroup,
  targetLabel,
} from './midi-targets';

const line = (more: Partial<MidiMonitorLine>): MidiMonitorLine => ({
  at: 0,
  device: 'APC mini mk2',
  message: 'Note On 56, velocity 127, channel 1',
  target: null,
  result: 'unmapped',
  value: null,
  desk: null,
  reason: null,
  ...more,
});

describe('the targets of MIDI controls', () => {
  it('are the keys of the Keyboard panel, the keys of the pages shown and the faders, each once and in a kind', () => {
    const ids = [
      ...DESK_ACTIONS.map((action) => action.id),
      ...SLOT_KEYS.map((key) => key.id),
      ...FADER_TARGETS.map((fader) => fader.id),
    ];
    expect(new Set(ids).size).toBe(ids.length);
    expect(TARGET_GROUPS.slice(0, ACTION_GROUPS.length)).toEqual([...ACTION_GROUPS]);
    for (const group of TARGET_GROUPS)
      expect(
        ids.some((id) => targetGroup(id) === group),
        group,
      ).toBe(true);
    for (const id of ids) expect(TARGET_GROUPS, id).toContain(targetGroup(id));

    // a fader's name says it is one; a key's never does
    expect(FADER_TARGETS.every((fader) => isFader(fader.id))).toBe(true);
    expect([...DESK_ACTIONS, ...SLOT_KEYS].some((key) => isFader(key.id))).toBe(false);
  });

  it('have the names the desk program knows, the same for OSC later', () => {
    const ids = new Set(FADER_TARGETS.map((fader) => fader.id));
    for (const id of [
      'fader.channel.1',
      'fader.channel.4096',
      'fader.memory.128',
      'fader.master',
      'fader.rate',
      'fader.sequence.3',
      'fader.effect.64',
      'fader.effect-speed.1',
      'fader.effect-size.1',
      'fader.effect-spread.1',
      'fader.slot.channel.64',
      'fader.slot.memory.32',
      'fader.slot.effect.32',
    ]) {
      expect(ids, id).toContain(id);
    }
    expect(SLOT_KEYS.map((key) => key.id)).toContain('slot.memory.2');
  });

  it('are called as the desk calls them', () => {
    expect(targetLabel('stack.play')).toBe('GO');
    expect(targetLabel('channel.625')).toBe('Channel key 113 (U2)');
    expect(targetLabel('fader.channel.625')).toBe('Channel fader 113 (U2)');
    expect(targetLabel('fader.slot.memory.2')).toBe('Memory fader 2 of the page shown');
    expect(targetLabel('slot.channel.3')).toBe('Channel key 3 of the page shown');
    expect(targetLabel('fader.sequence.7')).toBe('Speed of the sequence in memory 7');
    expect(targetLabel('something.new')).toBe('something.new');
  });

  it('say a control and what became of its message in words', () => {
    expect(controlText({ message: 'note', number: 56, channel: 1 })).toBe('Note 56, channel 1');
    expect(controlText({ message: 'cc', number: 48, channel: 2 })).toBe('CC 48, channel 2');
    expect(controlText({ message: 'cc14', number: 7, channel: 1 })).toBe(
      'CC 7 + 39 (14 bits), channel 1',
    );
    expect(controlText({ message: 'pitchbend', number: 0, channel: 9 })).toBe(
      'Pitch Bend, channel 9',
    );
    expect(controlText({ message: 'program', number: 5, channel: 1 })).toBe('Program 5, channel 1');
    expect(controlText({ message: 'dmx', number: 12, channel: 1 })).toBe('DMX channel 12');

    expect(resultText(line({}))).toBe('Not mapped');
    expect(resultText(line({ target: 'stack.play', result: 'down' }))).toBe('GO pressed');
    expect(resultText(line({ target: 'fader.master', result: 'moved', value: 128 }))).toBe(
      'Master to 128',
    );
    expect(
      resultText(line({ target: 'fader.master', result: 'pickup', value: 10, desk: 200 })),
    ).toBe('Master waits until the control reaches it: the control is at 10, the desk at 200');
    expect(
      resultText(
        line({
          target: 'fader.sequence.3',
          result: 'refused',
          reason: 'Memory 3 holds no sequence.',
        }),
      ),
    ).toBe('Speed of the sequence in memory 3: Memory 3 holds no sequence.');
    expect(resultText(line({ target: 'stack.play', result: 'no-page' }))).toContain(
      'no page of the desk is open on this PC',
    );
  });
});
