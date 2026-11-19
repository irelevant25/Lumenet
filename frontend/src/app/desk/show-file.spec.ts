import { readShowFile } from './show-file';

describe('reading a show file', () => {
  it('reads UTF-8, and leaves out the mark some editors put in front', async () => {
    const bytes = new Uint8Array([0xef, 0xbb, 0xbf, ...new TextEncoder().encode('Text Scéna\n')]);
    expect(await readShowFile(new Blob([bytes]))).toBe('Text Scéna\n');
  });

  it('reads a file of an older program in the Windows code page', async () => {
    // "Scéna" with é as the single byte 0xE9, which is no UTF-8
    const bytes = new Uint8Array([0x53, 0x63, 0xe9, 0x6e, 0x61]);
    expect(await readShowFile(new Blob([bytes]))).toBe('Scéna');
  });
});
