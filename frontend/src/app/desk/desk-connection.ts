import { Service, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';

/** Where the backend's SignalR hub is. The dev server passes this path on to the backend (proxy.conf.json). */
const HUB_URL = '/hub/desk';

/** How long to wait before trying to connect again. */
const RETRY_MS = 1000;

/**
 * The live connection to the desk program (the .NET backend).
 * It keeps trying for as long as the page is open.
 */
@Service()
export class DeskConnection {
  /** True while the page is connected to the desk program. */
  readonly online = signal(false);

  private readonly hub: HubConnection;
  private readonly connectedCallbacks: (() => void)[] = [];
  private started = false;

  constructor() {
    this.hub = new HubConnectionBuilder()
      .withUrl(HUB_URL)
      // Never give up: the desk program may be started long after the page.
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: () => RETRY_MS })
      // The page shows by itself when the backend is missing; no need to fill the console with it.
      .configureLogging(LogLevel.None)
      .build();

    this.hub.onreconnecting(() => this.online.set(false));
    this.hub.onreconnected(() => this.announceConnected());
    this.hub.onclose(() => {
      this.online.set(false);
      this.connectLater();
    });
  }

  /** Starts connecting. Safe to call more than once. */
  start(): void {
    if (this.started) return;
    this.started = true;
    void this.connect();
  }

  /** Runs `callback` every time a connection is made: the first time and after every reconnect. */
  whenConnected(callback: () => void): void {
    this.connectedCallbacks.push(callback);
  }

  /** Listens for a message the backend sends on its own. */
  on<Args extends unknown[]>(event: string, handler: (...args: Args) => void): void {
    this.hub.on(event, handler as (...args: unknown[]) => void);
  }

  /** Calls a method of the backend and waits for its answer. Fails with a plain message. */
  async invoke<Result>(method: string, ...args: unknown[]): Promise<Result> {
    try {
      return await this.hub.invoke<Result>(method, ...args);
    } catch (error) {
      throw new Error(plainMessage(error));
    }
  }

  /** Calls a method of the backend without waiting. While offline the call is simply dropped. */
  send(method: string, ...args: unknown[]): void {
    if (!this.online()) return;
    this.hub.send(method, ...args).catch(() => {
      // The connection went away in this moment. The page reloads everything when it is back.
    });
  }

  private async connect(): Promise<void> {
    try {
      await this.hub.start();
      this.announceConnected();
    } catch {
      this.connectLater();
    }
  }

  private connectLater(): void {
    setTimeout(() => void this.connect(), RETRY_MS);
  }

  private announceConnected(): void {
    this.online.set(true);
    for (const callback of this.connectedCallbacks) callback();
  }
}

/**
 * SignalR wraps the backend's message in a sentence of its own
 * ("An unexpected error occurred invoking ... HubException: <message>"). Keep only the message.
 *
 * A backend that does not know the method at all is a desk program from before the page
 * learned to ask for it: the page is served fresh while the program keeps running. Say that,
 * instead of SignalR's "Unknown hub method".
 */
function plainMessage(error: unknown): string {
  const text = error instanceof Error ? error.message : String(error);
  if (text.includes('Unknown hub method')) {
    return 'The desk program is older than this page and cannot do that yet. Start run.cmd again.';
  }

  const marker = 'HubException: ';
  const at = text.indexOf(marker);
  return at >= 0 ? text.slice(at + marker.length) : text;
}
