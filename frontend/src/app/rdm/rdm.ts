import type { RdmBoxView, RdmLightView, RdmModeView } from '../desk/hub-contract';

export type { RdmBoxView, RdmLightView, RdmModeView };

/** The key of a light in the panel: its box and its ID, as one text. */
export function lightKey(box: string, id: string): string {
  return `${box} ${id}`;
}

/** The last DMX channel a light takes; its address for a light without channels. */
export function lastChannel(light: RdmLightView): number {
  return light.address + Math.max(light.channels, 1) - 1;
}

/** The channels a light takes, as the table says them: "5 to 20", "5" for one, "" for none. */
export function channelsText(light: RdmLightView): string {
  if (light.address < 1 || light.channels < 1) return '';
  return light.channels === 1 ? `${light.address}` : `${light.address} to ${lastChannel(light)}`;
}

/** What the table calls a light: the name it was given, else its model, else its ID. */
export function lightName(light: RdmLightView): string {
  return light.label || light.model || light.id;
}

/** The line under a light's name: maker and model, and its ID. */
export function lightAbout(light: RdmLightView): string {
  const made = [light.maker, light.label ? light.model : '']
    .filter((part) => part !== '')
    .join(' ');
  return made ? `${made}, ${light.id}` : light.id;
}

/** The heading of a box: "dmxbox at 10.1.3.1, box 3". */
export function boxTitle(box: RdmBoxView): string {
  return box.number === null
    ? `dmxbox at ${box.address}`
    : `dmxbox at ${box.address}, box ${box.number}`;
}

/** A mode as its choice says it: "2: 8 channels, dimmer and strobe", with its channels if the name lacks them. */
export function modeLabel(mode: RdmModeView, index: number): string {
  const name = mode.name || `Mode ${index + 1}`;
  const says = mode.channels > 0 && !name.includes(`${mode.channels} ch`);
  return `${index + 1}: ${name}${says ? ` (${mode.channels} ${mode.channels === 1 ? 'channel' : 'channels'})` : ''}`;
}

/**
 * What needs saying about the channels of the lights of one line: two lights that take the same
 * channel, and a light that goes beyond channel 512. One sentence each, in the order of the channels.
 */
export function addressRemarks(lights: readonly RdmLightView[]): string[] {
  const placed = lights
    .filter((light) => light.error === null && light.address >= 1 && light.channels >= 1)
    .sort((a, b) => a.address - b.address || lastChannel(a) - lastChannel(b));
  const remarks: string[] = [];
  placed.forEach((light, at) => {
    for (const other of placed.slice(at + 1)) {
      if (other.address > lastChannel(light)) break;
      const from = other.address;
      const to = Math.min(lastChannel(light), lastChannel(other));
      remarks.push(
        from === to
          ? `Channel ${from}: both ${lightName(light)} and ${lightName(other)} take it.`
          : `Channels ${from} to ${to}: both ${lightName(light)} and ${lightName(other)} take them.`,
      );
    }
    if (lastChannel(light) > 512)
      remarks.push(`${lightName(light)} goes beyond channel 512: its last ones get no DMX.`);
  });
  return remarks;
}
