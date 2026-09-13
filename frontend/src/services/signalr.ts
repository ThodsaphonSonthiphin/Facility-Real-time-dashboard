import * as signalR from '@microsoft/signalr';
import type { ServicePointStatus } from '../types';
import { getValidAccessToken } from './authSession';

export function createScanHubConnection(onScanRecorded: (point: ServicePointStatus) => void): signalR.HubConnection {
  const connection = new signalR.HubConnectionBuilder()
    // ADR facility-0018: the hub requires login. SignalR calls the factory on every connect and reconnect,
    // so each attempt carries a token that is still valid (facility-0011 sends it as access_token).
    .withUrl('/hubs/scan', { accessTokenFactory: async () => (await getValidAccessToken()) ?? '' })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Information)
    .build();

  connection.on('ScanRecorded', (point: ServicePointStatus) => {
    onScanRecorded(point);
  });

  return connection;
}
