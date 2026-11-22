/** What the browser's window for saving a file gives back (File System Access, in Chrome and Edge). */
interface SavedFile {
  readonly name: string;
  createWritable(): Promise<{ write(text: string): Promise<void>; close(): Promise<void> }>;
}

type SavePicker = (options: unknown) => Promise<SavedFile>;

/**
 * Saves a text as a JSON file on this device: where its owner chooses, in a browser that asks
 * (Chrome and Edge on a PC, on a page of this PC), else among the downloads. Resolves to the name
 * the file got; null when the choice of the place was cancelled.
 */
export async function saveTextFile(name: string, text: string): Promise<string | null> {
  const picker = (globalThis as { showSaveFilePicker?: SavePicker }).showSaveFilePicker;
  if (typeof picker === 'function') {
    let file: SavedFile;
    try {
      file = await picker.call(globalThis, {
        suggestedName: name,
        types: [{ description: 'Lumenet file', accept: { 'application/json': ['.json'] } }],
      });
    } catch (error) {
      if (error instanceof DOMException && error.name === 'AbortError') return null;
      throw error;
    }
    const writable = await file.createWritable();
    await writable.write(text);
    await writable.close();
    return file.name;
  }

  const address = URL.createObjectURL(new Blob([text], { type: 'application/json' }));
  const link = document.createElement('a');
  link.href = address;
  link.download = name;
  link.click();
  setTimeout(() => URL.revokeObjectURL(address), 10_000);
  return name;
}
