using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

// Read only the local game-library metadata. Unknown fields are ignored.
public static class SteamUnlockedLibrary {
    static readonly Dictionary<string,List<Game>> previous=new Dictionary<string,List<Game>>(StringComparer.OrdinalIgnoreCase);
    static string Text(IDictionary<string,object> row,string key){object value;return row.TryGetValue(key,out value)&&value!=null?Convert.ToString(value):"";}
    static string Full(string value){if(String.IsNullOrWhiteSpace(value)||!Path.IsPathRooted(value))return "";try{return Path.GetFullPath(value).TrimEnd('\\','/');}catch{return "";}}
    static bool FolderAllowed(string folder){
        if(folder.Length==0||folder.Equals(Path.GetPathRoot(folder).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase))return false;
        foreach(var kind in new[]{Environment.SpecialFolder.Windows,Environment.SpecialFolder.ProgramFiles,Environment.SpecialFolder.ProgramFilesX86,Environment.SpecialFolder.UserProfile,Environment.SpecialFolder.ApplicationData,Environment.SpecialFolder.LocalApplicationData}){
            string shared=Environment.GetFolderPath(kind).TrimEnd('\\','/');if(shared.Length>0&&(folder.Equals(shared,StringComparison.OrdinalIgnoreCase)||(kind==Environment.SpecialFolder.Windows&&Within(folder,shared))))return false;
        }return true;
    }
    public static bool Within(string path,string folder){return path.StartsWith(folder.TrimEnd('\\','/')+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
    static string Relative(string value,string root){try{return Path.IsPathRooted(value)?Full(value):Full(Path.Combine(root,value));}catch{return "";}}
    public static List<Game> Parse(string json,bool downloads){
        var rows=new JavaScriptSerializer{MaxJsonLength=8*1024*1024}.DeserializeObject(json) as object[];
        if(rows==null)throw new InvalidDataException("Expected a game library array.");
        var result=new List<Game>();
        foreach(object item in rows){var row=item as IDictionary<string,object>;if(row==null)continue;
            string name=Text(row,downloads?"game_title":"name");string folder,baseFolder="";
            if(downloads){
                string extracted=Text(row,"extracted_folder"),status=Text(row,"status");
                if(extracted.Length==0||(!String.Equals(status,"completed",StringComparison.OrdinalIgnoreCase)&&!String.Equals(status,"dismissed",StringComparison.OrdinalIgnoreCase)))continue;
                baseFolder=Full(Text(row,"save_dir"));if(baseFolder.Length==0)continue;
                folder=Relative(extracted,baseFolder);if(!Path.IsPathRooted(extracted)&&!Within(folder,baseFolder))continue;
                name=Regex.Replace(name,@"\s+Free Download\b.*$","",RegexOptions.IgnoreCase).Trim();
            }else folder=Full(Text(row,"folder_path"));
            if(!FolderAllowed(folder)||String.IsNullOrWhiteSpace(name))continue;
            string exe="",raw=Text(row,"exe_path");
            if(raw.Length>0){
                string first=Relative(raw,downloads?baseFolder:folder),second=Relative(raw,folder);
                foreach(string candidate in new[]{first,second})if(candidate.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)&&Within(candidate,folder)){exe=candidate;break;}
            }
            string key=Text(row,downloads?"game_id":"catalog_id");long number;
            string id=Int64.TryParse(key,out number)&&number>0?"steamunlocked:catalog:"+number:"steamunlocked:entry:"+Text(row,"id");
            if(id=="steamunlocked:entry:")id="steamunlocked:folder:"+folder.ToUpperInvariant();
            var game=new Game{Id=id,Name=name,Folder=folder,Executable=exe,Source="SteamUnlocked",MatchFolder=true};
            // Folder identity also deduplicates a custom entry and a download record.
            if(!result.Any(g=>g.Id==id||String.Equals(g.Folder,folder,StringComparison.OrdinalIgnoreCase)))result.Add(game);
        }return result;
    }
    static List<Game> Read(string path,bool downloads){
        if(!File.Exists(path)){previous.Remove(path);return new List<Game>();}
        try{
            string json;using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
                if(file.Length>8*1024*1024)throw new InvalidDataException("Library exceeds 8 MB.");using(var reader=new StreamReader(file)){json=reader.ReadToEnd();if(json.Length>8*1024*1024)throw new InvalidDataException("Library exceeds 8 MB.");}
            }
            var games=Parse(json,downloads);previous[path]=games;return games;
        }catch(Exception ex){Diagnostics.Write("SteamUnlocked library read failed ("+Path.GetFileName(path)+"): "+ex.GetType().Name);List<Game> cached;return previous.TryGetValue(path,out cached)?cached:new List<Game>();}
    }
    public static void Scan(List<Game> games){
        // Roaming is the observed storage location. Local supports alternate installations.
        foreach(var location in new[]{Environment.SpecialFolder.ApplicationData,Environment.SpecialFolder.LocalApplicationData}){
            string root=Path.Combine(Environment.GetFolderPath(location),"com.uploadhaven.launcher");
            foreach(var game in Read(Path.Combine(root,"custom_games.json"),false).Concat(Read(Path.Combine(root,"downloads.json"),true))){
                var match=games.FirstOrDefault(g=>g.Id==game.Id||String.Equals(g.Folder,game.Folder,StringComparison.OrdinalIgnoreCase));
                if(match==null)games.Add(game);else if(match.Source=="SteamUnlocked"&&String.IsNullOrEmpty(match.Executable)&&!String.IsNullOrEmpty(game.Executable))match.Executable=game.Executable;
            }
        }
    }
}
