using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

public class ProcessRecord {
    public uint Id; public DateTime Born, Seen; public string Path="",LauncherSource=""; public Game Game;
}
public class Candidate {
    public IntPtr Handle; public uint Pid; public DateTime Born; public string Title, Game, GameId, GameFolder;
}
public class WatchResult {
    public List<Candidate> Windows=new List<Candidate>();
    public int Revision;public List<Game> Games; public string Steam, Message;
}
// Owned only by the one background worker; no UI or settings objects are shared.
public class Watcher {
    SteamLibrary library=new SteamLibrary();
    string customSignature="";
    Dictionary<string,Game> discovered=new Dictionary<string,Game>(StringComparer.OrdinalIgnoreCase);
    public static bool IsSteamUnlockedLauncher(string path){return String.Equals(System.IO.Path.GetFileName(path),"steamunlocked-launcher.exe",StringComparison.OrdinalIgnoreCase);}
    public static Game SteamUnlockedGame(string path){
        string full=System.IO.Path.GetFullPath(path);string id;
        using(var hash=System.Security.Cryptography.SHA256.Create())id=BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(full.ToUpperInvariant()))).Replace("-","").ToLowerInvariant();
        return new Game{Id="steamunlocked:"+id,Name=System.IO.Path.GetFileNameWithoutExtension(full),Folder=System.IO.Path.GetDirectoryName(full),Executable=full,Source="SteamUnlocked",MatchFolder=false};
    }
    public static bool CanInherit(ProcessRecord child,ProcessRecord ancestor,ProcessRecord liveParent){return child.Id!=ancestor.Id&&child.Born>=ancestor.Born&&(liveParent==null||liveParent.Born==ancestor.Born);}

    DateTime lastScan=DateTime.MinValue,lastDiagnostic=DateTime.MinValue;
    Dictionary<uint,ProcessRecord> cache=new Dictionary<uint,ProcessRecord>();
    Dictionary<uint,ProcessRecord> family=new Dictionary<uint,ProcessRecord>();
    public WatchResult Poll(string steam,bool refresh,List<Game> custom){
        DateTime now=DateTime.UtcNow;string signature=String.Join("|",custom.Select(g=>g.Id+g.Executable+g.Name+g.Arguments+g.Folder+g.MatchFolder));if(signature!=customSignature){cache.Clear();family.Clear();customSignature=signature;}
        bool rescan=refresh||(now-lastScan).TotalSeconds>=60;
        if(rescan){library.Steam=steam;library.Scan();lastScan=now;cache.Clear();}
        var catalog=library.games.Concat(custom).Concat(discovered.Values.Where(d=>!custom.Any(g=>String.Equals(g.Executable,d.Executable,StringComparison.OrdinalIgnoreCase)))).OrderBy(g=>g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
        var result=new WatchResult{Games=catalog,Steam=library.Steam,Message=library.Message};
        var parents=Native.Parents();var alive=new Dictionary<uint,ProcessRecord>();
        foreach(var process in Process.GetProcesses())using(process){try{
            uint pid=(uint)process.Id;DateTime born;string imagePath;
            if(!Native.ProcessInfo(pid,out imagePath,out born))continue;ProcessRecord record;
            string processName=System.IO.Path.GetFileName(imagePath).ToLowerInvariant();if(new[]{"steam.exe","steamwebhelper.exe","epicgameslauncher.exe","epicwebhelper.exe","galaxyclient.exe","galaxyclient helper.exe","ubisoftconnect.exe","upc.exe","uplaywebcore.exe","eadesktop.exe","ealauncher.exe","origin.exe","battle.net.exe","agent.exe","riotclientservices.exe","riotclientux.exe","explorer.exe","chrome.exe","msedge.exe","firefox.exe"}.Contains(processName))continue;
            if(!cache.TryGetValue(pid,out record)||record.Born!=born){
                record=new ProcessRecord{Id=pid,Born=born};
                record.Path=imagePath;
                cache[pid]=record;
            }
            if(record.Path.Length==0)record.Path=imagePath;record.Seen=now;alive[pid]=record;
            ProcessRecord old;
            if(family.TryGetValue(pid,out old)&&old.Born!=born)family.Remove(pid);
            if(IsSteamUnlockedLauncher(record.Path)){record.Game=null;record.LauncherSource="SteamUnlocked";family[pid]=record;continue;}
            if(record.Game==null)record.Game=catalog.FirstOrDefault(g=>String.Equals(g.Executable,record.Path,StringComparison.OrdinalIgnoreCase));
            if(record.Game==null)record.Game=catalog.Where(g=>g.MatchFolder&&!String.IsNullOrEmpty(g.Folder)).OrderByDescending(g=>g.Folder.Length).FirstOrDefault(g=>record.Path.StartsWith(g.Folder.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase));
            if(record.Game==null){string exe=System.IO.Path.GetFileName(record.Path);
                if(String.Equals(exe,"ArkAscended.exe",StringComparison.OrdinalIgnoreCase))record.Game=new Game{Id="",Name="ARK: Survival Ascended",Folder=""};
                else if(String.Equals(exe,"ShooterGame.exe",StringComparison.OrdinalIgnoreCase))record.Game=new Game{Id="",Name="ARK: Survival Evolved",Folder=""};
            }
            if(record.Game!=null)family[pid]=record;
            else if(family.TryGetValue(pid,out old)){record.Game=old.Game;record.LauncherSource=old.LauncherSource;family[pid]=record;}
        }catch{}}
        foreach(var pid in cache.Keys.ToArray())if(!alive.ContainsKey(pid))cache.Remove(pid);
        foreach(var pid in family.Keys.ToArray())if(!alive.ContainsKey(pid)&&(now-family[pid].Seen).TotalSeconds>60)family.Remove(pid);
        bool changed;
        do{changed=false;foreach(var record in alive.Values){
            if(record.Game!=null||record.LauncherSource.Length>0)continue;uint parent;ProcessRecord ancestor,current;
            if(!parents.TryGetValue(record.Id,out parent)||!family.TryGetValue(parent,out ancestor))continue;
            alive.TryGetValue(parent,out current);if(!CanInherit(record,ancestor,current))continue;
            // Never capture Steam itself, Explorer or a browser opened for a game's help page.
            string name=System.IO.Path.GetFileName(record.Path).ToLowerInvariant();
            if(new[]{"steam.exe","steamwebhelper.exe","epicgameslauncher.exe","epicwebhelper.exe","galaxyclient.exe","galaxyclient helper.exe","ubisoftconnect.exe","upc.exe","uplaywebcore.exe","eadesktop.exe","ealauncher.exe","origin.exe","battle.net.exe","agent.exe","riotclientservices.exe","riotclientux.exe","explorer.exe","chrome.exe","msedge.exe","firefox.exe"}.Contains(name))continue;
            record.Game=ancestor.Game;record.LauncherSource=ancestor.LauncherSource;family[record.Id]=record;changed=true;
        }}while(changed);
        foreach(var window in Native.Windows()){
            uint pid;Native.GetWindowThreadProcessId(window.Handle,out pid);ProcessRecord record;
            if(!alive.TryGetValue(pid,out record)||IsSteamUnlockedLauncher(record.Path))continue;
            if(record.Game==null&&record.LauncherSource=="SteamUnlocked"){
                Game game;if(!discovered.TryGetValue(record.Path,out game)){game=SteamUnlockedGame(record.Path);discovered[record.Path]=game;}record.Game=game;family[pid]=record;
            }
            if(record.Game!=null)
                result.Windows.Add(new Candidate{Handle=window.Handle,Pid=pid,Born=record.Born,Title=window.Title,Game=record.Game.Name,GameId=record.Game.Id,GameFolder=record.Game.Folder});
        }
        result.Games=catalog.Concat(discovered.Values.Where(d=>!catalog.Any(g=>g.Id==d.Id||String.Equals(g.Executable,d.Executable,StringComparison.OrdinalIgnoreCase)))).OrderBy(g=>g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
        if((now-lastDiagnostic).TotalSeconds>=30){lastDiagnostic=now;
            Diagnostics.Write("Scan: "+alive.Count+" readable processes; "+result.Games.Count+" titles; "+result.Windows.Count+" game windows.");
            foreach(var window in result.Windows)Diagnostics.Write("Detected: "+window.Game+" | PID "+window.Pid+" | HWND "+window.Handle+" | "+window.Title);
        }
        return result;
    }
}
