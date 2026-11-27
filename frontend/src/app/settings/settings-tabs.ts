/**
 * The tabs of Settings, in their order. The address of each is /settings#<id>, so a shortcut or a
 * window can open one (the Outputs shortcut opens #outputs, Listen of the keyboard comes back to
 * #keyboard); /settings alone shows General.
 */
export const SETTINGS_TABS = [
  { id: 'general', label: 'General' },
  { id: 'lan', label: 'LAN' },
  { id: 'sound', label: 'Sound' },
  { id: 'colors', label: 'Colors' },
  { id: 'keyboard', label: 'Keyboard' },
  { id: 'outputs', label: 'Outputs' },
  { id: 'rdm', label: 'RDM' },
  { id: 'backup', label: 'Backup' },
  { id: 'about', label: 'About' },
] as const;

export type SettingsTab = (typeof SETTINGS_TABS)[number]['id'];

/** The tab an address names (its fragment, "keyboard"); General for none or an unknown one. */
export function tabOf(fragment: string | null | undefined): SettingsTab {
  return SETTINGS_TABS.find((tab) => tab.id === fragment)?.id ?? 'general';
}
