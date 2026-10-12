using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;
using Microsoft.Win32;

[assembly: System.Reflection.AssemblyFileVersion("1.14.1.0")]
[assembly: System.Reflection.AssemblyVersion("1.14.1.0")]

public class Profile {
    public string Id="", Monitor="", MonitorKey=""; public bool Borderless, Override, UseLayout;
    public int X,Y,Width,Height;
}
public class Settings {
    public bool SetupCompleted;public List<MonitorPreset> MonitorPresets=new List<MonitorPreset>();
    public string UpdateFeed=ReleaseClient.DefaultFeed;public bool AutoCheckUpdates=true;public bool GitHubFeedConfigured;
    public List<GameOptions> GameOptions=new List<GameOptions>(); public bool FallbackEnabled;public string FallbackKey="";
    public string Steam="", Monitor="", MonitorKey="";
    public bool Hotkeys=true;public List<CustomGame> CustomGames=new List<CustomGame>(); public bool Borderless, StartupConfigured;
    public List<GameExclusion> ExcludedGames=new List<GameExclusion>();
    public bool Enabled=true; public List<Profile> Profiles=new List<Profile>();
}
public class DisplayItem {
    public Screen Screen;
    public override string ToString(){var b=Screen.Bounds;return Screen.DeviceName+" | "+b.Width+" x "+b.Height+(Screen.Primary?" (Primary)":"");}
}
public class WindowItem { public IntPtr Handle; public string Title; public override string ToString(){return Title;} }
public class WindowState {
    public string GameId="",GameFolder="",GameName="";
    public uint Pid; public DateTime Born, First=DateTime.UtcNow, Last=DateTime.MinValue;
    public string Status="Detected"; public bool Pending;public Rectangle Expected;
    public Native.RECT LastObserved;public DateTime StableSince=DateTime.UtcNow;
    public bool Placed;public DateTime Observed=DateTime.UtcNow;
    public int Attempts; public long Style; public Native.RECT Rect;
}
public class PollRequest { public string Steam; public bool Refresh;public int Revision;public List<Game> Custom; }
public partial class Launcher:Form {
    static readonly string Data=AppPaths.Data;
    static readonly string Config=Path.Combine(Data,"settings.xml");
    const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName="SteamMonitorLauncher";
    static readonly int ShowMessage=RegisterWindowMessage("SteamMonitorLauncher.ShowSettings.v1"+AppPaths.InstanceSuffix);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int RegisterWindowMessage(string name);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h,int msg,IntPtr w,IntPtr l);
    Settings settings=new Settings(); Watcher watcher=new Watcher();int revision;List<Candidate> lastCandidates=new List<Candidate>();
    BackgroundWorker worker=new BackgroundWorker(); System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    NotifyIcon tray; ToolStripMenuItem pauseMenu;
    ComboBox monitors=new ComboBox();SettingToggle enabled=new SettingToggle(),border=new SettingToggle(),startup=new SettingToggle();
    Label status=new Label(),count=new Label();TextBox search=new TextBox();ListBox gameList=new ListBox();
    List<Game> games=new List<Game>();Dictionary<IntPtr,WindowState> states=new Dictionary<IntPtr,WindowState>();
    bool recoveryNeeded;
    bool loading, exiting, refresh=true, background, hiddenOnce;
    Icon appIcon,pausedIcon;
    [STAThread] public static void Main(string[] args){
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        if(args.Contains("--self-test")){Environment.ExitCode=Diagnostics.StartupCheck();return;}
        string installed=Path.Combine(Data,@"bin\SteamMonitorLauncher.exe");
        string setup=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Install.ps1");
        if(!AppPaths.Portable&&File.Exists(setup)&&!String.Equals(Path.GetFullPath(Application.ExecutablePath),Path.GetFullPath(installed),StringComparison.OrdinalIgnoreCase)){
            try{Process.Start(new ProcessStartInfo("powershell.exe","-NoLogo -NoProfile -ExecutionPolicy Bypass -File \""+setup+"\""){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(ex.Message,"Installation could not start");}return;
        }
        if(!AppPaths.Portable&&File.Exists(installed)&&!String.Equals(Path.GetFullPath(Application.ExecutablePath),Path.GetFullPath(installed),StringComparison.OrdinalIgnoreCase)){
            Process.Start(new ProcessStartInfo(installed,args.Contains("--background")?"--background":""){UseShellExecute=false});return;
        }
        Application.ThreadException+=delegate(object sender,ThreadExceptionEventArgs e){Diagnostics.Write(e.Exception.ToString());MessageBox.Show("The launcher encountered an error. Open Logs.bat to view watcher.log.\n\n"+e.Exception.Message,"Steam Monitor Launcher");};
        bool first;
        using(var mutex=new Mutex(true,@"Local\SteamMonitorLauncher.Background.v1"+AppPaths.InstanceSuffix,out first)){
            if(!first){if(!args.Contains("--background"))PostMessage(new IntPtr(0xffff),ShowMessage,IntPtr.Zero,IntPtr.Zero);return;}
            try{Application.Run(new Launcher(args.Contains("--background")));}
            catch(Exception ex){Diagnostics.Write(ex.ToString());MessageBox.Show(ex.Message,"Steam Monitor Launcher",MessageBoxButtons.OK,MessageBoxIcon.Error);Environment.ExitCode=1;}
            finally{mutex.ReleaseMutex();}
        }
    }
    protected override void WndProc(ref Message m){if(m.Msg==0x312){HandleHotkey(m.WParam.ToInt32());return;}if(m.Msg==0x7e){Displays.Invalidate();states.Clear();}if(m.Msg==ShowMessage){OpenSettings();return;}base.WndProc(ref m);}
    public Launcher(bool startHidden,bool selfTest=false){
        background=startHidden;
        Text="Steam Monitor Launcher 1.14.1";Size=new Size(1080,840);MinimumSize=new Size(980,760);StartPosition=FormStartPosition.CenterScreen;
        Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(20,26,35);ForeColor=Color.FromArgb(233,239,248);AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        try{if(!selfTest&&File.Exists(Config))using(var f=File.OpenRead(Config))settings=(Settings)new XmlSerializer(typeof(Settings)).Deserialize(f);}catch{settings=new Settings();recoveryNeeded=true;}
        NormalizeSettings(settings);
        if(!selfTest&&File.Exists(Config)&&!String.IsNullOrEmpty(settings.Monitor))settings.SetupCompleted=true;
        appIcon=LoadIcon("Launcher.ico");pausedIcon=LoadIcon("LauncherPaused.ico");Icon=appIcon;
        BuildInterface();
        var menu=new ContextMenuStrip();menu.Items.Add("Open settings",null,delegate{OpenSettings();});pauseMenu=new ToolStripMenuItem("Pause monitoring",null,delegate{enabled.Checked=!enabled.Checked;});menu.Items.Add(pauseMenu);menu.Items.Add("Restore moved windows and pause",null,delegate{Restore();});menu.Items.Add("Disable Windows startup",null,delegate{SetStartup(false);});menu.Items.Add("Game library / controller view",null,delegate{OpenLibraryView();});menu.Items.Add("Launcher updater...",null,delegate{OpenUpdater();});menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Exit",null,delegate{ExitApp();});
        tray=new NotifyIcon{Icon=appIcon,Text="Steam Monitor Launcher",Visible=!selfTest,ContextMenuStrip=menu};tray.DoubleClick+=delegate{OpenSettings();};menu.Renderer=new ToolStripProfessionalRenderer(new DarkMenuColors());menu.BackColor=Theme.Card;menu.ForeColor=Theme.Text;
        worker.DoWork+=delegate(object sender,DoWorkEventArgs e){var request=(PollRequest)e.Argument;var result=watcher.Poll(request.Steam,request.Refresh,request.Custom);result.Revision=request.Revision;e.Result=result;};
        worker.RunWorkerCompleted+=Completed;
        timer.Interval=2000;timer.Tick+=delegate{Poll();MaybeCheckUpdates();};
        FormClosing+=delegate(object sender,FormClosingEventArgs e){if(!exiting&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;HideToTray();}};
        FormClosed+=delegate{ReleaseHotkeys();timer.Stop();timer.Dispose();tray.Visible=false;tray.Dispose();appIcon.Dispose();pausedIcon.Dispose();};
        Resize+=delegate{if(WindowState==FormWindowState.Minimized)HideToTray();};
        Shown+=delegate{
            if(selfTest)return;
            if(!selfTest&&recoveryNeeded){recoveryNeeded=false;MessageBox.Show(this,"Saved settings could not be read. Choose an automatic backup to recover your preferences.","Settings recovery");RecoverSettings();}
            if(!settings.SetupCompleted)SetupWizard();
            if(settings.SetupCompleted&&!AppPaths.Portable&&(!settings.StartupConfigured||StartupEnabled())){if(SetStartup(true)){settings.StartupConfigured=true;Save();}}
            if(background&&settings.Monitor.Length>0)HideToTray();
            if(!selfTest)RegisterKeys();timer.Start();Poll();
        };
        LoadDisplays();Status();
        if(!selfTest&&background&&settings.Monitor.Length>0){WindowState=FormWindowState.Minimized;ShowInTaskbar=false;}
    }
    Button Btn(string title,EventHandler action){var b=new ModernButton{Text=title,AutoSize=true,MinimumSize=new Size(90,38),Margin=new Padding(0,0,8,0),Padding=new Padding(10,4,10,4)};b.Click+=action;return b;}
    void Say(string s){if(status.Text!=s)Diagnostics.Write(s);status.Text=s;UpdateIndicator();}
    void Status(){if(tray==null)return;pauseMenu.Text=settings.Enabled?"Pause monitoring":"Resume monitoring";tray.Text=settings.Enabled?"Steam Monitor Launcher - monitoring":"Steam Monitor Launcher - paused";tray.Icon=settings.Enabled?appIcon:pausedIcon;Say(!settings.Enabled?"Monitoring paused. Your games stay where they are.":settings.Monitor.Length==0?"Choose a target display to finish setup.":"Watching for games. Close this window to keep monitoring in the tray.");}
    bool Save(){try{Directory.CreateDirectory(Data);string temp=Config+".tmp";using(var f=File.Create(temp))new XmlSerializer(typeof(Settings)).Serialize(f,settings);if(File.Exists(Config)){string backupDir=Path.Combine(Data,"backups");Directory.CreateDirectory(backupDir);string backup=Path.Combine(backupDir,"settings-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+".xml");File.Replace(temp,Config,backup);try{foreach(string old in Directory.GetFiles(backupDir,"settings-*.xml").Where(x=>System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(x),@"^settings-\d")).OrderByDescending(x=>x).Skip(20))File.Delete(old);}catch{}}else File.Move(temp,Config);return true;}catch(Exception ex){Say("Could not save settings: "+ex.Message);return false;}}
    bool StartupEnabled(){try{using(var key=Registry.CurrentUser.OpenSubKey(RunKey))return key!=null&&Convert.ToString(key.GetValue(RunName)).IndexOf("\""+Application.ExecutablePath+"\"",StringComparison.OrdinalIgnoreCase)>=0;}catch{return false;}}
    bool SetStartup(bool on){try{using(var key=Registry.CurrentUser.CreateSubKey(RunKey)){if(on)key.SetValue(RunName,"\""+Application.ExecutablePath+"\" --background",RegistryValueKind.String);else key.DeleteValue(RunName,false);}loading=true;startup.Checked=on;loading=false;settings.StartupConfigured=true;Save();Say(on?"Windows startup enabled for your account.":"Windows startup disabled. The app keeps running until you exit.");return true;}catch(Exception ex){loading=true;startup.Checked=StartupEnabled();loading=false;Say("Could not change Windows startup: "+ex.Message);return false;}}
    void OpenSettings(){Show();ShowInTaskbar=true;WindowState=FormWindowState.Normal;Activate();LoadDisplays();}
    void HideToTray(){Hide();ShowInTaskbar=false;if(!hiddenOnce){tray.ShowBalloonTip(2500,"Steam Monitor Launcher","Running in the background. Right-click this icon for settings or Exit.",ToolTipIcon.Info);hiddenOnce=true;}}
    void ExitApp(){exiting=true;Close();}
    void LoadDisplays(){loading=true;monitors.Items.Clear();var resolved=Displays.Resolve(settings.MonitorKey,settings.Monitor);if(resolved!=null){settings.Monitor=resolved.Screen.DeviceName;if(String.IsNullOrEmpty(settings.MonitorKey)){settings.MonitorKey=resolved.Key;Save();}}foreach(var screen in Screen.AllScreens)monitors.Items.Add(new DisplayItem{Screen=screen});monitors.SelectedIndex=-1;for(int i=0;i<monitors.Items.Count;i++)if(((DisplayItem)monitors.Items[i]).Screen.DeviceName==settings.Monitor)monitors.SelectedIndex=i;loading=false;displayPreview.Invalidate();UpdateIndicator();if(settings.Monitor.Length>0&&monitors.SelectedIndex<0)Say(settings.FallbackEnabled?"Selected display is disconnected; fallback is enabled.":"Selected display is disconnected. Monitoring waits for it to return; choose another display to switch.");}
    void Identify(){int n=0;foreach(var screen in Screen.AllScreens){n++;var f=new Form{FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(screen.Bounds.X+40,screen.Bounds.Y+40,370,125),TopMost=true,BackColor=Color.FromArgb(30,90,140),ForeColor=Color.White};f.Controls.Add(new Label{Dock=DockStyle.Fill,Text="Display "+n+"\n"+screen.DeviceName,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",22,FontStyle.Bold)});var t=new System.Windows.Forms.Timer{Interval=2500};t.Tick+=delegate{t.Stop();f.Close();t.Dispose();};f.Show();t.Start();}}
    void Filter(){string id=gameList.SelectedItem==null?"":((Game)gameList.SelectedItem).Id;gameList.BeginUpdate();gameList.Items.Clear();foreach(var g in games)if((g.Name+" "+g.Source).IndexOf(search.Text,StringComparison.CurrentCultureIgnoreCase)>=0&&(!excludedOnly.Checked||IsExcluded(g)))gameList.Items.Add(g);for(int i=0;i<gameList.Items.Count;i++)if(((Game)gameList.Items[i]).Id==id)gameList.SelectedIndex=i;if(gameList.SelectedIndex<0&&gameList.Items.Count>0)gameList.SelectedIndex=0;gameList.EndUpdate();count.Text=gameList.Items.Count+" / "+games.Count+" games";emptyHint.Visible=gameList.Items.Count==0;emptyHint.Text=games.Count==0?"Your installed games will appear here.\nUse Tools > Library management to add another game.":"No games match this filter.";launchButton.Enabled=gameList.SelectedItem!=null;UpdateExclusionButton();}
    void Launch(){LaunchGame(gameList.SelectedItem as Game);}
    void LaunchGame(Game g){if(g==null)return;try{var launch=Options(g.Id);if(launch.LaunchMode=="Default"&&g.Source!="Steam"&&String.IsNullOrEmpty(g.LaunchUri)&&!File.Exists(g.Executable)){if(!FindExecutable(g,false))return;launch=Options(g.Id);}if(launch.LaunchMode=="Executable"){Process.Start(new ProcessStartInfo(launch.LaunchPath,launch.Arguments??""){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(launch.LaunchPath)});}else if(launch.LaunchMode=="Shortcut"){Process.Start(new ProcessStartInfo(launch.LaunchPath){UseShellExecute=true});}else if(!String.IsNullOrEmpty(g.LaunchUri))Process.Start(new ProcessStartInfo(g.LaunchUri){UseShellExecute=true});else if(!String.IsNullOrEmpty(g.Executable))Process.Start(new ProcessStartInfo(g.Executable,g.Arguments??""){WorkingDirectory=Path.GetDirectoryName(g.Executable),UseShellExecute=false});else if(g.Source!="Steam"){Say("Start this game from "+g.Source+". The background watcher will move its window.");return;}else Process.Start(new ProcessStartInfo(settings.Steam,"-applaunch "+g.Id){UseShellExecute=false});launchWaiting[g.Id]=DateTime.UtcNow;RememberPlayed(g.Id);gameList.Invalidate();Say("Launching "+g.Name+". "+(IsExcluded(g)?"Excluded: automatic movement is disabled for this game.":settings.Enabled?"Background watcher is active.":"Monitoring is paused."));}catch(Exception ex){Say(ex.Message);}}
    void Poll(){if(worker.IsBusy||exiting)return;bool force=refresh;refresh=false;worker.RunWorkerAsync(new PollRequest{Steam=settings.Steam,Refresh=force,Revision=revision,Custom=CustomCatalog()});}
    void Completed(object sender,RunWorkerCompletedEventArgs e){
        if(exiting||IsDisposed)return;if(e.Error!=null){Say("Monitor scan failed; will retry. "+e.Error.Message);return;}
        var result=(WatchResult)e.Result;if(result.Revision!=revision)return;UpdateSessionState(result);lastCandidates=result.Windows;foreach(var found in result.Windows)launchWaiting.Remove(found.GameId);TrackSessions(result.Windows);
        if(settings.Steam!=result.Steam&&File.Exists(result.Steam)){settings.Steam=result.Steam;Save();}
        if(!games.Select(g=>g.Id+g.Name+g.Folder+g.Executable+g.Arguments).SequenceEqual(result.Games.Select(g=>g.Id+g.Name+g.Folder+g.Executable+g.Arguments))){games=result.Games;Filter();}
        if(games.Count==0){Say("No installed games found. Add games in Tools or locate Steam.");}
        gameList.Invalidate();
        if(!settings.Enabled)return;
        var present=new HashSet<IntPtr>(result.Windows.Select(w=>w.Handle));
        foreach(var h in states.Keys.ToArray())if(!present.Contains(h))states.Remove(h);
        foreach(var window in result.Windows){
            if(SessionPaused(window.GameId))continue;ManualPlacement hold;if(manualHolds.TryGetValue(window.Handle,out hold)&&hold.Alive())continue;
            if(Exclusions.Contains(settings.ExcludedGames,window.GameId,window.GameFolder,window.Game)){states.Remove(window.Handle);continue;}
            uint currentPid;Native.GetWindowThreadProcessId(window.Handle,out currentPid);
            if(!Native.IsWindow(window.Handle)||currentPid!=window.Pid)continue;
            WindowState state;
            if(!states.TryGetValue(window.Handle,out state)||state.Pid!=window.Pid||state.Born!=window.Born){
                Native.RECT original;if(!Native.GetWindowRect(window.Handle,out original))continue;
                state=new WindowState{GameId=window.GameId,GameFolder=window.GameFolder,GameName=window.Game,Pid=window.Pid,Born=window.Born,Style=Native.Style(window.Handle),Rect=original};states[window.Handle]=state;
            }
            var profile=settings.Profiles.FirstOrDefault(p=>p.Id==window.GameId&&p.Override);
            var display=ResolveTarget(profile==null?settings.MonitorKey:profile.MonitorKey,profile==null?settings.Monitor:profile.Monitor);
            if(display==null){SetGameStatus(window,state,"Waiting for selected monitor");state.Pending=false;state.First=DateTime.UtcNow;state.Attempts=0;continue;}
            var movement=Options(window.GameId);
            Native.RECT observed;if(Native.GetWindowRect(window.Handle,out observed)){
                if(!RectClose(observed,Rectangle.FromLTRB(state.LastObserved.Left,state.LastObserved.Top,state.LastObserved.Right,state.LastObserved.Bottom))){state.LastObserved=observed;state.StableSince=DateTime.UtcNow;}
                if(!state.Placed&&(DateTime.UtcNow-state.Observed).TotalSeconds<20&&(DateTime.UtcNow-state.StableSince).TotalSeconds<2){SetGameStatus(window,state,"Waiting for startup window to settle");continue;}
            }
            if(movement.StopAfterPlacement&&state.Placed){SetGameStatus(window,state,"Placement complete; manual movement allowed");continue;}
            if((DateTime.UtcNow-state.Observed).TotalSeconds<movement.DelaySeconds){SetGameStatus(window,state,"Waiting for startup delay");continue;}
            var screen=display.Screen;bool useBorder=profile==null?settings.Borderless:profile.Borderless;
            if(Native.IsIconic(window.Handle))continue; // Never undo a user's minimize action.
            DateTime now=DateTime.UtcNow;
            Native.RECT rect;if(!Native.GetWindowRect(window.Handle,out rect)){SetGameStatus(window,state,"Cannot read window position");continue;}
            if(state.Pending){if(RectClose(rect,state.Expected)){SetGameStatus(window,state,"Moved and verified");state.Pending=false;state.Placed=true;}
                else if((now-state.Last).TotalSeconds>=5){SetGameStatus(window,state,"Movement not confirmed / game reset its window");state.Pending=false;}}
            bool ark=window.Game.StartsWith("ARK",StringComparison.OrdinalIgnoreCase);
            var expected=DesiredBounds(rect,screen,useBorder,profile);
            bool already=profile!=null&&profile.UseLayout||useBorder?RectClose(rect,expected):screen.Bounds.Contains(rect.Left+(rect.Right-rect.Left)/2,rect.Top+(rect.Bottom-rect.Top)/2);
            if(already&&(!useBorder||(Native.Style(window.Handle)&0x00CF0000L)==0)){state.Placed=true;if(state.Attempts==0)SetGameStatus(window,state,"Already on target");continue;}
            if((now-state.First).TotalSeconds-movement.DelaySeconds>(ark?900:120)||state.Attempts>=(ark?180:24)){SetGameStatus(window,state,"Window is off target; retry period ended");continue;}
            if((now-state.Last).TotalSeconds<5)continue;
            state.Last=now;state.Attempts++;state.Expected=expected;
            bool ok=ApplyPosition(window.Handle,expected,useBorder);state.Pending=ok;
            SetGameStatus(window,state,ok?"Move sent; checking result...":"Windows rejected movement (error "+Marshal.GetLastWin32Error()+")");

        }
    }
    void ManualMove(){using(var form=CreateWindowPicker())form.ShowDialog(this);}
    internal Form CreateWindowPicker(){
        var form=new Form{Text="Move a running window",Size=new Size(850,680),MinimumSize=new Size(760,600),StartPosition=FormStartPosition.CenterParent,BackColor=Theme.Card,ForeColor=Theme.Text,Icon=appIcon,Font=Font};
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),ColumnCount=1,RowCount=6};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        foreach(int height in new[]{50,42,38})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,65));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,110));form.Controls.Add(layout);
        layout.Controls.Add(new Label{Text="Choose a display, then search for a window or use Pick window by click. Move, remember its monitor, or undo without closing this picker.",Dock=DockStyle.Fill},0,0);
        var destination=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,BackColor=Theme.Raised,ForeColor=Theme.Text};layout.Controls.Add(destination,0,1);
        var query=new TextBox{Dock=DockStyle.Fill,AccessibleName="Search running windows",BackColor=Theme.Raised,ForeColor=Theme.Text};NativeSearchCue(query,"Search window titles...");layout.Controls.Add(query,0,2);
        var list=new ListBox{Dock=DockStyle.Fill,IntegralHeight=false,HorizontalScrollbar=true,BackColor=Theme.Raised,ForeColor=Theme.Text,BorderStyle=BorderStyle.None};layout.Controls.Add(list,0,3);
        var feedback=new Label{Dock=DockStyle.Fill,Padding=new Padding(0,8,0,0),ForeColor=Theme.Muted};layout.Controls.Add(feedback,0,4);
        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};layout.Controls.Add(buttons,0,5);
        var move=Btn("Move selected window",delegate{});((ModernButton)move).Primary=true;
        var undo=Btn("Undo last move",delegate{feedback.Text=UndoManualMove();Say(feedback.Text);});var remember=Btn("Remember monitor",delegate{RememberManualMonitor();feedback.Text=status.Text;});
        var identities=new Dictionary<IntPtr,uint>();var all=new List<WindowItem>();
        Action updateEnabled=delegate{move.Enabled=list.SelectedItem!=null&&destination.SelectedItem!=null;undo.Enabled=remember.Enabled=lastManual!=null&&lastManual.Alive();};
        Action filter=delegate{var selected=list.SelectedItem as WindowItem;list.BeginUpdate();list.Items.Clear();foreach(var window in all.Where(w=>w.Title.IndexOf(query.Text.Trim(),StringComparison.CurrentCultureIgnoreCase)>=0)){list.Items.Add(window);if(selected!=null&&selected.Handle==window.Handle)list.SelectedItem=window;}list.EndUpdate();updateEnabled();};
        Action reload=delegate{
            var chosen=destination.SelectedItem as DisplayItem;string device=chosen==null?settings.Monitor:chosen.Screen.DeviceName;destination.Items.Clear();foreach(var screen in Screen.AllScreens)destination.Items.Add(new DisplayItem{Screen=screen});for(int i=0;i<destination.Items.Count;i++)if(((DisplayItem)destination.Items[i]).Screen.DeviceName==device)destination.SelectedIndex=i;
            all.Clear();identities.Clear();foreach(var window in Native.Windows()){uint pid;Native.GetWindowThreadProcessId(window.Handle,out pid);if(pid==(uint)Process.GetCurrentProcess().Id)continue;all.Add(window);identities[window.Handle]=pid;}filter();feedback.Text=all.Count==0?"No visible windows found. Open a game and refresh.":"Select a window, or click Pick window by click. Escape cancels picking.";
        };
        move.Click+=delegate{var window=list.SelectedItem as WindowItem;var choice=destination.SelectedItem as DisplayItem;if(window==null||choice==null)return;var screen=Screen.AllScreens.FirstOrDefault(x=>x.DeviceName==choice.Screen.DeviceName);if(screen==null){reload();feedback.Text="That display disconnected. Choose a connected display.";return;}uint pid,original;Native.GetWindowThreadProcessId(window.Handle,out pid);if(!Native.IsWindow(window.Handle)||!identities.TryGetValue(window.Handle,out original)||pid!=original){reload();feedback.Text="That window closed or changed. Select its replacement.";return;}feedback.Text=MoveManually(window.Handle,screen);Say(feedback.Text);updateEnabled();};
        WindowClickCapture capture=null;bool mainVisible=false;
        var pick=Btn("Pick window by click",delegate{if(capture!=null)return;try{mainVisible=Visible;capture=new WindowClickCapture(form,delegate(IntPtr selected){capture=null;if(mainVisible)Opacity=1;if(form.IsDisposed)return;form.Opacity=1;form.Activate();query.Clear();reload();if(selected==IntPtr.Zero){feedback.Text="Picking canceled. No window was moved.";return;}foreach(WindowItem item in list.Items)if(item.Handle==selected){list.SelectedItem=item;feedback.Text="Window selected. Choose a display and click Move selected window.";return;}feedback.Text="That window is not available. Refresh or pick another window.";});form.Opacity=0;if(mainVisible)Opacity=0;}catch(Exception ex){if(capture!=null){capture.Dispose();capture=null;}if(mainVisible)Opacity=1;form.Opacity=1;feedback.Text="Could not start picking: "+ex.Message;}});
        buttons.Controls.Add(move);buttons.Controls.Add(pick);buttons.Controls.Add(Btn("Refresh windows",delegate{reload();}));buttons.Controls.Add(remember);buttons.Controls.Add(undo);
        buttons.Controls.Add(Btn("Resume auto-move",delegate{var w=list.SelectedItem as WindowItem;if(w==null)return;ManualPlacement hold;if(manualHolds.TryGetValue(w.Handle,out hold)){manualHolds.Remove(w.Handle);states.Remove(w.Handle);if(!String.IsNullOrEmpty(hold.GameId))pausedGames.Remove(hold.GameId);}feedback.Text="Manual hold cleared for the selected window. Existing exclusions still apply.";}));
        var done=Btn("Done",delegate{form.Close();});buttons.Controls.Add(done);form.CancelButton=done;
        undo.Click+=delegate{updateEnabled();};remember.Click+=delegate{updateEnabled();};form.FormClosed+=delegate{if(capture!=null){capture.Dispose();capture=null;if(mainVisible&&!IsDisposed)Opacity=1;}};
        query.TextChanged+=delegate{filter();};list.SelectedIndexChanged+=delegate{updateEnabled();};destination.SelectedIndexChanged+=delegate{updateEnabled();};reload();return form;
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr wparam,string lparam);
    void NativeSearchCue(TextBox input,string cue){input.HandleCreated+=delegate{SendMessage(input.Handle,0x1501,IntPtr.Zero,cue);};}
    void Restore(){var saved=states.ToArray();enabled.Checked=false;int countRestored=0;foreach(var pair in saved){if(pair.Value.Attempts==0||Exclusions.Contains(settings.ExcludedGames,pair.Value.GameId,pair.Value.GameFolder,pair.Value.GameName))continue;uint pid;Native.GetWindowThreadProcessId(pair.Key,out pid);if(pid!=pair.Value.Pid||!Native.IsWindow(pair.Key))continue;try{using(var process=Process.GetProcessById((int)pid))if(process.StartTime.ToUniversalTime()!=pair.Value.Born)continue;}catch{continue;}Native.Style(pair.Key,pair.Value.Style);var r=pair.Value.Rect;if(Native.SetWindowPos(pair.Key,IntPtr.Zero,r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top,0x0004|0x0010|0x0020|0x4000))countRestored++;}states.Clear();Say("Monitoring paused. Restore requested for "+countRestored+" window(s).");}
}
