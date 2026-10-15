import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import '@fontsource/ibm-plex-sans-thai/400.css';
import '@fontsource/ibm-plex-sans-thai/600.css';
import '@fontsource/ibm-plex-sans-thai/700.css';
import './index.css';

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <p>กำลังเตรียมระบบ</p>
  </StrictMode>,
);
