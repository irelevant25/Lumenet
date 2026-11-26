import { Component, computed, inject, linkedSignal } from '@angular/core';
import { DeskMidi } from '../desk-midi';
import type { MidiDeviceView, MidiPresetView } from '../midi';

/**
 * The MIDI devices of the PC, in the MIDI panel: each with a box that says whether the desk uses
 * (opens) it, and how it is; under them a ready-made mapping for one of them.
 */
@Component({
  selector: 'lm-midi-devices',
  styleUrl: './midi-devices.css',
  templateUrl: './midi-devices.html',
})
export class MidiDevices {
  protected readonly midi = inject(DeskMidi);

  protected readonly devices = computed(() => this.midi.state()?.devices ?? []);
  protected readonly presets = computed(() => this.midi.state()?.presets ?? []);
  protected readonly works = computed(() => this.midi.state()?.works ?? true);

  /** The device a ready-made mapping is for: the first one the desk uses, else the first there is. */
  protected readonly device = linkedSignal(() => {
    const devices = this.devices();
    return (devices.find((device) => device.used) ?? devices[0])?.name ?? '';
  });

  /** The ready-made mapping chosen: the one whose controller has the name of the device, if any. */
  protected readonly preset = linkedSignal(() => {
    const name = this.device().toLowerCase();
    const presets = this.presets();
    return (
      (presets.find((preset) => name.includes(preset.device.toLowerCase())) ?? presets[0])?.id ?? ''
    );
  });

  protected readonly chosenPreset = computed<MidiPresetView | undefined>(() =>
    this.presets().find((preset) => preset.id === this.preset()),
  );

  protected use(device: MidiDeviceView, event: Event): void {
    void this.midi.setDevice(device.name, (event.target as HTMLInputElement).checked);
  }

  protected chooseDevice(event: Event): void {
    this.device.set((event.target as HTMLSelectElement).value);
  }

  protected choosePreset(event: Event): void {
    this.preset.set((event.target as HTMLSelectElement).value);
  }

  protected usePreset(): void {
    if (this.preset() && this.device()) void this.midi.usePreset(this.preset(), this.device());
  }

  /** How a device is, in words. */
  protected stateText(device: MidiDeviceView): string {
    switch (device.state) {
      case 'open':
        return device.output ? 'In use' : 'In use (no output: its lights cannot be set)';
      case 'missing':
        return 'Not plugged in';
      case 'failed':
        return device.problem ?? 'It cannot be opened';
      default:
        return 'Not used';
    }
  }
}
