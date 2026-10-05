using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

public static class Theme {
    public static readonly Color Background=Color.FromArgb(15,21,31),Card=Color.FromArgb(23,32,46),Raised=Color.FromArgb(32,44,61),Text=Color.FromArgb(237,242,250),Muted=Color.FromArgb(154,173,196),Accent=Color.FromArgb(84,214,233),Line=Color.FromArgb(49,66,86);
    public static GraphicsPath Round(Rectangle r,int radius){int d=radius*2;var p=new GraphicsPath();p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
}
public class ModernButton:Button {
    public bool Primary;bool hover;
    public ModernButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;ForeColor=Theme.Text;BackColor=Theme.Card;Cursor=Cursors.Hand;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);if(Width<6||Height<6)return;e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var r=new Rectangle(1,1,Width-3,Height-3);using(var path=Theme.Round(r,7)){using(var brush=new SolidBrush(!Enabled?Theme.Raised:Primary?(hover?Color.FromArgb(136,234,246):Theme.Accent):(hover?Color.FromArgb(45,62,83):Theme.Raised)))e.Graphics.FillPath(brush,path);using(var pen=new Pen(Focused?Theme.Accent:Theme.Line))e.Graphics.DrawPath(pen,path);}TextRenderer.DrawText(e.Graphics,Text,Font,r,!Enabled?Theme.Muted:Primary?Theme.Background:Theme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
}
// Use native checkbox/text controls: avoids owner-painted CheckBox repaint artifacts.
public class SettingToggle:UserControl {
    readonly CheckBox check=new CheckBox();
    readonly Label description=new Label();
    public event EventHandler CheckedChanged;
    public bool Checked {get{return check.Checked;}set{check.Checked=value;}}
    public string Description {get{return description.Text;}set{description.Text=value;}}
    public override string Text {get{return check==null?base.Text:check.Text;}set{base.Text=value;if(check!=null){check.Text=value;check.AccessibleName=value;}}}
    public SettingToggle(){
        BackColor=Theme.Card;ForeColor=Theme.Text;Height=76;Margin=new Padding(0);
        AutoScaleMode=AutoScaleMode.Inherit;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(0,8,8,6),Margin=new Padding(0),BackColor=Theme.Card};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        check.AutoSize=true;check.Dock=DockStyle.Top;check.BackColor=Theme.Card;check.ForeColor=Theme.Text;check.UseVisualStyleBackColor=false;check.FlatStyle=FlatStyle.Standard;check.Margin=new Padding(0,0,0,4);check.Cursor=Cursors.Hand;
        description.Dock=DockStyle.Fill;description.ForeColor=Theme.Muted;description.BackColor=Theme.Card;description.Margin=new Padding(20,0,0,0);description.AutoEllipsis=false;
        layout.Controls.Add(check,0,0);layout.Controls.Add(description,0,1);Controls.Add(layout);
        check.CheckedChanged+=delegate{var handler=CheckedChanged;if(handler!=null)handler(this,EventArgs.Empty);};
        description.Click+=delegate{check.Checked=!check.Checked;};
    }
}
public class BufferedPanel:Panel {public BufferedPanel(){DoubleBuffered=true;ResizeRedraw=true;}}
public class DarkMenuColors:ProfessionalColorTable {
    public override Color ToolStripDropDownBackground{get{return Theme.Card;}}
    public override Color ImageMarginGradientBegin{get{return Theme.Card;}}
    public override Color ImageMarginGradientMiddle{get{return Theme.Card;}}
    public override Color ImageMarginGradientEnd{get{return Theme.Card;}}
    public override Color MenuItemSelected{get{return Theme.Raised;}}
    public override Color MenuItemBorder{get{return Theme.Accent;}}
    public override Color MenuBorder{get{return Theme.Line;}}
}
public partial class Launcher {
    Label indicator=new Label(),emptyHint=new Label();BufferedPanel displayPreview=new BufferedPanel();Button launchButton;
    ToolTip hints=new ToolTip();
    CheckBox excludedOnly=new CheckBox();Button excludeButton;
    Icon LoadIcon(string name){try{using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name)){if(stream==null)throw new InvalidDataException("Missing icon: "+name);using(var icon=new Icon(stream,new Size(32,32)))return (Icon)icon.Clone();}}catch(Exception ex){Diagnostics.Write("Icon fallback: "+ex.Message);return (Icon)SystemIcons.Application.Clone();}}
    Bitmap LoadHeaderImage(){try{using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("LauncherHeader.png")){if(stream==null)throw new InvalidDataException("Missing header PNG");using(var image=Image.FromStream(stream))return new Bitmap(image);}}catch(Exception ex){Diagnostics.Write("Header image fallback: "+ex.Message);return new Bitmap(52,52);}}
    Label LabelText(string text,float size,bool bold){return new Label{Text=text,AutoSize=true,ForeColor=Theme.Text,Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular),Margin=new Padding(0,0,0,6)};}
    TableLayoutPanel Stack(){var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,BackColor=Theme.Card,Padding=new Padding(20),Margin=new Padding(0)};p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));return p;}
    void AddRow(TableLayoutPanel p,Control c,SizeType type,float height){int row=p.RowCount;p.RowCount++;p.RowStyles.Add(new RowStyle(type,height));c.Dock=DockStyle.Fill;p.Controls.Add(c,0,row);}
    void BuildInterface(){
        BackColor=Theme.Background;ForeColor=Theme.Text;
        var shell=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=3,BackColor=Theme.Background};
        shell.RowStyles.Add(new RowStyle(SizeType.Absolute,91));shell.RowStyles.Add(new RowStyle(SizeType.Percent,100));shell.RowStyles.Add(new RowStyle(SizeType.Absolute,83));Controls.Add(shell);
        var header=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,Margin=new Padding(0)};
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,68));header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,155));
        var logo=new PictureBox{Image=LoadHeaderImage(),SizeMode=PictureBoxSizeMode.Zoom,Size=new Size(52,52),Margin=new Padding(0,3,14,0)};header.Controls.Add(logo,0,0);
        var heading=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(0)};heading.Controls.Add(LabelText("Steam Monitor",23,true));var sub=LabelText("Your games, on the right screen.   /   v1.12.1",10,false);sub.ForeColor=Theme.Muted;heading.Controls.Add(sub);header.Controls.Add(heading,1,0);
        indicator.Dock=DockStyle.Top;indicator.Height=34;indicator.TextAlign=ContentAlignment.MiddleCenter;indicator.BackColor=Theme.Raised;indicator.Font=new Font(Font,FontStyle.Bold);indicator.Margin=new Padding(0,8,0,0);header.Controls.Add(indicator,2,0);shell.Controls.Add(header,0,0);
        var columns=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=new Padding(0)};columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,355));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,18));columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));shell.Controls.Add(columns,0,1);
        var left=Stack();left.AutoScroll=true;columns.Controls.Add(left,0,0);
        AddRow(left,LabelText("Target display",14,true),SizeType.Absolute,32);
        displayPreview.Height=110;displayPreview.Paint+=PaintDisplays;AddRow(left,displayPreview,SizeType.Absolute,110);
        monitors.DropDownStyle=ComboBoxStyle.DropDownList;monitors.FlatStyle=FlatStyle.Flat;monitors.BackColor=Theme.Raised;monitors.ForeColor=Theme.Text;monitors.AccessibleName="Target monitor for all detected games";monitors.DrawMode=DrawMode.OwnerDrawFixed;monitors.ItemHeight=26;monitors.DrawItem+=DrawDisplayItem;
        monitors.SelectedIndexChanged+=delegate{if(!loading){var d=monitors.SelectedItem as DisplayItem;if(d!=null){settings.Monitor=d.Screen.DeviceName;settings.MonitorKey=Displays.Key(d.Screen);states.Clear();Save();displayPreview.Invalidate();Status();}}};AddRow(left,monitors,SizeType.Absolute,38);
        var displayButtons=new FlowLayoutPanel{Margin=new Padding(0,7,0,8),WrapContents=false};displayButtons.Controls.Add(Btn("Identify",delegate{Identify();}));displayButtons.Controls.Add(Btn("Refresh",delegate{LoadDisplays();}));AddRow(left,displayButtons,SizeType.Absolute,52);
        var options=LabelText("Background settings",12,true);options.Margin=new Padding(0,12,0,0);AddRow(left,options,SizeType.Absolute,42);
        enabled.Text="Auto-move games";enabled.Description="Launch normally from any supported store.";enabled.Checked=settings.Enabled;enabled.CheckedChanged+=delegate{if(loading)return;settings.Enabled=enabled.Checked;if(settings.Enabled)foreach(var state in states.Values){state.First=DateTime.UtcNow;state.Last=DateTime.MinValue;state.Attempts=0;}Save();Status();};AddRow(left,enabled,SizeType.Absolute,76);
        border.Text="Borderless display";border.Description="Use Windowed mode in your game.";border.Checked=settings.Borderless;border.CheckedChanged+=delegate{if(loading)return;settings.Borderless=border.Checked;states.Clear();Save();};AddRow(left,border,SizeType.Absolute,76);
        startup.Text="Start with Windows";startup.Description="Run quietly after you sign in.";startup.Checked=StartupEnabled();startup.CheckedChanged+=delegate{if(!loading)SetStartup(startup.Checked);};AddRow(left,startup,SizeType.Absolute,76);
        var tip=LabelText("Closing this window keeps the watcher in your tray. Right-click the tray icon to exit.",9,false);tip.ForeColor=Theme.Muted;tip.AutoSize=false;tip.Margin=new Padding(0,12,0,0);AddRow(left,tip,SizeType.Absolute,78);
        var right=Stack();columns.Controls.Add(right,2,0);
        var libraryHeader=new TableLayoutPanel{ColumnCount=2,Margin=new Padding(0)};libraryHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));libraryHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,135));libraryHeader.Controls.Add(LabelText("Game library",14,true),0,0);count.Text="Scanning...";count.Dock=DockStyle.Fill;count.ForeColor=Theme.Muted;count.TextAlign=ContentAlignment.MiddleRight;libraryHeader.Controls.Add(count,1,0);AddRow(right,libraryHeader,SizeType.Absolute,36);
        // Center the native single-line editor at its preferred height. Dock=Fill
        // does not vertically center a single-line TextBox in a table cell.
        var searchWrap=new Panel{BackColor=Theme.Raised,Margin=new Padding(0,5,0,12)};
        var searchLabel=new Label{Text="Search",AutoSize=false,ForeColor=Theme.Muted,BackColor=Theme.Raised,Font=Font,TextAlign=ContentAlignment.MiddleLeft,Margin=new Padding(0)};
        search.BorderStyle=BorderStyle.None;search.BackColor=Theme.Raised;search.ForeColor=Theme.Text;search.Font=Font;search.Multiline=false;search.AutoSize=true;search.Dock=DockStyle.None;search.Margin=new Padding(0);search.AccessibleName="Search games";
        search.TextChanged+=delegate{Filter();};searchLabel.Click+=delegate{search.Focus();};searchWrap.Controls.Add(searchLabel);searchWrap.Controls.Add(search);
        Action sizeSearch=delegate{int pad=Math.Max(10,search.Font.Height/2);int labelWidth=TextRenderer.MeasureText("Search",search.Font).Width;int inputLeft=pad+labelWidth+pad;int inputHeight=search.PreferredHeight;searchLabel.Bounds=new Rectangle(pad,0,labelWidth,searchWrap.ClientSize.Height);search.Bounds=new Rectangle(inputLeft,Math.Max(0,(searchWrap.ClientSize.Height-inputHeight)/2),Math.Max(1,searchWrap.ClientSize.Width-inputLeft-pad),inputHeight);};
        searchWrap.Resize+=delegate{sizeSearch();};search.FontChanged+=delegate{sizeSearch();};AddRow(right,searchWrap,SizeType.Absolute,56);sizeSearch();
        excludedOnly.Text="Show excluded games only";excludedOnly.BackColor=Theme.Card;excludedOnly.ForeColor=Theme.Muted;excludedOnly.CheckedChanged+=delegate{Filter();};AddRow(right,excludedOnly,SizeType.Absolute,30);
        var listPanel=new Panel{Margin=new Padding(0,0,0,12)};gameList.Dock=DockStyle.Fill;gameList.IntegralHeight=false;gameList.BackColor=Theme.Card;gameList.ForeColor=Theme.Text;gameList.BorderStyle=BorderStyle.None;gameList.DrawMode=DrawMode.OwnerDrawFixed;gameList.ItemHeight=Math.Max(61,Font.Height*3+7);gameList.FontChanged+=delegate{gameList.ItemHeight=Math.Max(61,gameList.Font.Height*3+7);};gameList.DrawItem+=DrawGame;gameList.SelectedIndexChanged+=delegate{if(launchButton!=null)launchButton.Enabled=gameList.SelectedItem!=null;UpdateExclusionButton();};gameList.DoubleClick+=delegate{Launch();};listPanel.Controls.Add(gameList);emptyHint.Dock=DockStyle.Fill;emptyHint.Text="Scanning your installed game libraries...";emptyHint.ForeColor=Theme.Muted;emptyHint.TextAlign=ContentAlignment.MiddleCenter;listPanel.Controls.Add(emptyHint);emptyHint.BringToFront();AddRow(right,listPanel,SizeType.Percent,100);
        var tools=new FlowLayoutPanel{WrapContents=false,Margin=new Padding(0,0,0,10)};tools.Controls.Add(Btn("Refresh games",delegate{refresh=true;Poll();}));tools.Controls.Add(Btn("Locate Steam",delegate{using(var d=new OpenFileDialog{Filter="Steam|steam.exe",Title="Locate Steam"})if(d.ShowDialog()==DialogResult.OK){settings.Steam=d.FileName;Save();refresh=true;Poll();}}));AddRow(right,tools,SizeType.Absolute,49);
        // A plain panel owns the two button rectangles. AutoSize in a nested table
        // could make these buttons taller than their allocated row at higher DPI.
        var exclusionsBar=new Panel{Margin=new Padding(0,0,0,8),Padding=new Padding(0)};
        excludeButton=Btn("Exclude selected game",delegate{ToggleExclusion();});excludeButton.AutoSize=false;excludeButton.MinimumSize=Size.Empty;excludeButton.Margin=new Padding(0);excludeButton.Enabled=false;
        var manage=Btn("Manage exclusions",delegate{ManageExclusions();});manage.AutoSize=false;manage.MinimumSize=Size.Empty;manage.Margin=new Padding(0);
        exclusionsBar.Controls.Add(excludeButton);exclusionsBar.Controls.Add(manage);
        Action sizeExclusions=delegate{int gap=Math.Max(8,Font.Height/2);int width=Math.Max(1,(exclusionsBar.ClientSize.Width-gap)/2);int height=Math.Max(1,exclusionsBar.ClientSize.Height);excludeButton.Bounds=new Rectangle(0,0,width,height);manage.Bounds=new Rectangle(width+gap,0,Math.Max(1,exclusionsBar.ClientSize.Width-width-gap),height);};
        exclusionsBar.Resize+=delegate{sizeExclusions();};AddRow(right,exclusionsBar,SizeType.Absolute,48);sizeExclusions();
        launchButton=Btn("Launch selected game",delegate{Launch();});((ModernButton)launchButton).Primary=true;launchButton.Enabled=false;launchButton.Margin=new Padding(0,0,0,9);AddRow(right,launchButton,SizeType.Absolute,49);
        var advanced=Btn("Library / Game settings / Tools...",delegate{OpenTools();});advanced.Margin=new Padding(0,0,0,8);AddRow(right,advanced,SizeType.Absolute,46);
        var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Padding=new Padding(0,17,0,0),Margin=new Padding(0)};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,168));status.Dock=DockStyle.Fill;status.ForeColor=Theme.Muted;status.Margin=new Padding(0,0,16,0);footer.Controls.Add(status,0,0);var hide=Btn("Keep running in tray",delegate{HideToTray();});hide.Dock=DockStyle.Top;hide.Margin=new Padding(0);footer.Controls.Add(hide,1,0);shell.Controls.Add(footer,0,2);
        hints.SetToolTip(monitors,"One target monitor applies to all detected games.");hints.SetToolTip(border,"Does not override exclusive fullscreen. Use the game's Windowed setting first.");
        FormClosed+=delegate{logo.Image.Dispose();hints.Dispose();};
    }
    bool IsExcluded(Game g){return Exclusions.Contains(settings.ExcludedGames,g.Id,g.Folder,g.Name);}
    void UpdateExclusionButton(){if(excludeButton==null)return;var g=gameList.SelectedItem as Game;excludeButton.Enabled=g!=null;excludeButton.Text=g!=null&&IsExcluded(g)?"Allow selected game":"Exclude selected game";}
    void ToggleExclusion(){var g=gameList.SelectedItem as Game;if(g==null)return;bool wasExcluded=IsExcluded(g);var previous=settings.ExcludedGames.ToList();
        if(wasExcluded)settings.ExcludedGames.RemoveAll(r=>Exclusions.Matches(r,g.Id,g.Folder,g.Name));
        else settings.ExcludedGames.Add(new GameExclusion{Id=g.Id,Name=g.Name,Folder=g.Folder});
        if(!Save()){settings.ExcludedGames=previous;return;}
        foreach(var key in states.Keys.ToArray()){var state=states[key];if(Exclusions.Contains(settings.ExcludedGames,state.GameId,state.GameFolder,state.GameName))states.Remove(key);}
        Filter();gameList.Invalidate();Say(g.Name+(wasExcluded?" is allowed. Automatic movement resumes on the next scan.":" is excluded. Its current position is left unchanged."));
    }
    void ManageExclusions(){using(var dialog=new Form{Text="Excluded games",Size=new Size(650,450),StartPosition=FormStartPosition.CenterParent,BackColor=Theme.Card,ForeColor=Theme.Text,Font=Font,Icon=appIcon}){
        var list=new ListBox{Dock=DockStyle.Fill,BackColor=Theme.Card,ForeColor=Theme.Text,Font=Font,BorderStyle=BorderStyle.None};
        Action reload=delegate{list.Items.Clear();foreach(var item in settings.ExcludedGames.OrderBy(x=>x.Name))list.Items.Add(item);if(list.Items.Count>0)list.SelectedIndex=0;};reload();
        var help=new Label{Dock=DockStyle.Top,Height=64,Padding=new Padding(12),Text="Excluded games and their tracked launcher/game windows will not be moved automatically. Select an entry below to allow it again."};
        var allow=Btn("Allow selected game again",delegate{var entry=list.SelectedItem as GameExclusion;if(entry==null)return;int index=settings.ExcludedGames.IndexOf(entry);settings.ExcludedGames.Remove(entry);if(!Save()){settings.ExcludedGames.Insert(index,entry);return;}reload();Filter();gameList.Invalidate();Say(entry.Name+" is allowed again.");});allow.Dock=DockStyle.Bottom;
        dialog.Controls.Add(list);dialog.Controls.Add(help);dialog.Controls.Add(allow);dialog.ShowDialog(this);
    }}
    void ArkSetup(){var screen=Screen.AllScreens.FirstOrDefault(x=>x.DeviceName==settings.Monitor);if(screen==null){Say("Choose your target monitor first.");return;}var b=screen.WorkingArea;
        string command="-windowed -ResX="+b.Width+" -ResY="+b.Height+" -WinX="+b.X+" -WinY="+b.Y;
        using(var dialog=new Form{Text="ARK display setup",Size=new Size(640,340),StartPosition=FormStartPosition.CenterParent,BackColor=Theme.Card,ForeColor=Theme.Text,Font=Font,Icon=appIcon}){
            var text=new Label{Dock=DockStyle.Top,Height=160,Padding=new Padding(18),Text="First try ARK Settings > Video/Graphics > Windowed and apply. Then use Move a running window.\n\nOptional: close ARK, open Steam > ARK > Properties > General > Launch Options, and append the options below without deleting your existing options. These are Unreal Engine options; ARK may override some of them."};
            var input=new TextBox{Dock=DockStyle.Top,ReadOnly=true,Text=command};var copy=Btn("Copy launch options",delegate{Clipboard.SetText(command);Say("ARK launch options copied. Paste them into Steam's ARK launch options.");});copy.Dock=DockStyle.Bottom;
            dialog.Controls.Add(input);dialog.Controls.Add(text);dialog.Controls.Add(copy);dialog.ShowDialog(this);
        }
    }
    void UpdateIndicator(){if(indicator==null)return;bool connected=Displays.Resolve(settings.MonitorKey,settings.Monitor)!=null;indicator.Text=!settings.Enabled?"PAUSED":settings.Monitor.Length==0?"CHOOSE DISPLAY":!connected?"DISPLAY OFFLINE":"MONITORING";indicator.ForeColor=!settings.Enabled||!connected?Color.FromArgb(247,195,102):Theme.Accent;}
    void DrawDisplayItem(object sender,DrawItemEventArgs e){e.DrawBackground();string label="Choose your monitor...";if(e.Index>=0){var d=(DisplayItem)monitors.Items[e.Index];label="Display "+(e.Index+1)+"  |  "+d.Screen.Bounds.Width+" x "+d.Screen.Bounds.Height+(d.Screen.Primary?"  Main":"");}using(var b=new SolidBrush((e.State&DrawItemState.Selected)!=0?Theme.Raised:Theme.Card))e.Graphics.FillRectangle(b,e.Bounds);TextRenderer.DrawText(e.Graphics,label,Font,e.Bounds,Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);e.DrawFocusRectangle();}
    void DrawGame(object sender,DrawItemEventArgs e){if(e.Index<0)return;var g=(Game)gameList.Items[e.Index];bool chosen=(e.State&DrawItemState.Selected)!=0;var r=e.Bounds;int pad=Math.Max(10,r.Height/5),side=r.Height-2*pad;using(var b=new SolidBrush(chosen?Theme.Raised:Theme.Card))e.Graphics.FillRectangle(b,r);if(chosen)using(var b=new SolidBrush(Theme.Accent))e.Graphics.FillRectangle(b,r.Left,r.Top+pad,3,r.Height-2*pad);var badge=new Rectangle(r.Left+pad,r.Top+pad,side,side);using(var path=Theme.Round(badge,7))using(var b=new SolidBrush(Theme.Line))e.Graphics.FillPath(b,path);TextRenderer.DrawText(e.Graphics,g.Name.Length>0?g.Name.Substring(0,1).ToUpperInvariant():"G",Font,badge,Theme.Accent,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);int x=badge.Right+pad;using(var bold=new Font(Font,FontStyle.Bold))TextRenderer.DrawText(e.Graphics,g.Name,bold,new Rectangle(x,r.Top+4,r.Right-x-pad,r.Height/2),Theme.Text,TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);using(var small=new Font("Segoe UI",8.5f))TextRenderer.DrawText(e.Graphics,(IsExcluded(g)?"EXCLUDED  /  ":g.Source.ToUpperInvariant()+"  /  ")+(g.Id.StartsWith("custom:")?"EXE":g.Id),small,new Rectangle(x,r.Top+r.Height/2,r.Right-x-pad,r.Height/2-4),Theme.Muted,TextFormatFlags.EndEllipsis|TextFormatFlags.VerticalCenter);if((e.State&DrawItemState.Focus)!=0)e.DrawFocusRectangle();}
    void PaintDisplays(object sender,PaintEventArgs e){var screens=Screen.AllScreens;if(screens.Length==0)return;var union=screens.Select(s=>s.Bounds).Aggregate(Rectangle.Union);float scale=Math.Min((displayPreview.Width-24f)/Math.Max(1,union.Width),(displayPreview.Height-30f)/Math.Max(1,union.Height));e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;for(int i=0;i<screens.Length;i++){var s=screens[i];int x=(int)((s.Bounds.X-union.X)*scale+(displayPreview.Width-union.Width*scale)/2),y=(int)((s.Bounds.Y-union.Y)*scale+7);var r=new Rectangle(x,y,Math.Max(12,(int)(s.Bounds.Width*scale)-5),Math.Max(12,(int)(s.Bounds.Height*scale)-5));bool chosen=s.DeviceName==settings.Monitor;using(var path=Theme.Round(r,4)){using(var b=new SolidBrush(chosen?Color.FromArgb(29,72,84):Theme.Raised))e.Graphics.FillPath(b,path);using(var pen=new Pen(chosen?Theme.Accent:Theme.Line,chosen?2:1))e.Graphics.DrawPath(pen,path);}TextRenderer.DrawText(e.Graphics,(i+1).ToString(),Font,r,chosen?Theme.Accent:Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);}}
}
