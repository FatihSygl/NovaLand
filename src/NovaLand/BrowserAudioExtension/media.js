(() => {
  "use strict";
  const known = new WeakSet();
  let target = null, timer, reportTimer, scanTimer, stopped = false, previousCount = 0;
  const media = () => [...document.querySelectorAll("video,audio")];
  function apply(element, value) {
    try { element.volume = value / 100; element.muted = false; } catch { /* Unsupported player. */ }
  }
  function discover() {
    let changed = false;
    for (const element of media()) if (!known.has(element)) {
      changed = true;
      known.add(element);
      if (target !== null) apply(element, target);
      for (const event of ["play", "pause", "volumechange", "loadedmetadata", "emptied"])
        element.addEventListener(event, queueReport);
    }
    return changed;
  }
  function snapshot() {
    const all = media(), primary = all.find(m => !m.paused && !m.ended) || all[0];
    return {count: Math.min(256, all.length), volume: primary ? Math.round(primary.volume * 100) : 100,
      muted: !!primary?.muted, playing: all.some(m => !m.paused && !m.ended)};
  }
  function queueReport() {
    clearTimeout(reportTimer); reportTimer = setTimeout(() => { reportTimer = null; report(); }, 120);
  }
  async function report() {
    if (stopped) return;
    clearTimeout(timer); discover(); const state = snapshot(); previousCount = state.count;
    try { await chrome.runtime.sendMessage({type: "media-report", state}); }
    catch { stopped = true; observer.disconnect(); return; }
    // Fast updates only for a media player or the visible page; no animation loop.
    timer = setTimeout(report, state.count ? 1800 : document.visibilityState === "visible" ? 5000 : 10000);
  }
  chrome.runtime.onMessage.addListener((message, sender, reply) => {
    if (message?.type === "media-snapshot") { reply(snapshot()); return; }
    if (message?.type !== "media-volume" || !Number.isInteger(message.volume) || message.volume < 0 || message.volume > 100) return;
    target = message.volume; discover(); const all = media();
    for (const element of all) apply(element, target);
    reply(snapshot()); queueReport();
  });
  const observer = new MutationObserver(() => {
    if (!scanTimer) scanTimer = setTimeout(() => {
      scanTimer = null;
      if (discover() || media().length !== previousCount) queueReport();
    }, 250);
  });
  observer.observe(document.documentElement, {childList: true, subtree: true});
  document.addEventListener("visibilitychange", queueReport);
  report();
})();
