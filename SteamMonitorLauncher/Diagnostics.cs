using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
public static class Diagnostics {
    static readonly object Gate=new object();
    public static readonly string Folder=Path.Combine(AppPaths.Data,"logs");
    public static void Write(string text){try{lock(Gate){Directory.CreateDirectory(Folder);string file=Path.Combine(Folder,"watcher.log");if(File.Exists(file)&&new FileInfo(file).Length>1048576)File.Move(file,Path.Combine(Folder,"watcher-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+".log"));File.AppendAllText(file,DateTime.Now.ToString("s")+" "+text+Environment.NewLine);}}catch{}}
    static int MarshalSize(){return System.Runtime.InteropServices.Marshal.SizeOf(typeof(Gamepad.State));}
    public static int StartupCheck(){try{
        foreach(string name in new[]{"Launcher.ico","LauncherPaused.ico"})foreach(int size in new[]{16,32,48,256}){
            using(var a=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))using(var icon=new Icon(a,new Size(size,size)))using(var canvas=new Bitmap(size,size))using(var g=Graphics.FromImage(canvas)){g.DrawIcon(icon,new Rectangle(0,0,size,size));}
        }
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("LauncherHeader.png"))using(var png=Image.FromStream(stream)){if(png.Width<52)throw new InvalidDataException("Header PNG too small");}
        var serializer=new System.Xml.Serialization.XmlSerializer(typeof(Settings));
        Settings legacy;
        using(var reader=new StringReader("<Settings><Steam>test</Steam></Settings>"))legacy=(Settings)serializer.Deserialize(reader);
        if(legacy.ExcludedGames==null||legacy.ExcludedGames.Count!=0)throw new InvalidDataException("Legacy exclusion settings failed");
        legacy.ExcludedGames.Add(new GameExclusion{Id="123",Name="Test game",Folder=@"C:\Games\Test"});
        string saved;using(var writer=new StringWriter()){serializer.Serialize(writer,legacy);saved=writer.ToString();}
        using(var reader=new StringReader(saved))legacy=(Settings)serializer.Deserialize(reader);
        if(!Exclusions.Contains(legacy.ExcludedGames,"123",@"D:\MovedGame","Renamed")||Exclusions.Contains(legacy.ExcludedGames,"456",@"C:\Games\Test","Test game")||!Exclusions.Contains(legacy.ExcludedGames,"","","Test game"))throw new InvalidDataException("Exclusion persistence/identity check failed");
        legacy.MonitorKey="test-monitor";
        legacy.CustomGames.Add(new CustomGame{Id="custom:test",Name="Test custom game",Executable=@"C:\Games\Test\game.exe",Arguments="-windowed"});
        legacy.Profiles.Add(new Profile{Id="custom:test",Override=true,MonitorKey="test-monitor",UseLayout=true,X=-50,Y=5000,Width=9000,Height=600});
        using(var writer=new StringWriter()){serializer.Serialize(writer,legacy);saved=writer.ToString();}
        using(var reader=new StringReader(saved))legacy=(Settings)serializer.Deserialize(reader);
        if(legacy.CustomGames.Count!=1||!legacy.Profiles[0].Override||legacy.MonitorKey!="test-monitor")throw new InvalidDataException("Profile/custom-game backup roundtrip failed");
        legacy.FallbackEnabled=true;legacy.FallbackKey="fallback-test";
        legacy.GameOptions.Add(new GameOptions{Id="custom:test",Favorite=true,DelaySeconds=45,StopAfterPlacement=true,LaunchMode="Shortcut",LaunchPath=@"C:\Games\Test.lnk",Artwork=@"artwork\test.png",LastPlayed=new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc)});
        using(var writer=new StringWriter()){serializer.Serialize(writer,legacy);saved=writer.ToString();}
        using(var reader=new StringReader(saved))legacy=(Settings)serializer.Deserialize(reader);
        if(!legacy.FallbackEnabled||legacy.FallbackKey!="fallback-test"||legacy.GameOptions.Count!=1||!legacy.GameOptions[0].Favorite||legacy.GameOptions[0].DelaySeconds!=45||!legacy.GameOptions[0].StopAfterPlacement||legacy.GameOptions[0].LaunchMode!="Shortcut"||legacy.GameOptions[0].LastPlayed.Year!=2026)throw new InvalidDataException("Library options/fallback settings roundtrip failed");
        if(MarshalSize()!=16)throw new InvalidDataException("XInput structure size is invalid");
        var release=ReleaseClient.Parse("{\"Version\":\"2.0.0\",\"PackageUrl\":\"https://example.invalid/update.zip\",\"Sha256\":\""+new string('a',64)+"\"}","https://example.invalid/latest.json");
        if(release.Version!="2.0.0")throw new InvalidDataException("Release feed parsing failed");
        bool invalidRejected=false;try{ReleaseClient.Parse("{\"Version\":\"2.0.0\",\"PackageUrl\":\"http://example.invalid/update.zip\",\"Sha256\":\""+new string('a',64)+"\"}","");}catch(InvalidDataException){invalidRejected=true;}if(!invalidRejected)throw new InvalidDataException("Insecure update URL was accepted");
        if(!Watcher.IsSteamUnlockedLauncher(@"C:\Apps\STEAMUNLOCKED-LAUNCHER.EXE")||Watcher.IsSteamUnlockedLauncher(@"C:\Apps\other-steamunlocked-launcher.exe"))throw new InvalidDataException("SteamUnlocked launcher filename matching failed");
        var su=Watcher.SteamUnlockedGame(@"C:\Games\One\game.exe");var suAgain=Watcher.SteamUnlockedGame(@"c:\games\one\GAME.exe");var suOther=Watcher.SteamUnlockedGame(@"C:\Games\Two\game.exe");
        if(su.Id!=suAgain.Id||su.Id==suOther.Id||su.MatchFolder||su.Source!="SteamUnlocked")throw new InvalidDataException("SteamUnlocked game identity failed");
        var ancestor=new ProcessRecord{Id=1,Born=new DateTime(2026,1,1)};var child=new ProcessRecord{Id=2,Born=new DateTime(2026,1,2)};
        if(!Watcher.CanInherit(child,ancestor,null)||Watcher.CanInherit(child,ancestor,new ProcessRecord{Id=1,Born=new DateTime(2026,1,3)})||Watcher.CanInherit(ancestor,ancestor,ancestor))throw new InvalidDataException("Launcher ancestry identity validation failed");

        var json=new System.Web.Script.Serialization.JavaScriptSerializer();
        var imported=SteamUnlockedLibrary.Parse(json.Serialize(new[]{new {id="fixture",name="Example Game",folder_path=@"D:\Saved Games\Downloads\Example",catalog_id=42,exe_path=(string)null}}),false);
        if(imported.Count!=1||imported[0].Executable!=""||!imported[0].MatchFolder||imported[0].Id!="steamunlocked:catalog:42")throw new InvalidDataException("Custom library import failed");
        var downloads=SteamUnlockedLibrary.Parse(json.Serialize(new[]{
            new {id="one",game_id=43,game_title="Example Two Free Download (v1.0)",save_dir=@"C:\SteamUnlocked",status="dismissed",extracted_folder="ExampleTwo",exe_path=@"ExampleTwo\Binaries\game.exe"},
            new {id="two",game_id=44,game_title="Pending",save_dir=@"C:\SteamUnlocked",status="paused",extracted_folder="Pending",exe_path=""},
            new {id="three",game_id=45,game_title="Unsafe",save_dir=@"C:\SteamUnlocked",status="completed",extracted_folder=@"..\Outside",exe_path=""},
            new {id="four",game_id=43,game_title="Duplicate",save_dir=@"C:\SteamUnlocked",status="completed",extracted_folder="ExampleTwo",exe_path=""}
        }),true);
        if(downloads.Count!=1||downloads[0].Name!="Example Two"||downloads[0].Executable!=@"C:\SteamUnlocked\ExampleTwo\Binaries\game.exe")throw new InvalidDataException("Download library import, filtering or executable resolution failed");
        if(!SteamUnlockedLibrary.Within(@"D:\Saved Games\Downloads\Example\Binaries\game.exe",imported[0].Folder)||SteamUnlockedLibrary.Within(@"D:\Saved Games\Downloads\ExampleOther\game.exe",imported[0].Folder))throw new InvalidDataException("Library folder boundary failed");
        var bounds=Launcher.SavedBounds(legacy.Profiles[0],new Rectangle(-1920,0,1920,1080));
        if(bounds!=new Rectangle(-1920,480,1920,600))throw new InvalidDataException("Saved layout bounds clamp failed");
        using(var form=new Launcher(false,true)){form.CreateControl();form.PerformLayout();}
        Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"startup-check.log"),"PASS: assembly, both embedded icons at 16/32/48/256px, standalone header PNG, exclusion persistence/identity, profile/custom-game serialization, saved-layout bounds, and settings form construction/layout. SteamUnlocked JSON parsing, download filtering, path boundaries and deduplication passed. Game positioning not tested.");return 0;
    }catch(Exception ex){try{Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"startup-check.log"),ex.ToString());}catch{}return 1;}}
}
