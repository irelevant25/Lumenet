/**
 * The picture under the stage plan: a photo or a drawing of the stage that the owner chooses.
 * The page makes it smaller before it sends it, so that the project file stays small: at most
 * 1600 pixels on its longer side, as a JPEG.
 */

/** The longer side of a picture as the plan keeps it, in pixels. */
export const PICTURE_SIDE = 1600;

/** The most characters a picture can have: what the desk program takes. */
export const PICTURE_MAX_LENGTH = 4_000_000;

/** A picture as the plan keeps it: a data URL, and its size in pixels. */
export interface Picture {
  readonly data: string;
  readonly width: number;
  readonly height: number;
}

/** The size of a picture made smaller so that its longer side is at most `longest` pixels; a smaller one keeps its size. */
export function scaledSize(width: number, height: number, longest: number): { width: number; height: number } {
  const factor = Math.min(1, longest / Math.max(width, height, 1));
  return { width: Math.max(1, Math.round(width * factor)), height: Math.max(1, Math.round(height * factor)) };
}

/** Reads a picture file and makes it smaller, see above. */
export async function readPicture(file: File): Promise<Picture> {
  if (!file.type.startsWith('image/')) throw new Error(`"${file.name}" is no picture.`);

  const url = URL.createObjectURL(file);
  try {
    const image = await loaded(url, file.name);
    const size = scaledSize(image.naturalWidth, image.naturalHeight, PICTURE_SIDE);
    const canvas = document.createElement('canvas');
    canvas.width = size.width;
    canvas.height = size.height;
    const context = canvas.getContext('2d');
    if (!context) throw new Error('This browser cannot make the picture smaller.');
    context.drawImage(image, 0, 0, size.width, size.height);

    const data = canvas.toDataURL('image/jpeg', 0.85);
    if (data.length > PICTURE_MAX_LENGTH) throw new Error(`"${file.name}" is too big, even made smaller.`);
    return { data, ...size };
  } finally {
    URL.revokeObjectURL(url);
  }
}

/** The size of a picture that the plan keeps, in pixels. */
export async function sizeOf(data: string): Promise<{ width: number; height: number }> {
  const image = await loaded(data, 'The picture');
  return { width: image.naturalWidth, height: image.naturalHeight };
}

async function loaded(source: string, name: string): Promise<HTMLImageElement> {
  const image = new Image();
  image.src = source;
  try {
    await image.decode();
  } catch {
    throw new Error(`"${name}" cannot be read as a picture.`);
  }
  return image;
}
