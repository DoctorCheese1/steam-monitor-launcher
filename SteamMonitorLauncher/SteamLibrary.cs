using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32;

public class Game { public string Id, Name, Folder;public string Executable="",Arguments="",Source="Steam",LaunchUri=""; public bool MatchFolder=true; public override string ToString(){return Name;} }
public class SteamLibrary {
    public string Steam="";
    public List<Game> games=new List<Game>();
    public string Message="";
    void Report(string message){Message=message;}
    static string Value(string text,string key){var m=Regex.Match(text,"\""+Regex.Escape(key)+"\"\\s*\"((?:\\\\.|[^\"\\\\])*)\"",RegexOptions.IgnoreCase);return m.Success?m.Groups[1].Value.Replace("\\\\","\\").Replace("\\\"","\""):"";}
    string FindSteam(){if(File.Exists(Steam)&&String.Equals(Path.GetFileName(Steam),"steam.exe",StringComparison.OrdinalIgnoreCase))return Steam;try{using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")){if(k!=null){string p=Convert.ToString(k.GetValue("SteamPath"));string exe=Path.Combine(p,"steam.exe");if(File.Exists(exe))return exe;}}}catch{}string fallback=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),@"Steam\steam.exe");return File.Exists(fallback)?fallback:"";}
    public void Scan(){
        Steam=FindSteam();games.Clear();int skipped=0;
        if(Steam.Length==0){OtherLibraries.Scan(games);Report(games.Count+" games found. Use Tools to add games from other launchers.");return;}
        var roots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);roots.Add(Path.GetDirectoryName(Steam));
        string vdf=Path.Combine(Path.GetDirectoryName(Steam),@"steamapps\libraryfolders.vdf");
        try{if(File.Exists(vdf)){string text=File.ReadAllText(vdf);foreach(Match m in Regex.Matches(text,"\"(?:path|[0-9]+)\"\\s*\"((?:\\\\.|[^\"\\\\])*)\"")){string p=m.Groups[1].Value.Replace("\\\\","\\");if(Path.IsPathRooted(p))roots.Add(p);}}}catch{skipped++;}
        var ids=new HashSet<string>();
        foreach(string root in roots){try{string apps=Path.Combine(root,"steamapps");if(!Directory.Exists(apps))continue;foreach(string file in Directory.GetFiles(apps,"appmanifest_*.acf")){try{string t=File.ReadAllText(file),id=Value(t,"appid"),name=Value(t,"name"),dir=Value(t,"installdir");if(!Regex.IsMatch(id,"^[0-9]+$")||name.Length==0||dir.Length==0)continue;string common=Path.GetFullPath(Path.Combine(apps,"common"))+Path.DirectorySeparatorChar;string folder=Path.GetFullPath(Path.Combine(common,dir));if(!folder.StartsWith(common,StringComparison.OrdinalIgnoreCase)||!Directory.Exists(folder))continue;if(ids.Add(id))games.Add(new Game{Id=id,Name=name,Folder=folder});}catch{skipped++;}}}catch{skipped++;}}
        OtherLibraries.Scan(games);
        games=games.OrderBy(g=>g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();Report(games.Count+" installed titles found. Search above, then choose a game and display."+(skipped>0?" Some library files could not be read.":""));
    }
}
