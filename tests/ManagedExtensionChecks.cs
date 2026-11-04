using System.Text.Json;
using NovaIsland.Services;
internal static class ManagedExtensionChecks
{
    public static void Run(Action<bool,string> check)
    {
        var root=Path.Combine(Path.GetTempPath(),"novaland-managed-"+Guid.NewGuid().ToString("N"));
        var source=Path.Combine(root,"package"); var target=Path.Combine(root,"stable");
        Directory.CreateDirectory(source);
        string[] names=["manifest.json","background.js","core.js","media.js","popup.html","popup.js","managed.js","icon16.png","icon32.png","icon48.png","icon128.png"];
        foreach(var name in names) File.WriteAllText(Path.Combine(source,name),"test");
        File.WriteAllText(Path.Combine(source,"manifest.json"),"{\"version\":\"0.10.0\"}");
        try
        {
            var first=ManagedBrowserExtension.Prepare(source,target,new string('a',64));
            check(first.Version=="0.10.0" && Directory.Exists(first.Folder),"managed extension installed");
            var id=first.InstallId;
            var configured=File.ReadAllText(Path.Combine(target,"managed.js"));
            check(configured.Contains(id) && configured.Contains(new string('a',64)),"pairing configured only in local managed copy");
            check(File.ReadAllText(Path.Combine(source,"managed.js"))=="test","package never receives user's pairing code");
            var repeat=ManagedBrowserExtension.Prepare(source,target,new string('a',64));
            check(repeat.InstallId==id && repeat.Folder==first.Folder,"stable identity and path preserved");
            File.WriteAllText(Path.Combine(source,"manifest.json"),"{\"version\":\"0.11.0\"}");
            File.WriteAllText(Path.Combine(source,"background.js"),"next worker");
            var next=ManagedBrowserExtension.Prepare(source,target,new string('a',64));
            check(next.InstallId==id && File.ReadAllText(Path.Combine(target,"background.js"))=="next worker","new version updates same folder");
            File.WriteAllText(Path.Combine(source,"manifest.json"),"{\"version\":\"0.9.0\"}");
            var old=ManagedBrowserExtension.Prepare(source,target,new string('a',64));
            check(old.Version=="0.11.0" && File.ReadAllText(Path.Combine(target,"background.js"))=="next worker","old application cannot downgrade extension");
            var unrelated=Path.Combine(root,"unrelated");Directory.CreateDirectory(unrelated);
            File.WriteAllText(Path.Combine(unrelated,"personal.txt"),"keep");
            bool refused=false;try{ManagedBrowserExtension.Prepare(source,unrelated,new string('a',64));}catch(IOException){refused=true;}
            check(refused && File.ReadAllText(Path.Combine(unrelated,"personal.txt"))=="keep","unrelated user folder is preserved");
            File.WriteAllText(Path.Combine(source,"manifest.json"),"{\"version\":\"0.12.0\"}");
            File.Delete(Path.Combine(source,"core.js"));refused=false;
            try{ManagedBrowserExtension.Prepare(source,target,new string('a',64));}catch(FileNotFoundException){refused=true;}
            using var saved=JsonDocument.Parse(File.ReadAllText(Path.Combine(target,"manifest.json")));
            check(refused && saved.RootElement.GetProperty("version").GetString()=="0.11.0","incomplete new package cannot damage installed copy");
        }
        finally {Directory.Delete(root,true);}
    }
}
