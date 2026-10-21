import { useEffect, useRef, useState } from 'react';
import { getInstallInstructions } from './installInstructions';

export default function InstallAppButton() {
  const promptRef = useRef(null);
  const [installed, setInstalled] = useState(false);
  const [help, setHelp] = useState('');

  useEffect(() => {
    const displayMode = window.matchMedia('(display-mode: standalone)');
    const updateInstalled = () => setInstalled(Boolean(displayMode.matches || navigator.standalone === true));
    const handleBeforeInstall = (event) => {
      event.preventDefault();
      promptRef.current = event;
    };
    const handleInstalled = () => {
      promptRef.current = null;
      setInstalled(true);
      setHelp('Tally Web is installed. You can open it from the Start Menu or desktop shortcut if your browser created one.');
    };

    updateInstalled();
    displayMode.addEventListener?.('change', updateInstalled);
    window.addEventListener('beforeinstallprompt', handleBeforeInstall);
    window.addEventListener('appinstalled', handleInstalled);
    return () => {
      displayMode.removeEventListener?.('change', updateInstalled);
      window.removeEventListener('beforeinstallprompt', handleBeforeInstall);
      window.removeEventListener('appinstalled', handleInstalled);
    };
  }, []);

  const install = async () => {
    const installPrompt = promptRef.current;
    if (!installPrompt) {
      setHelp(getInstallInstructions(navigator.userAgent));
      return;
    }

    promptRef.current = null;
    try {
      await installPrompt.prompt();
      const choice = await installPrompt.userChoice;
      if (choice?.outcome === 'accepted') {
        setHelp('The browser accepted the install request. Installation will be confirmed when it completes.');
      } else {
        setHelp('Installation was cancelled. You can try again from the browser install option.');
      }
    } catch {
      setHelp(getInstallInstructions(navigator.userAgent));
    }
  };

  if (installed) return help ? <div className="siteInstallHelp siteInstallHelp-confirmed" role="status">
    <span>{help}</span>
    <button type="button" onClick={() => setHelp('')} aria-label="Dismiss installation message">×</button>
  </div> : null;

  return <div className="siteInstallControl">
    <button type="button" className="siteRefreshButton siteInstallButton" onClick={install} aria-expanded={Boolean(help)}>
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M12 3v11m0 0 4-4m-4 4-4-4M5 16v3h14v-3" />
      </svg>
      <span>Install App</span>
    </button>
    {help && <div className="siteInstallHelp" role="status">
      <span>{help}</span>
      <button type="button" onClick={() => setHelp('')}>Dismiss</button>
    </div>}
  </div>;
}
