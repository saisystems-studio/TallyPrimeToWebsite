export function getInstallInstructions(userAgent = '') {
  if (/Edg\//i.test(userAgent)) {
    return 'In Microsoft Edge, open Settings and more (⋯), choose Apps, then Install this site as an app.';
  }
  if (/Chrome\//i.test(userAgent) && !/Edg\//i.test(userAgent)) {
    return 'In Google Chrome, open ⋮, choose Cast, save and share, then Install page as app. You can also use the install icon in the address bar.';
  }
  if (/Firefox\//i.test(userAgent)) {
    return 'Firefox on Windows does not install websites as standalone apps. Open Tally Web in Chrome or Edge and choose its Install option.';
  }
  if (/Safari\//i.test(userAgent) && !/Chrome\//i.test(userAgent)) {
    return 'Safari installation options vary by device. On Windows, open Tally Web in Chrome or Edge and choose its Install option.';
  }
  return 'Open Tally Web in Microsoft Edge or Google Chrome and choose Install from the address-bar icon or browser menu.';
}
