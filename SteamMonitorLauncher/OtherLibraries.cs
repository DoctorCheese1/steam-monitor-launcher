using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Microsoft.Win32;

// Read-only local discovery. No credentials, downloads or store account access.
public static class OtherLibraries {
    public static bool SafeFolder(string folder) {
        try {
            if(String.IsNullOrWhiteSpace(folder)||!Path.IsPathRooted(folder)||!Directory.Exists(folder))return false;
            string full=Path.GetFullPath(folder).TrimEnd('\\');
            if(full.Equals(Path.GetPathRoot(full).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))return false;
            foreach(var kind in new[]{Environment.SpecialFolder.Windows,Environment.SpecialFolder.ProgramFiles,Environment.SpecialFolder.ProgramFilesX86,Environment.SpecialFolder.UserProfile,Environment.SpecialFolder.CommonApplicationData,Environment.SpecialFolder.LocalApplicationData}){
                string shared=Environment.GetFolderPath(kind).TrimEnd('\\');
                if(shared.Length>0&&full.Equals(shared,StringComparison.OrdinalIgnoreCase))return false;
            }
            string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\');
            return !full.StartsWith(windows+"\\",StringComparison.OrdinalIgnoreCase);
        }catch{return false;}
    }
    static string Field(IDictionary<string,object> data,string name){object v;return data.TryGetValue(name,out v)&&v is string?(string)v:"";}
    static void Add(List<Game> games,Game game){
        if(String.IsNullOrWhiteSpace(game.Name)||!SafeFolder(game.Folder))return;
        game.Folder=Path.GetFullPath(game.Folder).TrimEnd('\\');
        if(!games.Any(g=>g.Id==game.Id||String.Equals(g.Folder.TrimEnd('\\'),game.Folder,StringComparison.OrdinalIgnoreCase)))games.Add(game);
    }
    public static void Scan(List<Game> games){
        try{ScanEpic(games);}catch(Exception ex){Diagnostics.Write("Epic discovery: "+ex.Message);}
        foreach(var hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser})foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64}){
            try{using(var registry=RegistryKey.OpenBaseKey(hive,view)){
                ScanGog(registry,games);ScanUbisoft(registry,games);
            }}catch(Exception ex){Diagnostics.Write("Store registry discovery: "+ex.Message);}
        }
    }
    static void ScanEpic(List<Game> games){
        string directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),@"Epic\EpicGamesLauncher\Data\Manifests");
        if(!Directory.Exists(directory))return;
        foreach(string file in Directory.GetFiles(directory,"*.item"))try{
            if(new FileInfo(file).Length>4*1024*1024)continue;
            var json=new JavaScriptSerializer{MaxJsonLength=4*1024*1024};
            var data=json.DeserializeObject(File.ReadAllText(file)) as IDictionary<string,object>;if(data==null)continue;
            object incomplete;if(data.TryGetValue("bIsIncompleteInstall",out incomplete)&&incomplete is bool&&(bool)incomplete)continue;
            string id=Field(data,"AppName"),folder=Field(data,"InstallLocation"),name=Field(data,"DisplayName");
            if(id.Length==0||!SafeFolder(folder))continue;
            // Exclude launcher components and non-executable content manifests.
            string executable=Field(data,"LaunchExecutable");if(executable.Length==0)continue;
            string launch=Path.GetFullPath(Path.Combine(folder,executable));
            if(!launch.StartsWith(Path.GetFullPath(folder).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)||!File.Exists(launch))continue;
            string ns=Field(data,"CatalogNamespace"),item=Field(data,"CatalogItemId");
            string app=ns.Length>0&&item.Length>0?ns+":"+item+":"+id:id;
            Add(games,new Game{Id="epic:"+id,Name=name,Folder=folder,Source="Epic",LaunchUri="com.epicgames.launcher://apps/"+Uri.EscapeDataString(app)+"?action=launch&silent=true"});
        }catch(Exception ex){Diagnostics.Write("Epic manifest skipped: "+Path.GetFileName(file)+" | "+ex.Message);}
    }
    static string Read(RegistryKey key,string name){return Convert.ToString(key.GetValue(name))??"";}
    static void ScanGog(RegistryKey registry,List<Game> games){
        using(var root=registry.OpenSubKey(@"SOFTWARE\GOG.com\Games")){
            if(root==null)return;foreach(string id in root.GetSubKeyNames())try{using(var key=root.OpenSubKey(id)){
                if(key==null)continue;string folder=Read(key,"path"),exe=Read(key,"exe");
                if(!SafeFolder(folder))continue;
                if(exe.Length>0&&!Path.IsPathRooted(exe))exe=Path.Combine(folder,exe);
                if(!File.Exists(exe)||!exe.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||!Path.GetFullPath(exe).StartsWith(Path.GetFullPath(folder).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase))exe="";
                Add(games,new Game{Id="gog:"+id,Name=Read(key,"gameName"),Folder=folder,Source="GOG",Executable=exe});
            }}catch(Exception ex){Diagnostics.Write("GOG entry skipped: "+ex.Message);}
        }
    }
    static void ScanUbisoft(RegistryKey registry,List<Game> games){
        using(var root=registry.OpenSubKey(@"SOFTWARE\Ubisoft\Launcher\Installs")){
            if(root==null)return;foreach(string id in root.GetSubKeyNames())try{using(var key=root.OpenSubKey(id)){
                if(key==null||!id.All(Char.IsDigit))continue;string folder=Read(key,"InstallDir");if(!SafeFolder(folder))continue;
                string name=Path.GetFileName(folder.TrimEnd('\\','/'));
                Add(games,new Game{Id="ubisoft:"+id,Name=name,Folder=folder,Source="Ubisoft",LaunchUri="uplay://launch/"+id+"/0"});
            }}catch(Exception ex){Diagnostics.Write("Ubisoft entry skipped: "+ex.Message);}
        }
    }
}
