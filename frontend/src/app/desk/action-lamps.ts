import { slotKey } from './desk-actions';
import type { DeskStore } from './desk-store';
import { MEMORY_COUNT, UNIVERSE_SIZE } from './desk-types';
import { MAX_EFFECTS, effectLamp } from './effects';
import {
  activeFunction,
  channelKeysArmed,
  functionBlinks,
  heldMemory,
  memoryBlinks,
} from './function-mode';
import { LampLevel, channelLamp, channelPageLamp, memoryLamp, memoryPageLamp } from './lamps';
import { universeStart } from './universes';

/** How a key of the desk is lit: faint, half or full, and whether it blinks. */
export interface ActionLamp {
  readonly level: LampLevel;
  readonly blink: boolean;
}

const FAINT: ActionLamp = { level: 1, blink: false };
const LIT: ActionLamp = { level: 3, blink: false };

/**
 * How the key with this name (desk-actions.ts) is lit now, as the desk shows it, also where it is
 * not shown (another view, another page of faders): for the lights of the keys of a MIDI controller
 * mapped to it. A key that only works on its own view (Edit of the Stage view, Once of the Sequences
 * view and the like) or has no lamp is faint. `path` is the view shown ("/stage"), for the links.
 */
export function actionLamp(id: string, store: DeskStore, path: string): ActionLamp {
  const [kind, second] = id.split('.');
  const number = Number(id.slice(id.lastIndexOf('.') + 1));
  const lit = (on: boolean): ActionLamp => (on ? LIT : FAINT);

  switch (kind) {
    case 'view':
      return lit(path === (second === 'main' ? '/' : `/${second}`));
    case 'lan':
      return lit(store.lan()?.on === true);
    case 'master':
      return masterLamp(second, store);
    case 'stack':
      return stackLamp(second, store);
    case 'record': {
      const active = activeFunction(store.mode()) === second;
      return { level: active ? 3 : 1, blink: active && functionBlinks(store.mode()) };
    }
    case 'sequence':
      return sequenceLamp(second, store);
    case 'ind':
      return independentLamp(second, number, store);
    case 'universe': {
      if (number > store.universes()) return FAINT;
      const shown = store.shownUniverse() === number;
      return {
        level: channelPageLamp(store.shownChannels(), universeStart(number), UNIVERSE_SIZE, shown),
        blink: false,
      };
    }
    case 'page':
      return pageLamp(second, number, store);
    case 'channel':
      if (number > store.channelCount()) return FAINT;
      return {
        level: channelLamp(store.shownChannels()[number - 1]),
        blink: channelKeysArmed(store.mode()),
      };
    case 'memory':
      return memoryKeyLamp(number, store);
    case 'effect': {
      const playback = store.effectPlaybacks().get(number);
      return {
        level: effectLamp(store.effectsByNumber().get(number), playback),
        blink: playback?.fading ?? false,
      };
    }
    case 'slot': {
      // a key of the pages shown: lit as the key under that fader of the page shown
      const key = slotKey(id, store);
      return key ? actionLamp(key, store, path) : FAINT;
    }
    default:
      return FAINT;
  }
}

// Blackout blinks while it is on; Clear is always lit, as on the desk; Solo and Rate while they are on.
function masterLamp(name: string, store: DeskStore): ActionLamp {
  switch (name) {
    case 'blackout':
      return { level: 1, blink: store.blackout() };
    case 'clear':
      return LIT;
    case 'solo':
      return store.soloMode() === 'up' ? LIT : FAINT;
    case 'solo-down':
      return store.soloMode() === 'down' ? LIT : FAINT;
    case 'rate':
      return store.rateOn() ? LIT : FAINT;
    default:
      return FAINT;
  }
}

function stackLamp(name: string, store: DeskStore): ActionLamp {
  switch (name) {
    case 'live':
      return store.live() ? LIT : FAINT;
    case 'play':
      return store.stackRunning() ? LIT : FAINT;
    case 'pause':
      return store.stackPaused() ? { level: 3, blink: true } : FAINT;
    default:
      return FAINT;
  }
}

// Once, Loop and Random of the sequence that the Sequences view shows: the one it is played as.
function sequenceLamp(name: string, store: DeskStore): ActionLamp {
  const sequence = store.sequences()[store.inspectedMemory()];
  if (!sequence || !['once', 'loop', 'random'].includes(name)) return FAINT;
  const mode = sequence.random ? 'random' : sequence.loop ? 'loop' : 'once';
  return mode === name ? LIT : FAINT;
}

function independentLamp(kind: string, key: number, store: DeskStore): ActionLamp {
  switch (kind) {
    case 'channel': {
      const channel = store.independents()[key - 1];
      return channel ? (store.shownChannels()[channel - 1] > 0 ? LIT : FAINT) : FAINT;
    }
    case 'memory': {
      const memory = store.memoryIndependents()[key - 1];
      return memory
        ? {
            level: memoryLamp(store.memoryLevels()[memory - 1], store.memoryStored()[memory - 1]),
            blink: false,
          }
        : FAINT;
    }
    case 'effect': {
      const effect = store.effectIndependents()[key - 1];
      if (!effect) return FAINT;
      const playback = store.effectPlaybacks().get(effect);
      return {
        level: effectLamp(store.effectsByNumber().get(effect), playback),
        blink: playback?.fading ?? false,
      };
    }
    default:
      return FAINT;
  }
}

// A page key: full for the page shown, half for a page with something up or in it.
function pageLamp(kind: string, page: number, store: DeskStore): ActionLamp {
  switch (kind) {
    case 'channel': {
      const perPage = store.channelsPerPage();
      const offset = (page - 1) * perPage;
      if (offset >= UNIVERSE_SIZE) return FAINT;
      const first = universeStart(store.shownUniverse()) + offset;
      return {
        level: channelPageLamp(
          store.shownChannels(),
          first,
          perPage,
          first === store.firstChannel(),
        ),
        blink: false,
      };
    }
    case 'memory': {
      const perPage = store.memoriesPerPage();
      const first = (page - 1) * perPage + 1;
      if (first > MEMORY_COUNT) return FAINT;
      return {
        level: memoryPageLamp(
          store.memoryLevels(),
          store.memoryStored(),
          first,
          perPage,
          first === store.firstMemory(),
        ),
        blink: false,
      };
    }
    case 'effect': {
      const perPage = store.stripsPerRow();
      const first = (page - 1) * perPage + 1;
      if (first > MAX_EFFECTS) return FAINT;
      if (first === store.firstEffect()) return LIT;
      const effects = store.effectsByNumber();
      const holds = Array.from(
        { length: Math.min(perPage, MAX_EFFECTS - first + 1) },
        (_, index) => first + index,
      ).some((effect) => effects.has(effect));
      return { level: holds ? 2 : 1, blink: false };
    }
    default:
      return FAINT;
  }
}

// A memory key: lit while its fader is up, half while it holds something; the one a Record
// function works on stays lit; the keys a Record function waits for blink.
function memoryKeyLamp(memory: number, store: DeskStore): ActionLamp {
  if (memory < 1 || memory > MEMORY_COUNT) return FAINT;
  const mode = store.mode();
  const level =
    heldMemory(mode) === memory
      ? 3
      : memoryLamp(store.memoryLevels()[memory - 1], store.memoryStored()[memory - 1]);
  return { level, blink: memoryBlinks(mode, memory, store.memoryContent(memory)) };
}
