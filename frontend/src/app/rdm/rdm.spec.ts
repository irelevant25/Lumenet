import {
  RdmLightView,
  addressRemarks,
  boxTitle,
  channelsText,
  lightAbout,
  lightName,
  modeLabel,
} from './rdm';

function light(address: number, channels: number, more: Partial<RdmLightView> = {}): RdmLightView {
  return {
    box: '10.1.3.1',
    id: `7FF1:0000000${address}`,
    maker: 'Lumenet test',
    model: 'LED Par RGBW',
    label: '',
    address,
    channels,
    mode: 1,
    modes: [{ channels, name: `${channels} channels` }],
    identify: false,
    error: null,
    ...more,
  };
}

describe('RDM', () => {
  it('says the channels a light takes', () => {
    expect(channelsText(light(5, 16))).toBe('5 to 20');
    expect(channelsText(light(7, 1))).toBe('7');
    expect(channelsText(light(0, 0))).toBe(''); // a device without channels
  });

  it('calls a light by its name, else by its model, else by its ID', () => {
    expect(lightName(light(1, 4, { label: 'Front left' }))).toBe('Front left');
    expect(lightName(light(1, 4))).toBe('LED Par RGBW');
    expect(lightName(light(1, 4, { model: '' }))).toBe('7FF1:00000001');
    expect(lightAbout(light(1, 4, { label: 'Front left' }))).toBe(
      'Lumenet test LED Par RGBW, 7FF1:00000001',
    );
    expect(lightAbout(light(1, 4))).toBe('Lumenet test, 7FF1:00000001'); // the model is its name already
    expect(lightAbout(light(1, 4, { maker: '', model: '' }))).toBe('7FF1:00000001');
  });

  it('names a box by its address and number', () => {
    const box = {
      address: '10.1.3.1',
      number: 3,
      rdm: true,
      searching: false,
      lights: [],
      error: null,
    };
    expect(boxTitle(box)).toBe('dmxbox at 10.1.3.1, box 3');
    expect(boxTitle({ ...box, address: '127.0.0.1:52011', number: null })).toBe(
      'dmxbox at 127.0.0.1:52011',
    );
  });

  it('says a mode with its channels when its name does not', () => {
    expect(modeLabel({ channels: 8, name: '8 channels, dimmer and strobe' }, 1)).toBe(
      '2: 8 channels, dimmer and strobe',
    );
    expect(modeLabel({ channels: 16, name: 'Extended' }, 2)).toBe('3: Extended (16 channels)');
    expect(modeLabel({ channels: 1, name: '' }, 0)).toBe('1: Mode 1 (1 channel)');
    expect(modeLabel({ channels: 0, name: 'Basic' }, 0)).toBe('1: Basic');
  });

  it('says where two lights take the same channels, and a light beyond 512', () => {
    const lights = [
      light(1, 4, { label: 'A' }),
      light(3, 4, { label: 'B' }),
      light(6, 1, { label: 'C' }),
      light(20, 8, { label: 'D' }),
      light(510, 8, { label: 'E' }),
    ];
    expect(addressRemarks(lights)).toEqual([
      'Channels 3 to 4: both A and B take them.',
      'Channel 6: both B and C take it.',
      'E goes beyond channel 512: its last ones get no DMX.',
    ]);
    expect(addressRemarks([light(1, 4), light(5, 4)])).toEqual([]); // side by side is fine
    expect(
      addressRemarks([light(1, 4), light(2, 4, { error: 'The light does not answer.' })]),
    ).toEqual([]);
  });
});
