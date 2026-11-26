/** What the browser's window for saving a file gives back (File System Access, in Chrome and Edge). */
interface SavedFile {
  readonly name: string;
  createWritable(): Promise<
    WritableStream<Uint8Array> & {
      write(data: string | Blob): Promise<void>;
      close(): Promise<void>;
    }
  >;
}

type SavePicker = (options: unknown) => Promise<SavedFile>;

/** A kind of file as the window for saving offers it. */
interface FileKind {
  readonly description: string;
  readonly type: string;
  readonly ending: string;
}

const ZIP_FILE: FileKind = {
  description: 'Lumenet file',
  type: 'application/zip',
  ending: '.zip',
};

/**
 * Saves a ZIP the desk program makes (the file of Export) on this device: where its owner chooses,
 * in a browser that asks (Chrome and Edge on a PC, on a page of this PC), else among the downloads.
 * The window for saving opens first, while the press that asked for it still counts; the desk
 * program is asked after, and what it answers goes into the file as it comes. Resolves to the name
 * the file got; null when the choice of the place was cancelled.
 */
export function saveZipFile(name: string, made: () => Promise<Response>): Promise<string | null> {
  return saveFile(name, ZIP_FILE, made);
}

async function saveFile(
  name: string,
  kind: FileKind,
  content: () => Promise<string | Response>,
): Promise<string | null> {
  const picker = (globalThis as { showSaveFilePicker?: SavePicker }).showSaveFilePicker;
  if (typeof picker === 'function') {
    let file: SavedFile;
    try {
      file = await picker.call(globalThis, {
        suggestedName: name,
        types: [{ description: kind.description, accept: { [kind.type]: [kind.ending] } }],
      });
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') return null;
      throw error;
    }
    const made = await content();
    const writable = await file.createWritable();
    if (made instanceof Response && made.body) {
      await made.body.pipeTo(writable); // closes it at the end
    } else {
      await writable.write(made instanceof Response ? await made.blob() : made);
      await writable.close();
    }
    return file.name;
  }

  const made = await content();
  const blob = made instanceof Response ? await made.blob() : new Blob([made], { type: kind.type });
  const address = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = address;
  link.download = name;
  link.click();
  setTimeout(() => URL.revokeObjectURL(address), 10_000);
  return name;
}
