/**
 * Stands in for the @microsoft/signalr package in every test (vite.config.ts `test.alias`): regression-net #32 fakes
 * fetch and SignalR only, never our own code. It has just what shared/api/scanHub.ts uses. `hub` lets a test see the
 * connections the app built and play the server's part: push an event, drop the connection, bring it back.
 * test/setup.ts calls resetFakeHub after each test.
 */
type Listener = (...args: unknown[]) => void;

interface FakeConnectionOptions {
  accessTokenFactory?: () => string | Promise<string>;
}

export class FakeHubConnection {
  readonly url: string;
  readonly retryDelays: number[] | undefined;
  /** What accessTokenFactory gave on each start, in order. */
  readonly tokens: string[] = [];
  starts = 0;
  stops = 0;
  state: 'Disconnected' | 'Connecting' | 'Connected' | 'Reconnecting' = 'Disconnected';
  private readonly options: FakeConnectionOptions;
  private readonly listeners = new Map<string, Listener[]>();
  private readonly closedListeners: Array<(error?: Error) => void> = [];
  private readonly reconnectingListeners: Array<(error?: Error) => void> = [];
  private readonly reconnectedListeners: Array<(connectionId?: string) => void> = [];

  constructor(url: string, options: FakeConnectionOptions, retryDelays: number[] | undefined) {
    this.url = url;
    this.options = options;
    this.retryDelays = retryDelays;
  }

  on(name: string, listener: Listener): void {
    this.listeners.set(name, [...(this.listeners.get(name) ?? []), listener]);
  }

  onclose(callback: (error?: Error) => void): void {
    this.closedListeners.push(callback);
  }

  onreconnecting(callback: (error?: Error) => void): void {
    this.reconnectingListeners.push(callback);
  }

  onreconnected(callback: (connectionId?: string) => void): void {
    this.reconnectedListeners.push(callback);
  }

  async start(): Promise<void> {
    this.starts += 1;
    this.state = 'Connecting';
    this.tokens.push(String(await this.options.accessTokenFactory?.()));
    if (hub.failNextStarts > 0) {
      hub.failNextStarts -= 1;
      this.state = 'Disconnected';
      throw new Error('fake hub: negotiation failed');
    }
    this.state = 'Connected';
  }

  /** Like the real client, stopping an open connection calls the onclose callbacks. */
  async stop(): Promise<void> {
    this.stops += 1;
    const wasOpen = this.state !== 'Disconnected';
    this.state = 'Disconnected';
    if (wasOpen) {
      this.closedListeners.forEach((callback) => callback());
    }
  }

  /**
   * The server sends `name` with `payload` to this connection. A push expects no answer: the real client logs
   * "Result given for '<name>' method but server is not expecting a result" at Error level when a handler returns
   * one, so here that fails the test.
   */
  push(name: string, payload: unknown): void {
    (this.listeners.get(name) ?? []).forEach((listener) => {
      const result: unknown = listener(payload);
      if (result) {
        throw new Error(`fake hub: the handler for ${name} returned a result; a push expects none`);
      }
    });
  }

  /** The socket dropped and the automatic reconnect began. */
  reconnecting(): void {
    this.state = 'Reconnecting';
    this.reconnectingListeners.forEach((callback) => callback(new Error('fake hub: connection lost')));
  }

  /** The automatic reconnect got through. */
  reconnected(): void {
    this.state = 'Connected';
    this.reconnectedListeners.forEach((callback) => callback('fake-connection'));
  }

  /** The connection ended for good: the automatic reconnect gave up, or the server closed it. */
  close(): void {
    this.state = 'Disconnected';
    this.closedListeners.forEach((callback) => callback(new Error('fake hub: closed')));
  }
}

export class HubConnectionBuilder {
  private url = '';
  private options: FakeConnectionOptions = {};
  private retryDelays: number[] | undefined;

  withUrl(url: string, options: FakeConnectionOptions = {}): this {
    this.url = url;
    this.options = options;
    return this;
  }

  withAutomaticReconnect(retryDelays?: number[]): this {
    this.retryDelays = retryDelays;
    return this;
  }

  configureLogging(): this {
    return this;
  }

  build(): FakeHubConnection {
    const connection = new FakeHubConnection(this.url, this.options, this.retryDelays);
    hub.connections.push(connection);
    return connection;
  }
}

/** What a test sees and drives. failNextStarts: how many of the next start() calls fail, like a negotiate with no network. */
export const hub = {
  connections: [] as FakeHubConnection[],
  failNextStarts: 0,
  /** The newest connection; throws when the app built none. */
  latest(): FakeHubConnection {
    const connection = hub.connections.at(-1);
    if (!connection) {
      throw new Error('the app built no hub connection');
    }
    return connection;
  },
};

export function resetFakeHub(): void {
  hub.connections = [];
  hub.failNextStarts = 0;
}
