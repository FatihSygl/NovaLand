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
check(manifest.version === "0.9.0", "audio-only extension version");
check(!manifest.web_accessible_resources, "blocking page is no longer exposed");
function event() { return {listeners: [], addListener(fn) { this.listeners.push(fn); }}; }
async function worker({rules = [], key = "a".repeat(64), failRules = false, open = []} = {}) {
  const state = {rules, writes: 0, open, injected: [], updates: [], body: null, policy: false, commands: [], offline: false, unauthorized: false, failRules, mediaWrites: []};
  const storage = {key, client: randomUUID()};
  const timers = new Map();
  const chrome = {
    storage: {
      local: {get: async () => ({...storage}), set: async values => Object.assign(storage, values)},
      session: {get:async () => ({connection:state.connection}), set: async values => { state.connection = values.connection; }},
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
    scripting: {executeScript:async options => { state.injected.push(options); return []; }},
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
  check(!fs.existsSync(path.join(root, "social-block.js")), "blocking implementation removed");
  check(!fs.existsSync(path.join(root, "blocked.html")), "blocking page removed");
  check(manifest.content_scripts[0].exclude_matches.includes("*://*.whatsapp.com/*"), "WhatsApp remains outside media integration");
  w.state.open = [{id:1,url:"https://instagram.com/"}, {id:2,url:"https://x.com/home"}, {id:3,url:"https://youtube.com/"}];
  w.state.policy = true; // Even an older app cannot restore blocking in the new worker.
  await w.sync();
  check(w.state.rules.length === 0 && w.state.updates.length === 0, "no rule added or page redirected");
  check(!("socialSitesBlocked" in w.state.body), "no blocking state reported");
  const old = {id:7001, action:{type:"redirect"}}, other = {id:9000,action:{type:"allow"}};
  const migrated = await worker({rules:[old,other],key:""});
  check(migrated.state.rules.length === 1 && migrated.state.rules[0].id === 9000, "unpaired startup removes only the legacy rule");
  check(migrated.state.writes === 1, "legacy rule removed once");
  await migrated.sync();
  check(migrated.state.writes === 1, "sync never reinstalls removed rule");
  const failedCleanup = await worker({rules:[old],failRules:true});
  await failedCleanup.sync();
  check(failedCleanup.state.connection.ok, "cleanup failure does not disable site audio connection");

  for (const hostname of ["web.whatsapp.com", "whatsapp.com", "mmg.whatsapp.net"]) {
    const isolated = vm.createContext({location:{hostname}});
    vm.runInContext(fs.readFileSync(path.join(root,"media.js"),"utf8"),isolated);
    check(true, "WhatsApp runs no media code: "+hostname);
  }
  const tab = {id:11,url:"https://youtube.com/watch?v=private",active:true};
  w.state.open = [tab];
  await new Promise(resolve => w.chrome.runtime.onMessage.listeners[0](
    {type:"media-report",state:{count:1,volume:80,playing:true}},
    {tab,frameId:0,documentId:"doc"},resolve));
  await w.sync();
  const id = w.state.body.tabs[0].id;
  w.state.commands = [{id:randomUUID(),tab:id,volume:23}];
  await w.sync();
  check(w.state.mediaWrites.some(write=>write.message.type==="media-volume" && write.message.volume===23), "site volume still works");
  check(!JSON.stringify(w.state.body).includes("private"), "visited paths remain private");
  w.state.commands = [];
  const wa = {id:12,url:"https://web.whatsapp.com/"};
  w.state.open.push(wa);
  await new Promise(resolve=>w.chrome.runtime.onMessage.listeners[0](
    {type:"media-report",state:{count:1,volume:80,playing:true}},
    {tab:wa,frameId:0,documentId:"wa"},resolve));
  await w.sync();
  check(!w.state.body.tabs.some(t=>t.host==="web.whatsapp.com"), "no WhatsApp media reporting");
  check(!w.state.mediaWrites.some(t=>t.id===12), "no WhatsApp media commands");
  const alreadyOpen = await worker({open:[
    {id:21,url:"https://www.youtube.com/watch?v=already-open"},
    {id:22,url:"https://kick.com/player"},
    {id:23,url:"https://web.whatsapp.com"},
    {id:24,url:"chrome://extensions"},
    {id:25,url:"https://youtube.com",incognito:true}
  ]});
  await vm.runInContext("rescanMedia()",alreadyOpen.context);
  check(alreadyOpen.state.injected.some(x=>x.target.tabId===21), "already-open YouTube is bootstrapped without page reload");
  check(alreadyOpen.state.injected.some(x=>x.target.tabId===22), "other existing media sites are bootstrapped");
  check(alreadyOpen.state.injected.every(x=>[21,22].includes(x.target.tabId)), "bootstrap excludes WhatsApp, private and internal pages");
  const scanReply = await new Promise(resolve=>alreadyOpen.chrome.runtime.onMessage.listeners[0]({type:"audio-rescan"},{},resolve));
  check(scanReply.ok, "popup rescan confirms native connection");
  check(manifest.permissions.includes("scripting"), "bootstrap permission declared");
  let observers = 0, reports = 0;
  const pageContext = vm.createContext({
    location:{hostname:"www.youtube.com"},
    document:{documentElement:{},visibilityState:"visible",querySelectorAll:()=>[],addEventListener(){}},
    MutationObserver:class {constructor(){observers++;} observe(){} disconnect(){}},
    chrome:{runtime:{onMessage:event(),sendMessage:async()=>{reports++;}}},
    setTimeout:()=>1,clearTimeout(){}
  });
  const mediaCode = fs.readFileSync(path.join(root,"media.js"),"utf8");
  vm.runInContext(mediaCode,pageContext); await Promise.resolve();
  vm.runInContext(mediaCode,pageContext); await Promise.resolve();
  check(observers===1, "rescanning does not duplicate observers or page integration");
  check(reports===1, "rescanning requests refresh without duplicate immediate media reports");
  console.log(checks + " browser extension checks passed");
})().catch(error=>{console.error(error);process.exitCode=1;});
