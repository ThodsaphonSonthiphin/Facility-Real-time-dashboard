/** Loads the page again (an object, so a test can watch it: jsdom cannot navigate). */
export const browserPage = {
  reload: () => window.location.reload(),
};
