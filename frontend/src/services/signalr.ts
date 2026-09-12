import * as signalR from '@microsoft/signalr';
import { ServicePointStatus } from '../types';

export function createScanHubConnection(onScanRecorded: (point: ServicePointStatus) => void): signalR.HubConnection {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/scan')
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Information)
    .build();

  connection.on('ScanRecorded', (point: ServicePointStatus) => {
    onScanRecorded(point);
  });

  return connection;
}
