using NovaIsland.Services;
using System.Text.Json;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
var root = Path.Combine(Path.GetTempPath(), "novaland-check-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root); Environment.SetEnvironmentVariable("XDG_DATA_HOME",root); int count = 0;
void Assert(bool ok, string title) { if (!ok) throw new Exception(title); Console.WriteLine("PASS " + title); count++; }
void Reject(Action action, string title) { try { action(); } catch (InvalidDataException) { Assert(true,title); return; } throw new Exception(title); }
var moment = DateTimeOffset.UtcNow;
var queue = new NovaIsland.Models.ActivityQueue<string>();
queue.Enqueue("song", 20, "song", moment);
queue.Tick(moment.AddMilliseconds(3999));
Assert(queue.Active?.Value == "song", "notice remains until four seconds");
queue.Tick(moment.AddSeconds(4));
Assert(queue.Active is null, "notice expires at four seconds");
queue.Enqueue("song", 20, "song", moment);
queue.Enqueue("headset", 70, "headset", moment.AddSeconds(1));
Assert(queue.Active?.Value == "headset", "headset preempts song");
queue.Tick(moment.AddSeconds(5));
Assert(queue.Active?.Value == "song", "song resumes after four-second headset notice");
queue.Pause(true, moment.AddSeconds(6));
queue.Tick(moment.AddSeconds(20));
Assert(queue.Active?.Value == "song", "expanded panel pauses notice countdown");
queue.Pause(false, moment.AddSeconds(20));
queue.Tick(moment.AddSeconds(23));
Assert(queue.Active is null, "remaining notice time expires after resume");
var firstTask = new NovaIsland.Models.TodoEntry(Guid.NewGuid(), "First", false);
var chosenTask = new NovaIsland.Models.TodoEntry(Guid.NewGuid(), "Chosen", false);
var homeTasks = new[] { firstTask, chosenTask };
Assert(NovaIsland.Models.HomeSelection.Task(homeTasks, null, false) == firstTask, "idle home selects first open task");
Assert(NovaIsland.Models.HomeSelection.Task(homeTasks, chosenTask.Id, true) == chosenTask, "running focus keeps selected task regardless of list order");
chosenTask.IsCompleted = true;
Assert(NovaIsland.Models.HomeSelection.Task(homeTasks, chosenTask.Id, true) is null, "completed task does not silently transfer running focus to another task");
Assert(NovaIsland.Models.HomeSelection.Task(homeTasks, chosenTask.Id, false) == firstTask, "idle home skips completed selected task");
Assert(NovaIsland.Models.HomeSelection.Task(new[] { firstTask }, chosenTask.Id, true) is null, "deleted focus task releases association");
var version = new Version(1,0,0);
string Json(string tag="v1.0.1", bool draft=false, bool prerelease=false, string? url=null, string? digest=null, long size=5) => JsonSerializer.Serialize(new { tag_name=tag, draft, prerelease, assets=new[]{new { name=$"NovaLand_{tag}_win-x64.zip", browser_download_url=url??$"https://github.com/FatihSygl/NovaLand/releases/download/{tag}/NovaLand_{tag}_win-x64.zip",size,digest=digest??"sha256:"+new string('a',64)}} });
Assert(UpdatePackage.ParseRelease(Json(),version)?.Version==new Version(1,0,1),"new stable release selected");
Assert(UpdatePackage.ParseRelease(Json("v1.0.0"),version)==null,"same version ignored");
Assert(UpdatePackage.ParseRelease(Json("v0.9.5"),version)==null,"downgrade rejected");
Assert(UpdatePackage.ParseRelease(Json(draft:true),version)==null,"draft ignored");
Assert(UpdatePackage.ParseRelease(Json(prerelease:true),version)==null,"prerelease ignored");
Reject(()=>UpdatePackage.ParseRelease(Json(url:"https://example.com/evil.zip"),version),"foreign URL rejected");
Reject(()=>UpdatePackage.ParseRelease(Json(digest:"sha256:broken"),version),"invalid digest rejected");
Reject(()=>UpdatePackage.ParseRelease(Json(size:UpdatePackage.MaxDownload+1),version),"oversized download rejected");
var tiny=Path.Combine(root,"bytes");await File.WriteAllTextAsync(tiny,"hello");
var hash=Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(tiny)));
var release=new UpdateRelease(version,new Uri("https://github.com"),5,hash);
await UpdatePackage.VerifyAsync(tiny,release,default);Assert(true,"valid hash accepted");
await File.WriteAllTextAsync(tiny,"HELLO");
try { await UpdatePackage.VerifyAsync(tiny,release,default);throw new Exception("tamper accepted"); }catch(InvalidDataException){Assert(true,"same-size tamper rejected");}
string Zip(string name, Action<ZipArchive>? add=null) {
 var path=Path.Combine(root,Guid.NewGuid()+".zip");using var z=ZipFile.Open(path,ZipArchiveMode.Create);var e=z.CreateEntry(name);using(var w=new StreamWriter(e.Open()))w.Write("bad");add?.Invoke(z);return path;
}
foreach(var bad in new[]{"NovaLand/../escaped.txt","NovaLand/../../escaped.txt","NovaLand/C:/escaped","NovaLand/thing:stream","NovaLand/NUL.txt","NovaLand/folder./bad","NovaLand/foo /bad","other/file","NovaLand/..\\escaped.txt"})
 Reject(()=>UpdatePackage.Extract(Zip(bad),Path.Combine(root,Guid.NewGuid().ToString()),version),"unsafe archive: "+bad);
Reject(()=>UpdatePackage.Extract(Zip("NovaLand/file",z=>z.CreateEntry("NovaLand/FILE")),Path.Combine(root,"duplicate"),version),"case-insensitive duplicate rejected");
Reject(()=>UpdatePackage.Extract(Zip("NovaLand/readme.txt"),Path.Combine(root,"missing"),version),"missing app rejected");
var published = args.Length > 0 ? args[0] : "../publish-novaland-v100";
var publishedVersion = new Version(System.Reflection.AssemblyName.GetAssemblyName(Path.Combine(published,"NovaLand.dll")).Version!.ToString(3));
var valid=Path.Combine(root,"valid.zip");
using(var z=ZipFile.Open(valid,ZipArchiveMode.Create)) foreach(var f in Directory.EnumerateFiles(published))z.CreateEntryFromFile(f,"NovaLand/"+Path.GetFileName(f));
UpdatePackage.Extract(valid,Path.Combine(root,"valid"),publishedVersion);Assert(File.Exists(Path.Combine(root,"valid","NovaLand.exe")),"real published assembly extracted and version validated");
Reject(()=>UpdatePackage.Extract(valid,Path.Combine(root,"mismatch"),new Version(publishedVersion.Major+1,0,0)),"assembly-version mismatch rejected");
var bytes = await File.ReadAllBytesAsync(valid);
var body = Json("v"+publishedVersion,digest:"sha256:"+Convert.ToHexString(SHA256.HashData(bytes)),size:bytes.Length);
using(var updates = new UpdateService(new HttpClient(new FakeHttp(body,bytes)))) {
 Assert(await updates.CheckAndDownloadAsync(_=>{}),"HTTP metadata -> download -> hash -> extraction pipeline");
 Assert(updates.ReadyVersion==publishedVersion&&File.Exists(updates.ReadyExecutable),"verified application ready without executing it");
 Assert(await updates.CheckAndDownloadAsync(_=>{}),"ready package reused");
}
using(var updates = new UpdateService(new HttpClient(new FakeHttp("",bytes, HttpStatusCode.NotFound))))
 Assert(!await updates.CheckAndDownloadAsync(_=>{}),"repository without releases leaves app running");
bytes[bytes.Length/2]^=0xff;
using(var updates = new UpdateService(new HttpClient(new FakeHttp(body,bytes)))) {
 try {await updates.CheckAndDownloadAsync(_=>{});throw new Exception("bad HTTP package accepted");}
 catch(InvalidDataException){Assert(updates.ReadyExecutable is null,"tampered download never marked ready");}
}
Directory.Delete(root,true);Console.WriteLine($"{count} checks passed");
sealed class FakeHttp(string metadata, byte[] package, HttpStatusCode status=HttpStatusCode.OK):HttpMessageHandler {
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) {
  bool api=request.RequestUri!.Host=="api.github.com";
  return Task.FromResult(new HttpResponseMessage(api?status:HttpStatusCode.OK){Content=api?new StringContent(metadata):new ByteArrayContent(package)});
 }
}
