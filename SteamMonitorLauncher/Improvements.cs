using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public static class ExecutableDiscovery {
    public static bool IsCandidate(string path){
        string name=Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        return path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)&&!new[]{"unins","setup","install","vcredist","dxsetup","crashreport","crashpad","unitycrash","ue4prereq","ueprereq","vc_redist","easyanticheat","beservice","dotnet","reporter"}.Any(n=>name.StartsWith(n));
    }
    public static List<string> Scan(string root,Func<bool> canceled){
        var result=new List<string>();if(!OtherLibraries.SafeFolder(root))return result;
        var pending=new Queue<string>();pending.Enqueue(root);int folders=0;
        while(pending.Count>0&&folders++<2000&&result.Count<200&&!canceled()){
            var folder=pending.Dequeue();try{
                if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)continue;
                foreach(string file in Directory.EnumerateFiles(folder,"*.exe")){if(canceled())break;if(IsCandidate(file))result.Add(file);if(result.Count>=200)break;}
                foreach(string child in Directory.EnumerateDirectories(folder)){if(canceled()||pending.Count>=2000)break;if((File.GetAttributes(child)&FileAttributes.ReparsePoint)==0)pending.Enqueue(child);}
            }catch(UnauthorizedAccessException){}catch(IOException){}
        }
        return result.OrderByDescending(x=>x.IndexOf("shipping",StringComparison.OrdinalIgnoreCase)>=0).ThenBy(x=>x,StringComparer.OrdinalIgnoreCase).ToList();
    }
}
public partial class Launcher {
    Dictionary<string,DateTime> launchWaiting=new Dictionary<string,DateTime>();
    string GameStatus(Game game){
        if(IsExcluded(game))return "Excluded";
        if(SessionPaused(game.Id))return "Paused for this game session";
        if(manualHolds.Values.Any(h=>h.GameId==game.Id&&h.Alive()))return "Manual placement - auto-move held";
        if(!settings.Enabled)return "Auto-move paused";
        var running=lastCandidates.Where(c=>c.GameId==game.Id).ToList();
        if(running.Count>0){var messages=running.Select(c=>states.ContainsKey(c.Handle)?states[c.Handle].Status:"Detected").Distinct();return String.Join("; ",messages);}
        DateTime launched;if(launchWaiting.TryGetValue(game.Id,out launched)){if((DateTime.UtcNow-launched).TotalMinutes<5)return "Waiting for game window";launchWaiting.Remove(game.Id);return "No window detected - open Tools > Diagnostics";}
        if(!String.IsNullOrEmpty(game.Folder)&&!Directory.Exists(game.Folder))return "Folder unavailable - locate game";
        var option=Options(game.Id);if(option.LaunchMode!="Default"&&!File.Exists(option.LaunchPath))return "Launch file missing - locate game";
        if(game.Source!="Steam"&&String.IsNullOrEmpty(game.LaunchUri)&&!File.Exists(game.Executable)&&option.LaunchMode=="Default")return "Find game EXE when you press Play";
        return "Ready";
    }
    void TestDisplay(){
        var target=ResolveTarget(settings.MonitorKey,settings.Monitor);if(target==null){Say("Choose a connected target display first.");return;}
        ShowDisplayTest(target);
    }
    void ShowDisplayTest(DisplayIdentity target){
        using(var form=new Form{Text="Selected display test",StartPosition=FormStartPosition.Manual,Size=new Size(520,240),BackColor=Theme.Card,ForeColor=Theme.Text,Font=Font,Icon=appIcon,TopMost=true}){
            var a=target.Screen.WorkingArea;form.Location=new Point(a.X+(a.Width-form.Width)/2,a.Y+(a.Height-form.Height)/2);
            var text=new Label{Dock=DockStyle.Fill,Padding=new Padding(20),TextAlign=ContentAlignment.MiddleCenter,Text="Your games will move to this display.\n\n"+target.Screen.DeviceName+"\n"+a.Width+" x "+a.Height+"\n\nClose this test to return to the launcher."};form.Controls.Add(text);
            var close=Btn("This is the display",delegate{form.Close();});close.Dock=DockStyle.Bottom;form.Controls.Add(close);form.ShowDialog(this);
        }
    }
    void OpenGameSettings(Game game){if(game==null)return;using(var form=ToolForm("Game settings - "+game.Name)){
        var body=ToolStack(form);body.Controls.Add(new Label{Text=game.Name,Font=new Font(Font,FontStyle.Bold),Width=610,Height=32});
        body.Controls.Add(new Label{Text=GameStatus(game)+"\n"+game.Folder,Width=610,Height=72,AutoEllipsis=true});
        ToolButton(body,SessionPaused(game.Id)?"Resume automatic movement":"Pause auto-move until this game closes",delegate{ToggleSessionPause(game);form.Close();});
        ToolButton(body,"Monitor and window size",delegate{EditProfile(game);});
        ToolButton(body,"Launch method and movement timing",delegate{EditGameOptions(game);});
        ToolButton(body,"Find game EXE automatically",delegate{if(FindExecutable(game,false))form.Close();});
        ToolButton(body,"Locate moved game / repair missing files",delegate{if(FindExecutable(game,true))form.Close();});
        ToolButton(body,"Save current window position",delegate{PickCapture(game);});
        ToolButton(body,"View live detection status",delegate{ShowActivity();});
        ToolButton(body,Options(game.Id).Favorite?"Remove from favorites":"Add to favorites",delegate{var option=Options(game.Id);option.Favorite=!option.Favorite;StoreOptions(option);form.Close();});
        body.Controls.Add(new Label{Text="Changes apply only to this game. Use Background settings on the main screen for all games.",Width=610,Height=60});form.ShowDialog(this);
    }}
    bool FindExecutable(Game game,bool relocate){
        string folder=game.Folder;
        if(relocate||!Directory.Exists(folder)){using(var pick=new FolderBrowserDialog{Description="Choose the folder containing only "+game.Name,SelectedPath=Directory.Exists(folder)?folder:""}){if(pick.ShowDialog(this)!=DialogResult.OK)return false;folder=pick.SelectedPath;}}
        if(!OtherLibraries.SafeFolder(folder)){MessageBox.Show(this,"Choose one game's folder, not a drive root or shared system folder.");return false;}
        string chosen="";bool ready=false;using(var form=ToolForm("Find executable - "+game.Name)){
            var info=new Label{Dock=DockStyle.Top,Height=85,Padding=new Padding(12),Text="Searching this game folder... You can cancel without changing anything."};
            var list=new ListBox{Dock=DockStyle.Fill,HorizontalScrollbar=true,BackColor=Theme.Raised,ForeColor=Theme.Text};
            var buttons=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=100,Padding=new Padding(12),WrapContents=true};
            var select=Btn("Use selected EXE",delegate{if(list.SelectedItem!=null){chosen=Convert.ToString(list.SelectedItem);form.Close();}});select.Enabled=false;
            var browse=Btn("Browse manually",delegate{using(var pick=new OpenFileDialog{InitialDirectory=folder,Filter="Game executable|*.exe",CheckFileExists=true})if(pick.ShowDialog(form)==DialogResult.OK){if(!SteamUnlockedLibrary.Within(pick.FileName,folder)){MessageBox.Show(form,"Choose an EXE inside the selected game folder.");return;}chosen=pick.FileName;form.Close();}});
            var cancel=Btn("Cancel",delegate{form.Close();});buttons.Controls.Add(select);buttons.Controls.Add(browse);buttons.Controls.Add(cancel);form.Controls.Add(list);form.Controls.Add(info);form.Controls.Add(buttons);
            var scanner=new BackgroundWorker{WorkerSupportsCancellation=true};scanner.DoWork+=delegate(object sender,DoWorkEventArgs e){e.Result=ExecutableDiscovery.Scan(folder,()=>scanner.CancellationPending);};
            scanner.RunWorkerCompleted+=delegate(object sender,RunWorkerCompletedEventArgs e){ready=true;if(!form.IsDisposed){if(e.Error!=null)info.Text="Search failed: "+e.Error.Message+". You can browse manually.";else{var paths=(List<string>)e.Result;foreach(string path in paths)list.Items.Add(path);if(paths.Count>0)list.SelectedIndex=0;select.Enabled=paths.Count>0;info.Text=paths.Count+" likely executable(s). Confirm the actual game EXE below.\nInstallers and common helper programs are filtered. Search is limited to 2,000 folders / 200 candidates; browse manually if needed.";}}scanner.Dispose();};
            form.FormClosing+=delegate{if(!ready)scanner.CancelAsync();};scanner.RunWorkerAsync();form.ShowDialog(this);
        }
        if(chosen.Length==0||!File.Exists(chosen))return false;
        var option=Options(game.Id);option.LaunchMode="Executable";option.LaunchPath=chosen;option.DetectionPath=chosen;option.DetectionFolder=folder;option.GameName=game.Name;option.GameSource=game.Source;option.GameLaunchUri=game.LaunchUri;
        var custom=settings.CustomGames.FirstOrDefault(c=>c.Id==game.Id);string oldExe=custom==null?"":custom.Executable,oldFolder=custom==null?"":custom.WatchFolder;if(custom!=null){custom.Executable=chosen;custom.WatchFolder=folder;}
        if(!StoreOptions(option)){if(custom!=null){custom.Executable=oldExe;custom.WatchFolder=oldFolder;}return false;}
        CatalogChanged();Say("Game files updated for "+game.Name+". Monitor profile, favorites and exclusions were kept.");return true;
    }
    FlowLayoutPanel ToolsPage(TabControl tabs,string title,string help){var page=new TabPage(title){BackColor=Theme.Card,ForeColor=Theme.Text};tabs.TabPages.Add(page);var body=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(14),FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};page.Controls.Add(body);body.Controls.Add(new Label{Text=help,Width=610,Height=65,ForeColor=Theme.Muted});return body;}
    void MergeDuplicates(){
        var pairs=new List<Tuple<CustomGame,Game>>();
        foreach(var custom in settings.CustomGames){var target=games.FirstOrDefault(g=>g.Id!=custom.Id&&!g.Id.StartsWith("custom:")&&!String.IsNullOrEmpty(g.Executable)&&String.Equals(g.Executable,custom.Executable,StringComparison.OrdinalIgnoreCase));if(target==null)target=games.OrderBy(g=>g.Id,StringComparer.Ordinal).FirstOrDefault(g=>g.Id!=custom.Id&&g.Id.StartsWith("custom:")&&String.Equals(g.Executable,custom.Executable,StringComparison.OrdinalIgnoreCase)&&String.CompareOrdinal(g.Id,custom.Id)<0);if(target!=null)pairs.Add(Tuple.Create(custom,target));}
        if(pairs.Count==0){MessageBox.Show(this,"No duplicate custom entries with the same exact EXE were found. Different store installations and similarly named games are kept separate.","Duplicate cleanup");return;}
        using(var form=ToolForm("Review duplicate games")){var body=ToolStack(form);body.Controls.Add(new Label{Width=610,Height=75,Text="Review duplicates below. Favorites and exclusions combine; recent play keeps the newest date. Entries with conflicting profiles or launch settings are skipped."});var list=new CheckedListBox{Width=610,Height=250,CheckOnClick=true};foreach(var pair in pairs)list.Items.Add(pair.Item1.Name+" -> "+pair.Item2.Name+" ("+pair.Item2.Source+")",false);body.Controls.Add(list);
            ToolButton(body,"Merge checked duplicates",delegate{
                // Snapshot the whole settings object so save failures can restore it.
                Settings before;var serializer=new System.Xml.Serialization.XmlSerializer(typeof(Settings));using(var text=new StringWriter()){serializer.Serialize(text,settings);using(var reader=new StringReader(text.ToString()))before=(Settings)serializer.Deserialize(reader);}
                int merged=0,skipped=0;foreach(int index in list.CheckedIndices){var pair=pairs[index];string from=pair.Item1.Id,to=pair.Item2.Id;var source=settings.GameOptions.FirstOrDefault(o=>o.Id==from);var dest=settings.GameOptions.FirstOrDefault(o=>o.Id==to);var sourceProfile=settings.Profiles.FirstOrDefault(p=>p.Id==from);var destProfile=settings.Profiles.FirstOrDefault(p=>p.Id==to);
                    if(pair.Item1.Arguments!=pair.Item2.Arguments||(sourceProfile!=null&&destProfile!=null)||(source!=null&&dest!=null&&(source.LaunchMode!="Default"||source.DelaySeconds!=0||source.StopAfterPlacement||!String.IsNullOrEmpty(source.Artwork)||!String.IsNullOrEmpty(source.DetectionPath)))){skipped++;continue;}
                    if(source!=null){if(dest==null){source.Id=to;}else{dest.Favorite|=source.Favorite;if(source.LastPlayed>dest.LastPlayed)dest.LastPlayed=source.LastPlayed;settings.GameOptions.Remove(source);}}
                    if(sourceProfile!=null)sourceProfile.Id=to;
                    foreach(var exclusion in settings.ExcludedGames.Where(e=>e.Id==from)){exclusion.Id=to;exclusion.Name=pair.Item2.Name;exclusion.Folder=pair.Item2.Folder;}
                    settings.CustomGames.Remove(pair.Item1);merged++;
                }
                if(!Save()){settings=before;return;}CatalogChanged();Say(merged+" duplicate(s) merged; "+skipped+" conflicting entries kept.");form.Close();
            });form.ShowDialog(this);
        }
    }
}
