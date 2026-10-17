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
        var p=settings.Profiles.FirstOrDefault(x=>x.Id==game.Id&&x.Override);if(ResolveTarget(p==null?settings.MonitorKey:p.MonitorKey,p==null?settings.Monitor:p.Monitor)==null)return "Selected monitor unavailable - connect it or configure fallback";
        var running=lastCandidates.Where(c=>c.GameId==game.Id).ToList();
        if(running.Count>0){var messages=running.Select(c=>states.ContainsKey(c.Handle)?states[c.Handle].Status:"Detected").Distinct();return String.Join("; ",messages);}
        DateTime launched;if(launchWaiting.TryGetValue(game.Id,out launched)){if((DateTime.UtcNow-launched).TotalMinutes<5)return "Waiting for game window";launchWaiting.Remove(game.Id);return "No window detected - open Tools > Diagnostics";}
        if(!String.IsNullOrEmpty(game.Folder)&&!Directory.Exists(game.Folder))return "Folder unavailable - locate game";
        var option=Options(game.Id);if(option.LaunchMode!="Default"&&!File.Exists(option.LaunchPath))return "Launch file missing - locate game";
        if(game.Source!="Steam"&&String.IsNullOrEmpty(game.LaunchUri)&&!File.Exists(game.Executable)&&option.LaunchMode=="Default")return "Find game EXE when you press Play";
        return "Ready";
    }
    void TestDisplay(){
        var selected=monitors.SelectedItem as DisplayItem;var target=selected==null?null:Displays.Resolve(Displays.Key(selected.Screen),selected.Screen.DeviceName);if(target==null){Say("Choose a connected target display first.");return;}
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
        ToolButton(body,"Compatibility and multiple-window rules",delegate{CompatibilityDialog(game);});
        ToolButton(body,"Preview saved placement",delegate{PreviewPlacement(game);});
        ToolButton(body,"Explain detection and movement",delegate{ExplainGame(game);});
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
                    if(settings.WindowRules.Any(r=>r.Id==from||r.Id==to)||pair.Item1.Arguments!=pair.Item2.Arguments||(sourceProfile!=null&&destProfile!=null)||(source!=null&&dest!=null&&(source.LaunchMode!="Default"||source.DelaySeconds!=0||source.StopAfterPlacement||!String.IsNullOrEmpty(source.Artwork)||!String.IsNullOrEmpty(source.DetectionPath)))){skipped++;continue;}
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

public class WindowRule {
    public string Id="",MainExe="",HelperTitle="",HelperKey="",HelperMonitor="";
    public bool KeepHelpers=true;
    public bool IsHelper(Candidate c){
        return (!String.IsNullOrWhiteSpace(MainExe)&&!String.Equals(Path.GetFileName(c.Executable),MainExe.Trim(),StringComparison.OrdinalIgnoreCase))||
            (!String.IsNullOrWhiteSpace(HelperTitle)&&(c.Title??"").IndexOf(HelperTitle.Trim(),StringComparison.OrdinalIgnoreCase)>=0);
    }
}
public partial class Launcher {
    HashSet<string> connectedKeys;
    HashSet<string> reconnectHolds=new HashSet<string>();
    void UpdateDefaultBadge(){defaultBadge.Text=String.IsNullOrEmpty(settings.Monitor)?"Default: not set":"Default: "+settings.Monitor;}
    void CheckDisplayReconnect(){
        var now=new HashSet<string>(Displays.All().Select(d=>d.Key));
        if(connectedKeys!=null){
            var returned=new HashSet<string>(now.Except(connectedKeys));
            foreach(var c in lastCandidates){var p=settings.Profiles.FirstOrDefault(x=>x.Id==c.GameId&&x.Override);string key=p==null?settings.MonitorKey:p.MonitorKey;
                if(returned.Contains(key)){
                    if(settings.OfferReconnect)reconnectHolds.Add(c.GameId);
                    else states.Remove(c.Handle);
                }
            }
            if(reconnectHolds.Count>0&&returned.Count>0){tray.ShowBalloonTip(6000,"Game monitor reconnected","Open Tools > Display controls > Reconnect recovery to move games back or keep them where they are.",ToolTipIcon.Info);Say("A preferred monitor returned. Reconnect recovery is waiting in Tools.");}
            // A disconnected destination should retry on fallback even after initial placement.
            if(connectedKeys.Except(now).Any())foreach(var state in states.Values){state.Placed=false;state.Pending=false;state.Attempts=0;state.First=DateTime.UtcNow;}
        }
        connectedKeys=now;reconnectHolds.RemoveWhere(id=>!lastCandidates.Any(c=>c.GameId==id));
    }
    void ReconnectDialog(){using(var form=ToolForm("Reconnect recovery")){var body=ToolStack(form);
        var offer=new CheckBox{AutoSize=true,Text="Ask before returning running games to a reconnected monitor",Checked=settings.OfferReconnect};body.Controls.Add(offer);
        ToolButton(body,"Save reconnect preference",delegate{bool old=settings.OfferReconnect;settings.OfferReconnect=offer.Checked;if(!Save())settings.OfferReconnect=old;});
        var list=new ListBox{Width=610,Height=220};foreach(var g in games.Where(g=>reconnectHolds.Contains(g.Id)))list.Items.Add(g);body.Controls.Add(list);
        body.Controls.Add(new Label{Width=610,Height=70,Text="Choose a waiting game. Move back retries its automatic placement; Keep here holds its current windows until they close. Exclusions and session pauses still apply."});
        ToolButton(body,"Move selected game back",delegate{var g=list.SelectedItem as Game;if(g==null)return;reconnectHolds.Remove(g.Id);foreach(var c in lastCandidates.Where(c=>c.GameId==g.Id))states.Remove(c.Handle);list.Items.Remove(g);});
        ToolButton(body,"Keep selected game here this session",delegate{var g=list.SelectedItem as Game;if(g==null)return;if(!SessionPaused(g.Id))ToggleSessionPause(g);reconnectHolds.Remove(g.Id);list.Items.Remove(g);});form.ShowDialog(this);
    }}
    void CompatibilityDialog(Game game){using(var form=ToolForm("Compatibility - "+game.Name)){var body=ToolStack(form);
        var old=settings.WindowRules.FirstOrDefault(r=>r.Id==game.Id)??new WindowRule{Id=game.Id};
        body.Controls.Add(new Label{Width=610,Height=45,Text="Optional preset: changes this game's delay, borderless setting and stop-after-placement behavior when saved."});
        var preset=new ComboBox{Width=610,DropDownStyle=ComboBoxStyle.DropDownList};preset.Items.AddRange(new object[]{"Keep current settings","Standard windowed - 2 second delay","Slow startup / ARK - 30 seconds, borderless","Place once - 5 second delay"});preset.SelectedIndex=0;body.Controls.Add(preset);
        body.Controls.Add(new Label{Width=610,Height=65,Text="Main game EXE filename (optional). Other detected EXEs become helpers. Leave blank to treat all EXEs as game windows. Example: ArkAscended.exe"});
        var exe=new TextBox{Width=610,Text=old.MainExe};body.Controls.Add(exe);
        body.Controls.Add(new Label{Width=610,Height=45,Text="Helper window title contains (optional, case-insensitive). Matching windows count as helpers even in the main EXE."});
        var title=new TextBox{Width=610,Text=old.HelperTitle};body.Controls.Add(title);
        var keep=new CheckBox{Text="Leave helper windows where they are",Checked=old.KeepHelpers,AutoSize=true};body.Controls.Add(keep);
        var destination=new ComboBox{Width=610,DropDownStyle=ComboBoxStyle.DropDownList,Enabled=!keep.Checked};foreach(var d in Displays.All()){destination.Items.Add(d);if(d.Key==old.HelperKey)destination.SelectedItem=d;}body.Controls.Add(destination);keep.CheckedChanged+=delegate{destination.Enabled=!keep.Checked;};
        body.Controls.Add(new Label{Width=610,Height=65,Text="Uncheck to move helpers to the display above. Rules apply only to windows already detected as part of this game. Borderless requires Windowed mode in the game."});
        ToolButton(body,"Save compatibility profile and window rules",delegate{
            string filename=exe.Text.Trim();if(filename.Length>0&&(filename.IndexOfAny(new[]{'\\','/'})>=0||!filename.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))){MessageBox.Show(form,"Enter only an EXE filename, such as ArkAscended.exe.");return;}
            var target=destination.SelectedItem as DisplayIdentity;if(!keep.Checked&&target==null){MessageBox.Show(form,"Choose a connected helper display.");return;}
            var priorRule=settings.WindowRules.FirstOrDefault(r=>r.Id==game.Id);var priorOption=settings.GameOptions.FirstOrDefault(o=>o.Id==game.Id);var priorProfile=settings.Profiles.FirstOrDefault(p=>p.Id==game.Id);
            var rule=new WindowRule{Id=game.Id,MainExe=filename,HelperTitle=title.Text.Trim(),KeepHelpers=keep.Checked,HelperKey=target==null?old.HelperKey:target.Key,HelperMonitor=target==null?old.HelperMonitor:target.Screen.DeviceName};
            if(priorRule!=null)settings.WindowRules.Remove(priorRule);settings.WindowRules.Add(rule);
            if(preset.SelectedIndex>0){var option=Options(game.Id);option.DelaySeconds=preset.SelectedIndex==2?30:preset.SelectedIndex==3?5:2;option.StopAfterPlacement=preset.SelectedIndex==3;if(priorOption!=null)settings.GameOptions.Remove(priorOption);settings.GameOptions.Add(option);
                var p=priorProfile;var profile=new Profile{Id=game.Id,Override=true,Monitor=p!=null&&p.Override?p.Monitor:settings.Monitor,MonitorKey=p!=null&&p.Override?p.MonitorKey:settings.MonitorKey,Borderless=preset.SelectedIndex==2,UseLayout=p!=null&&p.UseLayout,X=p==null?0:p.X,Y=p==null?0:p.Y,Width=p==null?0:p.Width,Height=p==null?0:p.Height};if(priorProfile!=null)settings.Profiles.Remove(priorProfile);settings.Profiles.Add(profile);
            }
            if(!Save()){settings.WindowRules.Remove(rule);if(priorRule!=null)settings.WindowRules.Add(priorRule);settings.GameOptions.RemoveAll(o=>o.Id==game.Id);if(priorOption!=null)settings.GameOptions.Add(priorOption);settings.Profiles.RemoveAll(p=>p.Id==game.Id);if(priorProfile!=null)settings.Profiles.Add(priorProfile);return;}
            states.Clear();Say("Compatibility and window rules saved for "+game.Name);form.Close();
        });form.ShowDialog(this);
    }}
    void ExplainGame(Game game){using(var form=ToolForm("Detection details - "+game.Name)){var p=settings.Profiles.FirstOrDefault(x=>x.Id==game.Id&&x.Override);var o=Options(game.Id);var target=ResolveTarget(p==null?settings.MonitorKey:p.MonitorKey,p==null?settings.Monitor:p.Monitor);var text=new TextBox{Dock=DockStyle.Fill,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical};form.Controls.Add(text);
        text.Text=game.Name+"\r\n\r\nStatus: "+GameStatus(game)+"\r\nMonitor source: "+(p==null?"Default monitor":"Per-game override")+"\r\nResolved target: "+(target==null?"Unavailable":target.ToString())+"\r\nStartup delay: "+o.DelaySeconds+" seconds per new window\r\nStop after placement: "+o.StopAfterPlacement+"\r\nDetected windows: "+lastCandidates.Count(c=>c.GameId==game.Id)+"\r\nFolder: "+game.Folder+"\r\nEXE: "+game.Executable+"\r\n\r\n"+String.Join("\r\n",lastCandidates.Where(c=>c.GameId==game.Id).Select(c=>c.Title+" | "+(states.ContainsKey(c.Handle)?states[c.Handle].Status:"Waiting for scan")))+"\r\n\r\nIf no window appears, start the game and refresh games, then use Find game EXE if needed. If Windows rejects movement, ensure both apps run at the same privilege level. If the game resets its position, use Windowed mode and a longer startup delay. Exclusions, session pauses and manual holds take priority over movement rules.";form.ShowDialog(this);
    }}
    void PreviewPlacement(Game game){var p=settings.Profiles.FirstOrDefault(x=>x.Id==game.Id&&x.Override);var target=ResolveTarget(p==null?settings.MonitorKey:p.MonitorKey,p==null?settings.Monitor:p.Monitor);if(target==null){MessageBox.Show(this,"The destination display is unavailable. Connect it or configure fallback.");return;}
        var c=lastCandidates.FirstOrDefault(x=>x.GameId==game.Id);Native.RECT rect=new Native.RECT{Right=1280,Bottom=720};if(c!=null)Native.GetWindowRect(c.Handle,out rect);bool borderless=p==null?settings.Borderless:p.Borderless;var bounds=DesiredBounds(rect,target.Screen,borderless,p);
        using(var form=ToolForm("Placement preview - "+game.Name)){var panel=new BufferedPanel{Dock=DockStyle.Fill};form.Controls.Add(panel);var label=new Label{Dock=DockStyle.Bottom,Height=100,Padding=new Padding(12),Text=target+"\nWindow: "+bounds.Width+" x "+bounds.Height+" at "+bounds.X+", "+bounds.Y+"\n"+(c==null&&!(p!=null&&p.UseLayout)&&!borderless?"Estimate using a 1280 x 720 window. ":"")+"Preview only; no game window is moved."};form.Controls.Add(label);
            panel.Paint+=delegate(object sender,PaintEventArgs e){var area=target.Screen.Bounds;float scale=Math.Min((panel.Width-40f)/area.Width,(panel.Height-40f)/area.Height);if(scale<=0)return;var screen=new RectangleF(20,20,area.Width*scale,area.Height*scale);using(var brush=new SolidBrush(Theme.Raised))e.Graphics.FillRectangle(brush,screen);var box=new RectangleF(20+(bounds.X-area.X)*scale,20+(bounds.Y-area.Y)*scale,bounds.Width*scale,bounds.Height*scale);using(var pen=new Pen(Theme.Accent,3))e.Graphics.DrawRectangle(pen,box.X,box.Y,box.Width,box.Height);};form.ShowDialog(this);
        }
    }
}
