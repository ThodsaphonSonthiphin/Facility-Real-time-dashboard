export type CleanerTabKey = 'my-work' | 'check-in' | 'scan-qr' | 'my-schedule';

export type CleanerGuideKey = 'check-in' | 'scan-qr';

export interface CleanerGuide {
  title: string;
  detail: string;
}
