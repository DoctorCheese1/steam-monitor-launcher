using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

public class CustomGame {public string Id="",Name="",Executable="",Arguments="",WatchFolder="";}
public partial class Launcher {
    [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hwnd,int id);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    string hotkeyStatus="Not registered";
    static void NormalizeSettings(Settings s){
        if(s.MonitorPresets==null)s.MonitorPresets=new List<MonitorPreset>();s.MonitorPresets.RemoveAll(p=>p==null);
        if(!s.GitHubFeedConfigured){if(String.IsNullOrWhiteSpace(s.UpdateFeed)){s.UpdateFeed=ReleaseClient.DefaultFeed;s.AutoCheckUpdates=true;}s.GitHubFeedConfigured=true;}
        s.Steam=s.Steam??"";s.Monitor=s.Monitor??"";s.MonitorKey=s.MonitorKey??"";
        if(s.ExcludedGames==null)s.ExcludedGames=new List<GameExclusion>();
        if(s.Profiles==null)s.Profiles=new List<Profile>();
        if(s.GameOptions==null)s.GameOptions=new List<GameOptions>();s.GameOptions.RemoveAll(x=>x==null);
        if(s.CustomGames==null)s.CustomGames=new List<CustomGame>();
    }
    List<Game> CustomCatalog(){return settings.CustomGames.Select(c=>new Game{Id=c.Id,Name=c.Name,Executable=c.Executable,Arguments=c.Arguments,Source="Custom",MatchFolder=!String.IsNullOrEmpty(c.WatchFolder),Folder=String.IsNullOrEmpty(c.WatchFolder)?Path.GetDirectoryName(c.Executable)??"":c.WatchFolder}).Concat(settings.GameOptions.Where(o=>!String.IsNullOrEmpty(o.DetectionPath)&&!settings.CustomGames.Any(c=>c.Id==o.Id)).Select(o=>new Game{Id=o.Id,Name=o.GameName,Source=o.GameSource,Executable=o.DetectionPath,Folder=o.DetectionFolder,MatchFolder=!String.IsNullOrEmpty(o.DetectionFolder),LaunchUri=o.GameLaunchUri})).ToList();}
    static bool RectClose(Native.RECT r,Rectangle target){return Math.Abs((long)r.Left-target.Left)<=4&&Math.Abs((long)r.Top-target.Top)<=4&&Math.Abs((long)r.Right-target.Right)<=4&&Math.Abs((long)r.Bottom-target.Bottom)<=4;}
    public static Rectangle SavedBounds(Profile profile,Rectangle area){
        int w=Math.Max(100,Math.Min(area.Width,profile.Width)),h=Math.Max(100,Math.Min(area.Height,profile.Height));
        return new Rectangle(area.X+Math.Max(0,Math.Min(area.Width-w,profile.X)),area.Y+Math.Max(0,Math.Min(area.Height-h,profile.Y)),w,h);
    }
    static Rectangle DesiredBounds(Native.RECT rect,Screen screen,bool borderless,Profile profile){
        if(borderless)return screen.Bounds;
        if(profile!=null&&profile.UseLayout&&profile.Width>0&&profile.Height>0)return SavedBounds(profile,screen.WorkingArea);
        var a=screen.WorkingArea;int w=Math.Min(a.Width,Math.Max(320,rect.Right-rect.Left)),h=Math.Min(a.Height,Math.Max(240,rect.Bottom-rect.Top));return new Rectangle(a.X+(a.Width-w)/2,a.Y+(a.Height-h)/2,w,h);
    }
    static bool ApplyPosition(IntPtr h,Rectangle bounds,bool borderless){
        if(Native.IsZoomed(h)||Native.IsIconic(h))Native.ShowWindowAsync(h,9);
        if(borderless)Native.Style(h,Native.Style(h)&~0x00CF0000L);
        return Native.SetWindowPos(h,IntPtr.Zero,bounds.X,bounds.Y,bounds.Width,bounds.Height,0x0004|0x0010|0x0020|0x4000);
    }
    void SetGameStatus(Candidate game,WindowState state,string value){if(state.Status==value)return;state.Status=value;Say(game.Game+": "+value);}
    void ReleaseHotkeys(){if(IsHandleCreated)for(int i=1;i<=4;i++)UnregisterHotKey(Handle,i);}
    void RegisterKeys(){ReleaseHotkeys();if(!settings.Hotkeys){hotkeyStatus="Hotkeys disabled";return;}var failed=new List<string>();uint[] keys={(uint)Keys.M,(uint)Keys.P,(uint)Keys.R,(uint)Keys.S};for(int i=0;i<keys.Length;i++)if(!RegisterHotKey(Handle,i+1,0x4003,keys[i]))failed.Add(((Keys)keys[i]).ToString());hotkeyStatus=failed.Count==0?"All hotkeys registered":"Unavailable (already in use): Ctrl+Alt+"+String.Join(", Ctrl+Alt+",failed);Diagnostics.Write(hotkeyStatus);}
    Candidate ForegroundGame(){var h=GetForegroundWindow();return lastCandidates.FirstOrDefault(c=>c.Handle==h);}
    void HandleHotkey(int id){
        if(id==2){enabled.Checked=!enabled.Checked;return;}
        var c=ForegroundGame();if(c==null){Say("Hotkey: focus a detected game window first.");return;}
        if(Exclusions.Contains(settings.ExcludedGames,c.GameId,c.GameFolder,c.Game)){Say("Hotkey skipped: this game is excluded.");return;}
        uint pid;Native.GetWindowThreadProcessId(c.Handle,out pid);string exe;DateTime born;if(pid!=c.Pid||!Native.ProcessInfo(pid,out exe,out born)||born!=c.Born)return;
        if(id==4){CaptureLayout(c);return;}
        if(id==3){WindowState old;if(states.TryGetValue(c.Handle,out old)){enabled.Checked=false;Native.Style(c.Handle,old.Style);var r=old.Rect;Native.SetWindowPos(c.Handle,IntPtr.Zero,r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top,0x0004|0x0010|0x0020|0x4000);Say("Restore sent. Monitoring paused to preserve that position.");}else Say("No original position recorded for this window.");return;}
        var profile=settings.Profiles.FirstOrDefault(p=>p.Id==c.GameId&&p.Override);var target=ResolveTarget(profile==null?settings.MonitorKey:profile.MonitorKey,profile==null?settings.Monitor:profile.Monitor);
        if(target==null){Say("Target display is unavailable.");return;}
        Native.RECT rect;if(!Native.GetWindowRect(c.Handle,out rect))return;
        WindowState state;if(!states.TryGetValue(c.Handle,out state)){state=new WindowState{GameId=c.GameId,GameFolder=c.GameFolder,GameName=c.Game,Pid=c.Pid,Born=c.Born,Rect=rect,Style=Native.Style(c.Handle)};states[c.Handle]=state;}
        bool borderless=profile==null?settings.Borderless:profile.Borderless;state.Expected=DesiredBounds(rect,target.Screen,borderless,profile);state.Attempts++;state.Last=DateTime.UtcNow;state.First=state.Last;state.Pending=ApplyPosition(c.Handle,state.Expected,borderless);SetGameStatus(c,state,state.Pending?"Move sent; checking result...":"Windows rejected movement");
    }
    bool CaptureLayout(Candidate c){
        if(String.IsNullOrEmpty(c.GameId)){Say("Add this game as a custom EXE or locate its Steam library before saving a profile.");return false;}
        Native.RECT rect;if(!Native.IsWindow(c.Handle)||Native.IsIconic(c.Handle)||!Native.GetWindowRect(c.Handle,out rect)){Say("The game window is closed or minimized.");return false;}
        uint pid;Native.GetWindowThreadProcessId(c.Handle,out pid);if(pid!=c.Pid)return false;
        var screen=Screen.FromHandle(c.Handle);var a=screen.WorkingArea;
        var old=settings.Profiles.FirstOrDefault(p=>p.Id==c.GameId);
        var profile=new Profile{Id=c.GameId,Override=true,Monitor=screen.DeviceName,MonitorKey=Displays.Key(screen),Borderless=false,UseLayout=true,X=rect.Left-a.Left,Y=rect.Top-a.Top,Width=rect.Right-rect.Left,Height=rect.Bottom-rect.Top};
        if(old!=null)settings.Profiles.Remove(old);settings.Profiles.Add(profile);
        if(!Save()){settings.Profiles.Remove(profile);if(old!=null)settings.Profiles.Add(old);return false;}
        states.Clear();Say("Saved position and size for "+c.Game+". Its profile now uses this monitor and windowed layout.");return true;
    }
    Form ToolForm(string title){return new Form{Text=title,Size=new Size(740,640),MinimumSize=new Size(680,580),StartPosition=FormStartPosition.CenterParent,BackColor=Theme.Card,ForeColor=Theme.Text,Font=Font,Icon=appIcon};}
    FlowLayoutPanel ToolStack(Form form){var body=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(18),FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};form.Controls.Add(body);return body;}
    void ToolButton(FlowLayoutPanel panel,string title,EventHandler action){var b=Btn(title,action);b.Width=620;b.Height=40;b.AutoSize=false;b.Margin=new Padding(0,0,0,10);panel.Controls.Add(b);}
    void OpenTools(){using(var form=ToolForm("Tools")){
        form.Width=780;var tabs=new TabControl{Dock=DockStyle.Fill};form.Controls.Add(tabs);
        var library=ToolsPage(tabs,"Library management","Add games from other launchers, import shortcuts, or clean up duplicates. For one game's monitor and launch options, use Game settings on the main screen.");
        ToolButton(library,"Add a game EXE",delegate{EditCustom(null);});
        ToolButton(library,"Scan folders and import games",delegate{ScanFolders();});
        ToolButton(library,"Import desktop or Start Menu shortcuts",delegate{ImportShortcuts();});
        ToolButton(library,"Review duplicate games",delegate{MergeDuplicates();});
        var selected=gameList.SelectedItem as Game;var custom=selected==null?null:settings.CustomGames.FirstOrDefault(c=>c.Id==selected.Id);
        if(custom!=null){ToolButton(library,"Edit selected custom game",delegate{EditCustom(custom);});ToolButton(library,"Remove selected custom game",delegate{if(MessageBox.Show(form,"Remove "+custom.Name+" from this library? Game files will stay on disk.","Remove game",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;settings.CustomGames.Remove(custom);if(!Save()){settings.CustomGames.Add(custom);return;}CatalogChanged();form.Close();});}
        var display=ToolsPage(tabs,"Display controls","Choose how the app behaves when a monitor disconnects, or move a running window manually.");
        ToolButton(display,"Monitor presets",delegate{MonitorPresets();});ToolButton(display,"Run setup walkthrough",delegate{SetupWizard();});ToolButton(display,"Test selected display",delegate{TestDisplay();});ToolButton(display,"Refresh connected displays",delegate{LoadDisplays();});ToolButton(display,"Monitor disconnect fallback",delegate{FallbackDialog();});ToolButton(display,"Move a running window",delegate{ManualMove();});ToolButton(display,"ARK windowed setup",delegate{ArkSetup();});ToolButton(display,"Keyboard shortcuts",delegate{HotkeyDialog();});
        var diagnostics=ToolsPage(tabs,"Diagnostics","See why a game has not moved. Reports include recent detection and movement results.");ToolButton(diagnostics,"Live game status",delegate{ShowActivity();});ToolButton(diagnostics,"Export troubleshooting report",delegate{ExportReport();});ToolButton(diagnostics,"Open logs",delegate{Directory.CreateDirectory(Diagnostics.Folder);Process.Start("explorer.exe",Diagnostics.Folder);});
        var recovery=ToolsPage(tabs,"Backup and recovery","Settings backups keep your monitor choices, favorites, exclusions and game profiles. Open Updates for online or offline app updates.");ToolButton(recovery,"Restore an automatic settings backup",delegate{RecoverSettings();});ToolButton(recovery,"Export settings backup",delegate{ExportSettings();});ToolButton(recovery,"Import settings backup",delegate{ImportSettings();});ToolButton(recovery,"Open updates and app recovery",delegate{OpenUpdater();});ToolButton(recovery,"Open installed app folder",delegate{Process.Start("explorer.exe",AppPaths.Portable?Data:Path.Combine(Data,"bin"));});
        ToolButton(recovery,"Portable mode help",delegate{MessageBox.Show(form,"Run Build Portable.bat, then open Portable\\SteamMonitorLauncher.exe. Copy the entire Portable folder to move settings and artwork with the app.","Portable mode");});
        ToolButton(recovery,"Uninstall app",delegate{if(AppPaths.Portable){MessageBox.Show(form,"Disable Start with Windows if enabled, exit the app and remove its portable folder.");return;}string script=Path.Combine(Data,"Uninstall.ps1");if(File.Exists(script))Process.Start(new ProcessStartInfo("powershell.exe","-NoProfile -ExecutionPolicy Bypass -File \""+script+"\""){UseShellExecute=true});});
        form.ShowDialog(this);
    }}
    void EditProfile(Game game){using(var form=ToolForm("Profile: "+game.Name)){var body=ToolStack(form);var existing=settings.Profiles.FirstOrDefault(x=>x.Id==game.Id);var p=existing??new Profile{Id=game.Id,Monitor=settings.Monitor,MonitorKey=settings.MonitorKey,Borderless=settings.Borderless};
        var use=new CheckBox{Text="Override the global settings for this game",Checked=p.Override,AutoSize=true,Margin=new Padding(0,0,0,15)};body.Controls.Add(use);
        var monitor=new ComboBox{Width=620,DropDownStyle=ComboBoxStyle.DropDownList};foreach(var d in Displays.All())monitor.Items.Add(d);var match=Displays.Resolve(p.MonitorKey,p.Monitor);if(match!=null)for(int i=0;i<monitor.Items.Count;i++)if(((DisplayIdentity)monitor.Items[i]).Key==match.Key)monitor.SelectedIndex=i;body.Controls.Add(monitor);
        var borderless=new CheckBox{Text="Fill the monitor with a borderless window",Checked=p.Borderless,AutoSize=true,Margin=new Padding(0,20,0,12)};body.Controls.Add(borderless);
        var layout=new CheckBox{Text="Restore saved window position and size (when borderless is off)",Checked=p.UseLayout,Enabled=p.Width>0&&p.Height>0,AutoSize=true};body.Controls.Add(layout);
        body.Controls.Add(new Label{Width=620,Height=85,Text=p.Width>0?"Saved layout: "+p.Width+" x "+p.Height+", offset "+p.X+", "+p.Y+" from the display work area.\nUse Save layout in Tools, or Ctrl+Alt+S while the game is focused, to replace it.":"No saved layout yet. Arrange the game window, then use Save layout in Tools or Ctrl+Alt+S while the game is focused."});
        ToolButton(body,"Save profile",delegate{var d=monitor.SelectedItem as DisplayIdentity;if(use.Checked&&d==null){MessageBox.Show(form,"Choose a connected monitor.");return;}var updated=new Profile{Id=game.Id,Override=use.Checked,Monitor=d==null?p.Monitor:d.Screen.DeviceName,MonitorKey=d==null?p.MonitorKey:d.Key,Borderless=borderless.Checked,UseLayout=layout.Checked,X=p.X,Y=p.Y,Width=p.Width,Height=p.Height};if(existing!=null)settings.Profiles.Remove(existing);settings.Profiles.Add(updated);if(!Save()){settings.Profiles.Remove(updated);if(existing!=null)settings.Profiles.Add(existing);return;}states.Clear();Say("Profile saved for "+game.Name);form.Close();});
        form.ShowDialog(this);
    }}
    void PickCapture(Game game){using(var form=ToolForm("Save game layout")){var list=new ListBox{Dock=DockStyle.Fill,BackColor=Theme.Card,ForeColor=Theme.Text};var candidates=lastCandidates.Where(x=>x.GameId==game.Id).ToList();foreach(var c in candidates)list.Items.Add(c.Title);var save=Btn("Save selected window layout",delegate{if(list.SelectedIndex>=0&&list.SelectedIndex<candidates.Count&&CaptureLayout(candidates[list.SelectedIndex]))form.Close();});save.Dock=DockStyle.Bottom;form.Controls.Add(list);form.Controls.Add(save);if(candidates.Count==0)list.Items.Add("No running window detected. Start the game first.");form.ShowDialog(this);}}
    void EditCustom(CustomGame current){string exe=current==null?"":current.Executable;if(current==null)using(var dialog=new OpenFileDialog{Filter="Game executable (*.exe)|*.exe"}){if(dialog.ShowDialog()!=DialogResult.OK)return;exe=dialog.FileName;}
        using(var form=ToolForm("Custom game")){var body=ToolStack(form);body.Controls.Add(new Label{Text="Display name",Width=620});var name=new TextBox{Width=620,Text=current==null?Path.GetFileNameWithoutExtension(exe):current.Name};body.Controls.Add(name);body.Controls.Add(new Label{Text="Executable (exact file used for detection)",Width=620});var file=new TextBox{Width=620,Text=exe};body.Controls.Add(file);body.Controls.Add(new Label{Text="Optional launch arguments",Width=620});var args=new TextBox{Width=620,Text=current==null?"":current.Arguments};body.Controls.Add(args);body.Controls.Add(new Label{Text="Select the actual game EXE when possible. Protected Xbox/Microsoft Store games may not expose a usable EXE. Required store launchers and sign-in still apply.",Width=620,Height=80});
        body.Controls.Add(new Label{Text="Optional game folder to watch (only this game, not a shared library)",Width=620});var watch=new TextBox{Width=620,Text=current==null?"":current.WatchFolder};body.Controls.Add(watch);ToolButton(body,"Browse game folder...",delegate{using(var picker=new FolderBrowserDialog{Description="Select one game folder"})if(picker.ShowDialog(form)==DialogResult.OK)watch.Text=picker.SelectedPath;});
        ToolButton(body,"Save custom game",delegate{if(!String.IsNullOrWhiteSpace(watch.Text)&&!OtherLibraries.SafeFolder(watch.Text)){MessageBox.Show(form,"Choose an existing folder for one game, not a drive root, Windows folder or shared program folder.");return;}if(!File.Exists(file.Text)||!file.Text.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||String.IsNullOrWhiteSpace(name.Text)){MessageBox.Show(form,"Enter a name and an existing EXE path.");return;}if(settings.CustomGames.Any(x=>(current==null||x.Id!=current.Id)&&String.Equals(x.Executable,Path.GetFullPath(file.Text),StringComparison.OrdinalIgnoreCase))){MessageBox.Show(form,"That EXE is already in your custom games.");return;}var c=new CustomGame{Id=current==null?"custom:"+Guid.NewGuid().ToString("N"):current.Id,Name=name.Text.Trim(),Executable=Path.GetFullPath(file.Text),Arguments=args.Text,WatchFolder=String.IsNullOrWhiteSpace(watch.Text)?"":Path.GetFullPath(watch.Text.Trim())};if(current!=null)settings.CustomGames.Remove(current);settings.CustomGames.Add(c);if(!Save()){settings.CustomGames.Remove(c);if(current!=null)settings.CustomGames.Add(current);return;}CatalogChanged();form.Close();});form.ShowDialog(this);}
    }
    void CatalogChanged(){revision++;states.Clear();lastCandidates.Clear();var overrides=CustomCatalog();games=games.Where(x=>!x.Id.StartsWith("custom:")&&!overrides.Any(o=>o.Id==x.Id)).Concat(overrides).OrderBy(x=>x.Name).ToList();Filter();refresh=true;Poll();}
    void ShowActivity(){using(var form=ToolForm("Live game status")){var list=new ListBox{Dock=DockStyle.Fill,BackColor=Theme.Card,ForeColor=Theme.Text,HorizontalScrollbar=true};form.Controls.Add(list);Action update=delegate{list.BeginUpdate();list.Items.Clear();foreach(var c in lastCandidates){WindowState state;string message=Exclusions.Contains(settings.ExcludedGames,c.GameId,c.GameFolder,c.Game)?"Excluded":!settings.Enabled?"Paused":SessionPaused(c.GameId)?"Paused for this session":manualHolds.ContainsKey(c.Handle)?"Manual placement held":states.TryGetValue(c.Handle,out state)?state.Status:"Detected";list.Items.Add(c.Game+" | "+message+" | "+c.Title);}if(list.Items.Count==0)list.Items.Add("No game windows detected yet.");list.EndUpdate();};var t=new System.Windows.Forms.Timer{Interval=1000};t.Tick+=delegate{update();};update();t.Start();form.ShowDialog(this);t.Stop();t.Dispose();}}
    void HotkeyDialog(){using(var form=ToolForm("Keyboard shortcuts")){var body=ToolStack(form);var on=new CheckBox{AutoSize=true,Text="Enable global hotkeys",Checked=settings.Hotkeys};body.Controls.Add(on);body.Controls.Add(new Label{Width=620,Height=190,Text="Ctrl + Alt + M   Move the focused detected game\nCtrl + Alt + P   Pause / resume automatic movement\nCtrl + Alt + R   Restore focused game's original position and pause\nCtrl + Alt + S   Save focused game's current layout\n\nExcluded games are skipped.\n\n"+hotkeyStatus});ToolButton(body,"Save",delegate{settings.Hotkeys=on.Checked;if(!Save())return;RegisterKeys();MessageBox.Show(form,hotkeyStatus);form.Close();});form.ShowDialog(this);}}
    void ExportSettings(){using(var d=new SaveFileDialog{Filter="Settings backup (*.xml)|*.xml",FileName="SteamMonitor-settings.xml"})if(d.ShowDialog()==DialogResult.OK)try{using(var file=File.Create(d.FileName))new XmlSerializer(typeof(Settings)).Serialize(file,settings);Say("Settings backup exported.");}catch(Exception ex){Say("Backup failed: "+ex.Message);}}
    public static Settings ReadBackup(string path){if(new FileInfo(path).Length>2*1024*1024)throw new InvalidDataException("Backup is larger than 2 MB.");using(var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null})){var s=(Settings)new XmlSerializer(typeof(Settings)).Deserialize(reader);NormalizeSettings(s);if(s.Profiles.Any(p=>p==null||String.IsNullOrEmpty(p.Id))||s.ExcludedGames.Any(x=>x==null)||s.CustomGames.Any(x=>x==null))throw new InvalidDataException("Backup contains incomplete records.");if(s.CustomGames.Select(x=>x.Id).Distinct().Count()!=s.CustomGames.Count)throw new InvalidDataException("Duplicate custom-game IDs.");foreach(var c in s.CustomGames){if(String.IsNullOrEmpty(c.Id)||!c.Id.StartsWith("custom:")||!Path.IsPathRooted(c.Executable)||!c.Executable.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid custom-game entry.");if(!String.IsNullOrEmpty(c.WatchFolder)&&(!Path.IsPathRooted(c.WatchFolder)||(Directory.Exists(c.WatchFolder)&&!OtherLibraries.SafeFolder(c.WatchFolder))))throw new InvalidDataException("Invalid custom game watch folder.");}return s;}}
    void ImportSettings(){using(var d=new OpenFileDialog{Filter="Settings backup (*.xml)|*.xml"})if(d.ShowDialog()==DialogResult.OK)try{var replacement=ReadBackup(d.FileName);if(MessageBox.Show(this,"Replace game profiles, monitor choices, exclusions and custom games with this backup? A copy of current settings will be retained. Only restore backups you trust. Windows startup will keep its current setting.","Restore settings",MessageBoxButtons.OKCancel)!=DialogResult.OK)return;Directory.CreateDirectory(Data);string before=Path.Combine(Data,"settings-before-import-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".xml");using(var f=File.Create(before))new XmlSerializer(typeof(Settings)).Serialize(f,settings);var previous=settings;replacement.StartupConfigured=true;settings=replacement;if(!Save()){settings=previous;return;}loading=true;enabled.Checked=settings.Enabled;border.Checked=settings.Borderless;loading=false;Displays.Invalidate();LoadDisplays();CatalogChanged();RegisterKeys();Status();Say("Backup restored. Previous settings saved to "+before);}catch(Exception ex){Say("Restore failed: "+ex.Message);}}
}
