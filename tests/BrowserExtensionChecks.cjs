// Run with: node tests/BrowserExtensionChecks.cjs
"use strict";
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const {randomUUID} = require("node:crypto");
const root = path.join(__dirname, "../src/NovaLand/BrowserAudioExtension");
let checks = 0;
function check(value, label) { assert.ok(value, label); checks++; }
const manifest = JSON.parse(fs.readFileSync(path.join(root, "manifest.json")));
check(manifest.permissions.includes("declarativeNetRequestWithHostAccess"), "redirect permission declared");
check(manifest.web_accessible_resources.some(r => r.resources.includes("blocked.html")), "redirect page accessible");
function event() { return {listeners: [], addListener(fn) { this.listeners.push(fn); }}; }
async function worker({rules = [], key = "a".repeat(64), failRules = false} = {}) {
  const state = {rules, writes: 0, open: [], updates: [], body: null, policy: false, commands: [], offline: false, unauthorized: false, failRules, mediaWrites: []};
  const storage = {key, client: randomUUID()};
  const timers = new Map();
  const chrome = {
    storage: {
      local: {get: async () => ({...storage}), set: async values => Object.assign(storage, values)},
      session: {set: async values => { state.connection = values.connection; }},
      onChanged: event()
    },
    runtime: {onMessage: event(), onStartup: event(), getURL: p => "chrome-extension://test/" + p},
    tabs: {
      query: async () => state.open.map(t => ({...t})),
      get: async id => { const t = state.open.find(t => t.id === id); if (!t) throw Error("closed"); return {...t}; },
      update: async (id, values) => { state.updates.push({id, ...values}); Object.assign(state.open.find(t => t.id === id), values); },
      sendMessage: async (id, message) => { state.mediaWrites.push({id, message}); return {count: 1, volume: message.volume ?? 100, playing: true}; },
      onRemoved: event(), onUpdated: event(), onActivated: event()
    },
    alarms: {create() {}, onAlarm: event()},
    declarativeNetRequest: {
      getDynamicRules: async () => state.rules,
      updateDynamicRules: async ({removeRuleIds, addRules}) => {
        if (state.failRules) throw Error("permission denied");
        state.rules = state.rules.filter(r => !removeRuleIds.includes(r.id)).concat(addRules); state.writes++;
      }
    }
  };
  const context = vm.createContext({
    chrome, URL, crypto: {randomUUID}, AbortSignal, console,
    setTimeout(fn) { const id = randomUUID(); timers.set(id, fn); return id; },
    clearTimeout(id) { timers.delete(id); }, setInterval() { return 1; }, clearInterval() {},
    fetch: async (_, options) => {
      state.body = JSON.parse(options.body);
      if (state.offline) throw Error("offline");
      return {ok: !state.unauthorized, json: async () => ({blockSocialSites: state.policy, commands: state.commands})};
    }
  });
  context.importScripts = (...files) => files.forEach(file => vm.runInContext(fs.readFileSync(path.join(root, file), "utf8"), context));
  vm.runInContext(fs.readFileSync(path.join(root, "background.js"), "utf8"), context);
  await vm.runInContext("ready", context);
  return {state, chrome, context, sync: () => vm.runInContext("sync()", context)};
}
(async () => {
  const w = await worker();
  for (const url of ["https://instagram.com", "https://www.instagram.com/reels/", "http://m.x.com", "https://twitter.com/home", "https://X.COM.", "https://a.b.twitter.com"]) {
    check(vm.runInContext("NovaAudio.socialHost(" + JSON.stringify(url) + ")", w.context), "target " + url);
  }
  for (const url of ["https://notinstagram.com", "https://x.com.evil.test", "https://example.org/?url=https://instagram.com", "https://instagram.com@safe.test", "file:///instagram.com", "not a URL", "https://youtube.com"]) {
    check(!vm.runInContext("NovaAudio.socialHost(" + JSON.stringify(url) + ")", w.context), "allow " + url);
  }
  w.state.open = [{id:1,url:"https://instagram.com/reels"}, {id:2,url:"https://x.com/home"}, {id:3,url:"https://youtube.com/watch?v=1"}, {id:4,url:"https://twitter.com",incognito:true}];
  await w.sync();
  check(w.state.writes === 0 && w.state.updates.length === 0, "default off leaves browser untouched");
  w.state.policy = true;
  await w.sync();
  check(w.state.rules.length === 1, "enabling installs exactly one rule");
  const rule = w.state.rules[0];
  check(rule.action.redirect.extensionPath === "/blocked.html" && rule.condition.resourceTypes.join() === "main_frame", "only page navigation redirected");
  check(rule.condition.requestDomains.join() === "instagram.com,x.com,twitter.com", "precise domain scope");
  check(w.state.updates.map(t => t.id).join() === "1,2", "existing target tabs redirected; other and incognito tabs untouched");
  await w.sync();
  check(w.state.body.socialSitesBlocked === true, "applied state acknowledged");
  check(w.state.writes === 1, "unchanged sync avoids rule rewrites");
  check(!JSON.stringify(w.state.body).includes("/reels"), "visited paths never sent to native app");
  w.state.offline = true;
  await w.sync();
  check(w.state.rules.length === 1, "last enabled choice survives app disconnect");
  w.state.open.push({id:5,url:"https://www.instagram.com/"});
  await vm.runInContext("NovaSocialBlock.enforce(5)", w.context);
  check(w.state.open.find(t=>t.id===5).url.endsWith("/blocked.html"), "cached/SPA navigation fallback works while offline");
  const restarted = await worker({rules: w.state.rules});
  check(vm.runInContext("NovaSocialBlock.enabled", restarted.context), "enabled rule restored on worker restart");
  restarted.state.policy = false;
  await restarted.sync();
  check(restarted.state.rules.length === 0, "turning off removes rule");
  check(!vm.runInContext("NovaSocialBlock.enabled", restarted.context), "disabled state confirmed");
  const unpaired = await worker({rules: w.state.rules, key: ""});
  check(unpaired.state.rules.length === 0, "removing pairing clears owned rule");
  const rejected = await worker();
  rejected.state.policy = true; rejected.state.unauthorized = true;
  await rejected.sync();
  check(rejected.state.rules.length === 0, "unauthenticated response cannot activate blocking");
  const denied = await worker({failRules:true});
  denied.state.policy = true;
  await denied.sync();
  check(!vm.runInContext("NovaSocialBlock.enabled", denied.context) && !denied.state.connection.ok, "failed rule installation never claims success");
  denied.state.failRules = false;
  await denied.sync();
  check(denied.state.rules.length === 1, "rule failure retried on next sync");

  // Use the real worker's media message and command paths alongside blocking.
  const audio = await worker();
  const tab = {id:11, url:"https://youtube.com/watch?v=private", active:true};
  audio.state.open = [tab];
  await new Promise(resolve => audio.chrome.runtime.onMessage.listeners[0](
    {type:"media-report",state:{count:1,volume:80,playing:true}},
    {tab,frameId:0,documentId:"doc"}, resolve));
  await audio.sync();
  const id = audio.state.body.tabs[0].id;
  audio.state.commands = [{id:randomUUID(), tab:id, volume:23}];
  audio.state.policy = true;
  await audio.sync();
  check(audio.state.mediaWrites.some(w=>w.message.type==="media-volume" && w.message.volume===23), "site volume command remains functional with blocking enabled");
  check(!JSON.stringify(audio.state.body).includes("private"), "media reporting still excludes URLs");
  console.log(checks + " browser extension checks passed");
})().catch(error => { console.error(error); process.exitCode = 1; });
