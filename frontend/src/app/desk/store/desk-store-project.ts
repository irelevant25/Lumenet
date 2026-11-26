import {
  BoxPlan,
  EMPTY_STAGE,
  FoundNode,
  NetworkChoice,
  NO_HISTORY,
  OutputPlan,
  ProjectList,
  StagePlan,
} from '../desk-types';
import { IDLE } from '../function-mode';
import { RECOVERED_NOTICE_MS } from './desk-store-helpers';
import { DeskStoreStack } from './desk-store-stack';

/**
 * A layer of DeskStore: the project and what goes with it: the stage plan and its picture, the
 * universes, boxes and outputs, the autosave, undo and redo, LAN, the project's files, Import and
 * Export, and the notes of the project.
 */
export abstract class DeskStoreProject extends DeskStoreStack {
  /**
   * The note under the key of a fixture, or of a group, on the Fixtures page. It belongs to the
   * stage plan, which goes to the desk program whole.
   */
  setLightNote(lightId: number, note: string): void {
    const plan = this.stage();
    if ((plan.lights.find((light) => light.id === lightId)?.note ?? '') === note) return;
    this.setStage({
      ...plan,
      lights: plan.lights.map((light) => (light.id === lightId ? { ...light, note } : light)),
    });
  }

  setGroupNote(groupId: number, note: string): void {
    const plan = this.stage();
    if ((plan.groups.find((group) => group.id === groupId)?.note ?? '') === note) return;
    this.setStage({
      ...plan,
      groups: plan.groups.map((group) => (group.id === groupId ? { ...group, note } : group)),
    });
  }

  /**
   * Takes the stage plan as the owner edited it: shown at once, and sent whole. The desk
   * program answers every page with the plan as it keeps it; if it refuses, the page says why
   * and fetches the plan it has.
   */
  setStage(plan: StagePlan): void {
    this.stage.set(plan);
    this.connection.invoke('SetStage', plan).catch((error: Error) => {
      this.say(error.message);
      void this.fetchStage();
    });
  }

  /**
   * Puts a picture under the plan (a data URL of a JPEG, PNG or WebP image), or takes it away
   * (null). Shown at once; the desk program answers every page with the plan, which names it.
   */
  setStagePicture(picture: string | null): void {
    this.stagePicture.set(picture);
    this.connection.invoke('SetStagePicture', picture).catch((error: Error) => {
      this.say(error.message);
      this.stagePictureId = null; // fetched again with the plan
      void this.fetchStage();
    });
  }

  /**
   * How many universes the project has, 1 to 8. The desk program refuses fewer while a step, a
   * light of the plan or an independent key uses a channel of a universe that would go; the page
   * then says why. The whole desk comes back with the new number of channels.
   */
  setUniverses(universes: number): void {
    if (this.live() || universes === this.universes()) return;
    this.run('SetUniverses', universes);
  }

  /**
   * The project's list of boxes as the Boxes window changed it: a box added by hand, an output
   * given another universe, a box taken out; an empty list for automatic. The desk program
   * answers every page with the boxes as they are then. Rejects with its message when it refuses
   * the list (an address that is none, a box listed twice, the stack live).
   */
  async setBoxes(boxes: readonly BoxPlan[]): Promise<void> {
    await this.connection.invoke('SetBoxes', boxes);
  }

  /**
   * The project's outputs besides its boxes as the Outputs window changed them: an Art-Net node,
   * sACN or a USB interface added, a port changed, an output taken out. The desk program answers
   * every page with the outputs and their states. Rejects with its message when it refuses the
   * list (an address that is none, an output listed twice, Art-Net to a dmxbox, the stack live).
   */
  async setOutputs(outputs: readonly OutputPlan[]): Promise<void> {
    await this.connection.invoke('SetOutputs', outputs);
  }

  /**
   * Asks the Art-Net nodes on the PC's networks to answer: those that did, after a second and a
   * half. The answer comes as a stream (DeskConnection.ask), so the page's other calls go on meanwhile.
   */
  async findArtNetNodes(): Promise<FoundNode[]> {
    return (await this.connection.ask('FindArtNetNodes')) ?? [];
  }

  /** The serial ports of the PC, for a DMX USB interface; none from a desk program without outputs. */
  async serialPorts(): Promise<string[]> {
    try {
      return (await this.connection.invoke('GetSerialPorts')) ?? [];
    } catch {
      return [];
    }
  }

  /** The networks of the PC a broadcast or a multicast can go out on. */
  async networks(): Promise<NetworkChoice[]> {
    try {
      return (await this.connection.invoke('GetNetworks')) ?? [];
    } catch {
      return [];
    }
  }

  /** Switches the autosave and sets how often it keeps the work (on the PC only). */
  setAutosave(on: boolean, seconds: number): void {
    this.autosave.set({ on, seconds });
    this.connection
      .invoke('SetAutosave', on, seconds)
      .then((options) => {
        if (options) this.autosave.set(options);
      })
      .catch((error: Error) => this.say(error.message));
  }

  /**
   * Undo: what the project holds goes back to how it was before its last change. Memories,
   * sequences, the stack, notes and the independent keys count; the faders and Clear do not.
   * Locked while the stack is live, as every change of what is stored is.
   */
  undo(): void {
    this.moveInHistory('Undo', 'Undone');
  }

  /** Redo: a change that was undone is made again. */
  redo(): void {
    this.moveInHistory('Redo', 'Redone');
  }

  protected moveInHistory(method: 'Undo' | 'Redo', done: string): void {
    if (this.live()) return;
    // Fader moves of just before reach the desk first: a memory may be stored again by redo.
    this.flushOutbox();
    this.connection
      .invoke(method)
      .then((what) => this.say(`${done}: ${what}.`))
      .catch((error: Error) => this.say(error.message));
  }

  /**
   * Lets other devices in the network use the desk (with the password), or cuts them off.
   * Only a page on the PC itself can do this.
   */
  setLan(on: boolean): void {
    const lan = this.lan();
    if (!lan?.local || lan.on === on) return;
    // Lit at once. The desk answers with the address and the password a moment later.
    this.lan.set({ ...lan, on, password: on ? lan.password : null });
    this.run('SetLanAccess', on);
  }

  /**
   * Chooses the address of this PC at which devices in the network may come in, or lets the
   * desk choose by itself (null). The devices that are connected connect again.
   */
  setLanAddress(address: string | null): void {
    if (!this.lan()?.local) return;
    this.run('SetLanAddress', address);
  }

  /**
   * Makes the typed text the LAN password: every device has to give the new one. Resolves when
   * the desk took it; rejects with the desk's message (too short, other characters) when it did not.
   */
  async setLanPassword(password: string): Promise<void> {
    await this.connection.invoke('SetLanPassword', password);
  }

  /** Makes a new random LAN password. */
  newLanPassword(): void {
    if (!this.lan()?.local) return;
    this.run('NewLanPassword');
  }

  listProjects(): Promise<ProjectList> {
    return this.connection.invoke('GetProjects');
  }

  async saveProject(name: string): Promise<void> {
    this.project.set(await this.connection.invoke('SaveProject', name));
    this.say(`Project "${name}" saved`);
  }

  /** Starts an empty project: no memories, no notes, every fader at zero. */
  async newProject(): Promise<void> {
    this.mode.set(IDLE);
    this.dropOutbox();
    this.project.set(await this.connection.invoke('NewProject'));
  }

  async loadProject(name: string): Promise<void> {
    this.mode.set(IDLE);
    this.project.set(await this.connection.invoke('LoadProject', name));
  }

  /** Opens one of the examples that come with the desk program, as loading does; the example itself stays as it is. */
  async loadExample(name: string): Promise<void> {
    this.mode.set(IDLE);
    this.project.set(await this.connection.invoke('LoadExample', name));
  }

  /** The open project as its file says it, for Export. Nothing changes. */
  exportProject(): Promise<string> {
    return this.connection.invoke('ExportProject');
  }

  /**
   * Opens a project file from this device (Import) in place of the open project, as loading does.
   * The project is called `name` and has no file in the projects folder until it is saved.
   */
  async importProjectFile(name: string, text: string): Promise<void> {
    this.mode.set(IDLE);
    this.dropOutbox();
    this.project.set(await this.connection.invoke('ImportProjectFile', name, text));
  }

  /** Puts a stage from a file of this device (Import) in place of the project's: its lights, groups and picture. */
  async importStage(text: string): Promise<void> {
    await this.connection.invoke('ImportStage', text);
  }

  /**
   * The notes as the Notes view wrote them: shown here at once, and kept by the desk program,
   * which tells the other pages. Rejects with the desk program's message when it refuses them
   * (while the stack is live, or when they are too long).
   */
  async setNotes(notes: string): Promise<void> {
    this.notes.set(notes);
    try {
      await this.connection.invoke('SetNotes', notes);
    } catch (error) {
      void this.fetchNotes(); // the notes the desk program keeps, in place of those it refused
      throw error;
    }
  }

  /**
   * Opens the show of a theatre console, from a file in the USITT ASCII format, as a project in
   * place of the open one, as loading does. Gives back what the desk made of it, sentence by sentence.
   */
  async importShow(fileName: string, text: string): Promise<string[]> {
    this.mode.set(IDLE);
    const answer = await this.connection.invoke('ImportProject', fileName, text);
    this.project.set(answer.project);
    return answer.report;
  }

  protected async fetchLan(): Promise<void> {
    try {
      this.lan.set((await this.connection.invoke('GetLanAccess')) ?? null);
    } catch {
      // A desk program from before LAN does not know the question. Then the page has no LAN key.
      this.lan.set(null);
    }
  }

  protected async fetchHistory(): Promise<void> {
    try {
      this.history.set((await this.connection.invoke('GetHistory')) ?? NO_HISTORY);
    } catch {
      // A desk program from before undo: nothing can be undone.
      this.history.set(NO_HISTORY);
    }
  }

  protected async fetchAutosave(): Promise<void> {
    try {
      this.autosave.set((await this.connection.invoke('GetAutosave')) ?? null);
    } catch {
      // A desk program from before the autosave options.
      this.autosave.set(null);
    }
  }

  // Once per page: unsaved work that the desk program brought back at its start.
  protected async tellRecovered(): Promise<void> {
    if (this.recoveryTold) return;
    try {
      const kept = await this.connection.invoke('GetRecovered');
      if (!kept) return;
      this.recoveryTold = true;
      this.say(
        `The work that was not saved when the desk program stopped (${kept.slice(11, 16)}) is back. ` +
          'Save it, or load the project again to drop it.',
        RECOVERED_NOTICE_MS,
      );
    } catch {
      // A desk program from before the autosave.
    }
  }

  protected async fetchNotes(): Promise<void> {
    try {
      this.notes.set((await this.connection.invoke('GetNotes')) ?? '');
      this.notesKnown.set(true);
    } catch {
      // A desk program from before notes. The Notes view says so.
      this.notes.set('');
      this.notesKnown.set(false);
    }
  }

  protected async fetchStage(): Promise<void> {
    try {
      this.takeStage((await this.connection.invoke('GetStage')) ?? EMPTY_STAGE);
      this.stageKnown.set(true);
    } catch {
      // A desk program from before stage plans. The Stage view says so.
      this.takeStage(EMPTY_STAGE);
      this.stageKnown.set(false);
    }
  }

  // The plan as the desk program keeps it, with the picture it names: fetched when that is another one.
  protected takeStage(plan: StagePlan): void {
    // A desk program from before pictures and shapes leaves them out: a light is then round and one square big.
    const filled: StagePlan = {
      ...EMPTY_STAGE,
      ...plan,
      lights: (plan.lights ?? []).map((light) => ({
        ...light,
        shape: light.shape ?? 'round',
        width: light.width ?? 1,
        depth: light.depth ?? 1,
      })),
      types: plan.types ?? [],
      groups: plan.groups ?? [],
    };
    this.stage.set(filled);
    if (filled.pictureId === this.stagePictureId) return;

    this.stagePictureId = filled.pictureId;
    if (filled.pictureId === null) {
      this.stagePicture.set(null);
      return;
    }
    const wanted = filled.pictureId;
    this.connection
      .invoke('GetStagePicture')
      .then((picture) => {
        if (this.stagePictureId === wanted) this.stagePicture.set(picture ?? null);
      })
      .catch(() => this.stagePicture.set(null));
  }
}
