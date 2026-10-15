// Let the unmodified shared game-skill client use this project's Playwright dependency.
export async function resolve(specifier, context, nextResolve) {
  if (specifier === 'playwright') return {url: new URL('../node_modules/playwright/index.mjs', import.meta.url).href, shortCircuit: true};
  return nextResolve(specifier, context);
}
