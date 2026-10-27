const key = document.getElementById("key"), status = document.getElementById("status");
chrome.storage.local.get("key").then(saved => { key.value = saved.key || ""; });
document.getElementById("save").addEventListener("click", async () => {
  const value = key.value.trim().toLowerCase();
  if (!/^[a-f0-9]{64}$/.test(value)) { status.textContent = "Uygulamadan kopyaladığın 64 karakterlik kodu gir."; return; }
  await chrome.storage.local.set({key:value}); status.textContent = "Kod kaydedildi. NovaLand açıkken bağlantı kurulacak.";
});
async function refresh() {
  const {connection} = await chrome.storage.session.get("connection");
  if (connection?.ok && Date.now() - connection.at < 15000) status.textContent = "Bağlı · Sesleri NovaLand’dan ayarlayabilirsin.";
}
refresh(); setInterval(refresh, 1500);
