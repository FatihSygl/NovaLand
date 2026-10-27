/* Pure helpers shared by the worker and regression checks. */
globalThis.NovaAudio = {
  host(url) {
    try { const u = new URL(url); return /^https?:$/.test(u.protocol) ? u.hostname.toLowerCase() : null; }
    catch { return null; }
  },
  volume(value) { return Number.isFinite(value) ? Math.max(0, Math.min(100, Math.round(value))) : null; },
  summarize(frames) {
    const live = frames.filter(f => f.count > 0);
    const primary = live.find(f => f.playing) || live[0];
    return primary ? {volume: primary.volume, muted: primary.muted, playing: live.some(f => f.playing), count: Math.min(256, live.reduce((n,f) => n + f.count, 0))} : null;
  }
};
