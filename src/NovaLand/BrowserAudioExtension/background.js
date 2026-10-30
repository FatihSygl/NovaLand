"use strict";
importScripts("core.js", "social-block.js");
const API = "http://127.0.0.1:47864/sync";
const tabs = new Map();
let config = {}, busy = false, nextSync, pulse, lastSync = 0;
const acks = new Set();
const ready = chrome.storage.local.get(["key", "client", "preferences"]).then(async saved => {
  config = {key: saved.key || "", client: saved.client || crypto.randomUUID(), preferences: saved.preferences || {}};
  if (!saved.client) await chrome.storage.local.set({client: config.client});
  await NovaSocialBlock.initialize();
  if (!config.key) await NovaSocialBlock.apply(false);
});
chrome.storage.onChanged.addListener((changes, area) => {
  if (area === "local" && changes.key) {
    ready.then(async () => {
      config.key = changes.key.newValue || "";
      if (!config.key) await NovaSocialBlock.apply(false);
      schedule();
    }).catch(() => {});
  }
});
function schedule() {
  if (nextSync) return;
  nextSync = setTimeout(() => { nextSync = null; sync(); }, Math.max(0, 700 - (Date.now() - lastSync)));
}
function maintainPulse() {
  if ([...tabs.values()].some(t => [...t.frames.values()].some(f => f.count > 0))) {
    if (!pulse) pulse = setInterval(schedule, 1000);
  } else { clearInterval(pulse); pulse = null; }
}
async function sendVolume(tabId, tab, volume) {
  const frames = [...tab.frames.entries()].filter(([,f]) => f.count > 0);
  let applied = false;
  for (const [frameId, frame] of frames) {
    try {
      const options = frame.documentId ? {documentId: frame.documentId} : {frameId};
      const result = await chrome.tabs.sendMessage(tabId, {type:"media-volume", volume}, options);
      if (tabs.get(tabId) !== tab) return false; // Navigation invalidated the command.
      if (result?.count > 0) { Object.assign(frame, result, {at: Date.now()}); applied = true; }
    } catch { tab.frames.delete(frameId); }
  }
  return applied;
}
chrome.runtime.onMessage.addListener((message, sender, reply) => {
  if (message?.type !== "media-report" || !sender.tab || sender.tab.incognito) return;
  (async () => {
    await ready;
    const host = NovaAudio.host(sender.tab.url);
    if (!host || !message.state || !Number.isInteger(message.state.count) || message.state.count < 0 || message.state.count > 256) return;
    const volume = NovaAudio.volume(message.state.volume); if (volume === null) return;
    let tab = tabs.get(sender.tab.id);
    if (!tab || tab.host !== host) { tab = {id: crypto.randomUUID(), host, frames: new Map()}; tabs.set(sender.tab.id, tab); }
    const prior = tab.frames.get(sender.frameId);
    const frame = {count: message.state.count, volume, muted: !!message.state.muted, playing: !!message.state.playing, documentId: sender.documentId, at: Date.now()};
    tab.frames.set(sender.frameId, frame);
    // Restore only a user-selected site level when a new player/document appears.
    const preferred = config.preferences[host];
    if (Number.isInteger(preferred) && frame.count > 0 && (!prior || prior.count === 0 || prior.documentId !== frame.documentId))
      await sendVolume(sender.tab.id, tab, preferred);
    maintainPulse(); schedule();
  })().then(() => reply({ok:true})).catch(() => reply({ok:false}));
  return true;
});
async function sync() {
  await ready;
  if (busy || !config.key) return;
  busy = true; lastSync = Date.now();
  const ackBatch = [...acks];
  try {
    const open = await chrome.tabs.query({}); const current = new Map(open.map(t => [t.id,t]));
    const entries = [];
    for (const [id, tab] of tabs) {
      const actual = current.get(id);
      if (!actual || actual.incognito || NovaAudio.host(actual.url) !== tab.host) { tabs.delete(id); continue; }
      // Hidden/silent tabs may have heavily throttled page timers. Refresh their
      // existing media frames from the worker so a muted tab remains controllable.
      for (const [frameId,frame] of tab.frames) if (Date.now() - frame.at > 5000) {
        try {
          const options = frame.documentId ? {documentId:frame.documentId} : {frameId};
          const snapshot = await chrome.tabs.sendMessage(id, {type:"media-snapshot"}, options);
          if (snapshot && tabs.get(id) === tab) Object.assign(frame, snapshot, {at:Date.now()});
          else tab.frames.delete(frameId);
        } catch { tab.frames.delete(frameId); }
      }
      const state = NovaAudio.summarize([...tab.frames.values()]);
      if (state) entries.push({id:tab.id, host:tab.host, active:!!actual.active, ...state, muted:state.muted || !!actual.mutedInfo?.muted});
    }
    const response = await fetch(API, {method:"POST", headers:{"Content-Type":"application/json", Authorization:"Bearer " + config.key},
      body:JSON.stringify({client:config.client, tabs:entries.slice(0,64), acks:ackBatch, socialSitesBlocked:NovaSocialBlock.enabled}), signal:AbortSignal.timeout(2500)});
    if (!response.ok) throw new Error("Connection");
    const result = await response.json(); ackBatch.forEach(id => acks.delete(id));
    const policyChanged = await NovaSocialBlock.apply(result.blockSocialSites === true, open);
    if (policyChanged) schedule(); // Confirm application on the next ordinary sync.
    await chrome.storage.session.set({connection:{ok:true, at:Date.now()}});
    for (const command of (result.commands || []).slice(0,64)) {
      if (acks.has(command.id)) continue;
      const entry = [...tabs.entries()].find(([,t]) => t.id === command.tab);
      if (entry && Number.isInteger(command.volume) && command.volume >= 0 && command.volume <= 100) {
        const [id,tab] = entry;
        if (await sendVolume(id,tab,command.volume)) {
          await chrome.tabs.update(id, {muted:false});
          config.preferences[tab.host] = command.volume;
          await chrome.storage.local.set({preferences:config.preferences});
        }
      }
      acks.add(command.id);
    }
    if (acks.size > 128) acks.clear();
    if (result.commands?.length) schedule();
  } catch {
    await chrome.storage.session.set({connection:{ok:false, at:Date.now()}}).catch(() => {});
  } finally { busy = false; maintainPulse(); }
}
chrome.tabs.onRemoved.addListener(id => { tabs.delete(id); maintainPulse(); schedule(); });
chrome.tabs.onUpdated.addListener((id, change) => {
  if (change.url || change.status === "loading") {
    tabs.delete(id);
    ready.then(() => NovaSocialBlock.enforce(id)).catch(() => {});
  }
  maintainPulse(); schedule();
});
chrome.tabs.onActivated.addListener(schedule);
chrome.alarms.create("nova-audio", {periodInMinutes:0.5});
chrome.alarms.onAlarm.addListener(alarm => { if (alarm.name === "nova-audio") schedule(); });
chrome.runtime.onStartup.addListener(schedule);

ready.then(schedule).catch(() => {});
