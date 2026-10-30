/* Native DNR rules do the blocking even while the worker sleeps. No browsing log. */
"use strict";
globalThis.NovaSocialBlock = (() => {
  const ruleId = 7001;
  let enabled = false, queue = Promise.resolve();
  async function initialize() {
    enabled = (await chrome.declarativeNetRequest.getDynamicRules()).some(rule => rule.id === ruleId);
  }
  async function enforce(tabId) {
    if (!enabled) return;
    try {
      // Re-read the current URL: do not redirect a tab that has already left the site.
      const tab = await chrome.tabs.get(tabId);
      if (enabled && !tab.incognito && NovaAudio.socialHost(tab.url))
        await chrome.tabs.update(tabId, {url: chrome.runtime.getURL("blocked.html")});
    } catch { /* A tab may close during the check. */ }
  }
  function apply(value, openTabs = []) {
    const work = queue.then(async () => {
      const next = value === true, changed = next !== enabled;
      if (changed) {
        await chrome.declarativeNetRequest.updateDynamicRules({
          removeRuleIds: [ruleId],
          addRules: next ? [{
            id: ruleId, priority: 1,
            action: {type: "redirect", redirect: {extensionPath: "/blocked.html"}},
            condition: {requestDomains: ["instagram.com", "x.com", "twitter.com"], resourceTypes: ["main_frame"]}
          }] : []
        });
        enabled = next;
      }
      if (enabled) await Promise.allSettled(openTabs.filter(tab => !tab.incognito && NovaAudio.socialHost(tab.url)).map(tab => enforce(tab.id)));
      return changed;
    });
    queue = work.catch(() => {});
    return work;
  }
  return {initialize, apply, enforce, get enabled() { return enabled; }};
})();
