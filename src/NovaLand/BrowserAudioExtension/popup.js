const key = document.getElementById("key"), status = document.getElementById("status");
chrome.storage.local.get("key").then(saved => { key.value = saved.key || ""; });
function show(connection) {
  if (connection?.ok && Date.now() - connection.at < 75000)
    status.textContent = connection.mediaTabs > 0
      ? "Bağlı · " + connection.mediaTabs + " medya sekmesi. NovaLand ana sayfasında Sistem menüsünden siteyi seç."
      : "Bağlı · Henüz medya bulunamadı. Bir video açıp Sekmeleri tara’ya bas.";
  else status.textContent = connection?.error === "pairing"
    ? "Kod eşleşmiyor. NovaLand ayarlarından yeni kodu kopyalayıp Bağlan’a bas."
    : "NovaLand’a ulaşılamıyor. Uygulamayı aç; uzantının site ve yerel bağlantı izinlerini kontrol et.";
}
async function rescan() {
  status.textContent = "Açık medya sekmeleri taranıyor…";
  try { show(await chrome.runtime.sendMessage({type:"audio-rescan"})); }
  catch { status.textContent = "Uzantı başlatılamadı. Uzantılar sayfasından Yenile’ye bas."; }
}
document.getElementById("save").addEventListener("click", async () => {
  const value = key.value.trim().toLowerCase();
  if (!/^[a-f0-9]{64}$/.test(value)) { status.textContent = "Uygulamadan kopyaladığın 64 karakterlik kodu gir."; return; }
  await chrome.storage.local.set({key:value}); await rescan();
});
document.getElementById("rescan").addEventListener("click",rescan);
async function refresh() {
  const {connection} = await chrome.storage.session.get("connection"); show(connection);
}
refresh(); setInterval(refresh,1500);
