import { CHANNEL_COUNT, MEMORY_COUNT, pageStart } from './desk-types';
import { channelLamp, channelPageLamp, memoryLamp, memoryPageLamp } from './lamps';

function levels(count: number, raised: Record<number, number> = {}): number[] {
  const list = new Array<number>(count).fill(0);
  for (const [number, level] of Object.entries(raised)) list[Number(number) - 1] = level;
  return list;
}

function flags(count: number, on: number[] = []): boolean[] {
  const list = new Array<boolean>(count).fill(false);
  for (const number of on) list[number - 1] = true;
  return list;
}

describe('lamps', () => {
  it('a channel key is faint at zero and full above zero', () => {
    expect(channelLamp(0)).toBe(1);
    expect(channelLamp(1)).toBe(3);
    expect(channelLamp(255)).toBe(3);
  });

  it('a memory key is faint when empty, half when something is stored, full when its fader is up', () => {
    expect(memoryLamp(0, false)).toBe(1);
    expect(memoryLamp(0, true)).toBe(2);
    expect(memoryLamp(10, true)).toBe(3);
    expect(memoryLamp(10, false)).toBe(3);
  });

  it('the channel page that is shown is full; another page is half when a channel on it is up', () => {
    const channels = levels(CHANNEL_COUNT, { 64: 1, 449: 200 });
    expect(channelPageLamp(channels, 65, 64, true)).toBe(3); // shown, although nothing is up on it
    expect(channelPageLamp(channels, 1, 64, false)).toBe(2); // 1 to 64: channel 64 is up
    expect(channelPageLamp(channels, 65, 64, false)).toBe(1); // 65 to 128: nothing
    expect(channelPageLamp(channels, 449, 64, false)).toBe(2); // 449 to 512: channel 449 is up
    // with pages of 32 the same channel 64 sits on the page 33 to 64, not on 1 to 32
    expect(channelPageLamp(channels, 1, 32, false)).toBe(1);
    expect(channelPageLamp(channels, 33, 32, false)).toBe(2);
  });

  it('the memory page that is shown is full; another page is half when it holds or plays something', () => {
    const memoryLevels = levels(MEMORY_COUNT, { 33: 90 });
    const stored = flags(MEMORY_COUNT, [1, 128]);
    expect(memoryPageLamp(memoryLevels, stored, 65, 32, true)).toBe(3); // shown, although it is empty
    expect(memoryPageLamp(memoryLevels, stored, 1, 32, false)).toBe(2); // 1 to 32: memory 1 is stored
    expect(memoryPageLamp(memoryLevels, stored, 33, 32, false)).toBe(2); // 33 to 64: fader 33 is up
    expect(memoryPageLamp(memoryLevels, stored, 65, 32, false)).toBe(1); // 65 to 96: nothing
    expect(memoryPageLamp(memoryLevels, stored, 113, 16, false)).toBe(2); // 113 to 128: memory 128 is stored
  });

  it('a page starts at the right number for either page size', () => {
    expect(pageStart(1, 64)).toBe(1);
    expect(pageStart(64, 64)).toBe(1);
    expect(pageStart(65, 64)).toBe(65);
    expect(pageStart(40, 32)).toBe(33);
    expect(pageStart(512, 32)).toBe(481);
  });
});
