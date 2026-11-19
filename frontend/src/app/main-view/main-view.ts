import { Component } from '@angular/core';
import { ChannelSection } from '../channel-section/channel-section';
import { Independents } from '../independents/independents';
import { MasterSection } from '../master-section/master-section';
import { MemorySection } from '../memory-section/memory-section';
import { RecordSection } from '../record-section/record-section';

/**
 * The main view: the channels, the memories, the master section with the record keys under
 * it, and at the right edge the independents.
 */
@Component({
  imports: [ChannelSection, Independents, MasterSection, MemorySection, RecordSection],
  selector: 'lm-main-view',
  styleUrl: './main-view.css',
  templateUrl: './main-view.html',
})
export class MainView {}
