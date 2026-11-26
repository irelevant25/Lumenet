import type { SoundFileInfo, SoundWave } from './sounds';

/*
 * Sound files and the desk program, over HTTP (the hub's messages are too small for songs): a file
 * sent from the Sounds view, and the ZIPs of Export and Import that take the sounds of a project
 * along. (The device of the sound is asked for by the device panel itself, over the hub.)
 */

/**
 * Asks the desk program for the file of Export (POST /api/export/pack): a ZIP with each part of the
 * text as a JSON file of its own, and the files of the sounds of its project in "sounds/". Fails with
 * what it said.
 */
export async function packExport(name: string, text: string): Promise<Response> {
  const response = await fetch(`/api/export/pack?name=${encodeURIComponent(name)}`, {
    method: 'POST',
    body: text,
    headers: { 'Content-Type': 'application/json' },
  });
  if (!response.ok) {
    const answer = (await response.json().catch(() => null)) as { message?: string } | null;
    throw new Error(
      answer?.message ??
        `The desk program could not put the sounds into the file (${response.status}).`,
    );
  }
  return response;
}

/** What a ZIP of Export brings: its file of Export, and the sound files that could not be taken. */
export interface UnpackedImport {
  readonly text: string;
  readonly leftOut: readonly string[];
}

/**
 * Hands a ZIP of Export to the desk program (POST /api/import/unpack): its sound files go into the
 * desk program's sounds, and its file of Export comes back to be imported. Fails with what it said.
 */
export async function unpackImport(file: File): Promise<UnpackedImport> {
  let response: Response;
  try {
    response = await fetch('/api/import/unpack', {
      method: 'POST',
      body: file,
      headers: { 'Content-Type': 'application/zip' },
    });
  } catch {
    throw new Error(`"${file.name}" could not be sent to the desk program.`);
  }
  const answer = (await response.json().catch(() => null)) as
    (Partial<UnpackedImport> & { message?: string }) | null;
  if (!response.ok || typeof answer?.text !== 'string') {
    throw new Error(
      answer?.message ?? `The desk program could not open "${file.name}" (${response.status}).`,
    );
  }
  return { text: answer.text, leftOut: answer.leftOut ?? [] };
}

// The waves fetched so far, by the hash of their file: a file of a hash never changes.
const waves = new Map<string, Promise<SoundWave | null>>();

/**
 * How loud a sound's file is along its length (GET /api/sounds/{hash}/wave), for the wave of the
 * Sounds view; null when the desk program cannot say. Each file is asked for once.
 */
export function fetchSoundWave(hash: string): Promise<SoundWave | null> {
  let wave = waves.get(hash);
  if (!wave) {
    wave = askWave(hash);
    waves.set(hash, wave);
    void wave.then((answer) => {
      if (!answer) waves.delete(hash); // asked again next time: the file may come
    });
  }
  return wave;
}

async function askWave(hash: string): Promise<SoundWave | null> {
  try {
    const response = await fetch(`/api/sounds/${encodeURIComponent(hash)}/wave`);
    if (!response.ok) return null;
    const answer = (await response.json()) as { seconds?: number; peaks?: string; levels?: string };
    if (typeof answer.peaks !== 'string' || typeof answer.levels !== 'string') return null;
    return {
      seconds: answer.seconds ?? 0,
      peaks: bytesOf(answer.peaks),
      levels: bytesOf(answer.levels),
    };
  } catch {
    return null;
  }
}

/** The bytes of a text in Base64, as the desk program sends byte arrays. */
function bytesOf(base64: string): Uint8Array {
  const text = atob(base64);
  const bytes = new Uint8Array(text.length);
  for (let index = 0; index < text.length; index++) bytes[index] = text.charCodeAt(index);
  return bytes;
}

/**
 * Hands a file to the desk program (POST /api/sounds), which keeps it among its sounds. The answer
 * says what it is, or, as a text, why it was not taken.
 */
export async function uploadSoundFile(file: File): Promise<SoundFileInfo | string> {
  try {
    const response = await fetch(`/api/sounds?name=${encodeURIComponent(file.name)}`, {
      method: 'POST',
      body: file,
      headers: { 'Content-Type': 'application/octet-stream' },
    });
    if (!response.ok) {
      const answer = (await response.json().catch(() => null)) as { message?: string } | null;
      return answer?.message ?? `The desk program did not take ${file.name} (${response.status}).`;
    }
    return (await response.json()) as SoundFileInfo;
  } catch {
    return `${file.name} could not be sent to the desk program.`;
  }
}
