/**
 * Reads a show file of a theatre console as text. Such files are plain ASCII as a rule; letters
 * beyond it come as UTF-8 from newer programs and in the Windows code page from older ones. So a
 * file that is not valid UTF-8 is read as Windows-1252, where every byte is a letter.
 */
export async function readShowFile(file: Blob): Promise<string> {
  const bytes = await file.arrayBuffer();
  try {
    return new TextDecoder('utf-8', { fatal: true }).decode(bytes);
  } catch {
    return new TextDecoder('windows-1252').decode(bytes);
  }
}
