/**
 * Hands a file to the browser's own download, as a link with `download` would. The object URL is revoked a moment
 * later, once the browser has taken the file. Nothing is kept on the device by the page (facility-0068); the saved
 * file is the download the Admin asked for.
 */
export function saveFile(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.append(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1_000);
}
