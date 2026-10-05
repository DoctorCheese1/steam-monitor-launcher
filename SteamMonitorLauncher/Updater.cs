using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

public class OnlineRelease {
    public string Version {get;set;}
    public string PackageUrl {get;set;}
    public string Sha256 {get;set;}
    public string Notes {get;set;}
    public string FeedUrl;
}
public static class ReleaseClient {
    public const string Current="1.12.2";
    public const string DefaultFeed="https://github.com/DoctorCheese1/steam-monitor-launcher/releases/latest/download/latest.json";
    public static Uri Https(string url){Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!=Uri.UriSchemeHttps||!String.IsNullOrEmpty(uri.UserInfo))throw new InvalidDataException("Enter a public HTTPS release URL without embedded credentials.");return uri;}
    static HttpWebResponse Open(string url){
        ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072;
        Uri uri=Https(url);
        for(int redirects=0;redirects<=5;redirects++){
            var request=(HttpWebRequest)WebRequest.Create(uri);request.AllowAutoRedirect=false;request.Timeout=20000;request.ReadWriteTimeout=20000;request.UserAgent="SteamMonitorLauncher/"+Current;request.AutomaticDecompression=DecompressionMethods.None;request.CachePolicy=new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);
            var response=(HttpWebResponse)request.GetResponse();int code=(int)response.StatusCode;
            if(code>=300&&code<400){string next=response.Headers["Location"];response.Dispose();if(String.IsNullOrEmpty(next))throw new InvalidDataException("Update server sent an empty redirect.");uri=Https(new Uri(uri,next).AbsoluteUri);continue;}
            if(code!=200){response.Dispose();throw new InvalidDataException("Unexpected update server response: "+code);}
            return response;
        }
        throw new InvalidDataException("Too many update redirects.");
    }
    public static OnlineRelease Parse(string json,string feed){
        var item=new JavaScriptSerializer{MaxJsonLength=65536}.Deserialize<OnlineRelease>(json);
        Version version;if(item==null||!Regex.IsMatch(item.Version??"",@"^\d{1,4}\.\d{1,4}\.\d{1,4}$")||!Version.TryParse(item.Version,out version))throw new InvalidDataException("The release file has an invalid Version.");
        Https(item.PackageUrl);if(!Regex.IsMatch(item.Sha256??"","^[0-9a-fA-F]{64}$"))throw new InvalidDataException("The release file needs a SHA-256 package hash.");
        item.Notes=item.Notes??"";if(item.Notes.Length>8000)item.Notes=item.Notes.Substring(0,8000);item.FeedUrl=feed;return item;
    }
    public static OnlineRelease Check(string feed){
        using(var response=Open(feed))using(var stream=response.GetResponseStream())using(var output=new MemoryStream()){
            if(response.ContentLength>65536)throw new InvalidDataException("Release file is too large.");byte[] buffer=new byte[4096];int count;while((count=stream.Read(buffer,0,buffer.Length))>0){if(output.Length+count>65536)throw new InvalidDataException("Release file is too large.");output.Write(buffer,0,count);}return Parse(Encoding.UTF8.GetString(output.ToArray()).TrimStart('\uFEFF'),feed);
        }
    }
    public static void Download(OnlineRelease item,string destination,BackgroundWorker worker){
        try{using(var response=Open(item.PackageUrl))using(var stream=response.GetResponseStream())using(var file=File.Create(destination)){
            const long limit=50*1024*1024;if(response.ContentLength>limit)throw new InvalidDataException("Update package exceeds 50 MB.");byte[] buffer=new byte[65536];int count;long total=0;DateTime next=DateTime.MinValue;
            while((count=stream.Read(buffer,0,buffer.Length))>0){if(worker.CancellationPending)throw new OperationCanceledException("Download canceled.");total+=count;if(total>limit)throw new InvalidDataException("Update package exceeds 50 MB.");file.Write(buffer,0,count);if(DateTime.UtcNow>=next){worker.ReportProgress(response.ContentLength>0?(int)Math.Min(99,total*100/response.ContentLength):0,"Downloaded "+(total/1024)+" KB");next=DateTime.UtcNow.AddMilliseconds(250);}}
            if(response.ContentLength>=0&&total!=response.ContentLength)throw new InvalidDataException("Incomplete update download.");
        }
        if(worker.CancellationPending)throw new OperationCanceledException("Download canceled.");
        using(var file=File.OpenRead(destination))using(var sha=SHA256.Create()){string hash=BitConverter.ToString(sha.ComputeHash(file)).Replace("-","");if(!String.Equals(hash,item.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Download checksum mismatch. Installation was blocked.");}
        worker.ReportProgress(100,"Download verified. Ready to install.");
        }catch{try{File.Delete(destination);}catch{}throw;}
    }
}
public partial class Launcher {
    DateTime nextUpdateCheck=DateTime.MinValue;bool updateChecking;string notifiedVersion="";OnlineRelease availableRelease;
    void MaybeCheckUpdates(){if(settings.AutoCheckUpdates&&!String.IsNullOrWhiteSpace(settings.UpdateFeed)&&DateTime.UtcNow>=nextUpdateCheck&&!updateChecking){nextUpdateCheck=DateTime.UtcNow.AddHours(6);CheckRelease(null);}}
    void CheckRelease(Action<string,OnlineRelease> done){
        if(updateChecking){if(done!=null)done("An update check is already running. Try again shortly.",null);return;}
        string feed=(settings.UpdateFeed??"").Trim();try{ReleaseClient.Https(feed);}catch(Exception ex){if(done!=null)done(ex.Message,null);return;}
        updateChecking=true;var checker=new BackgroundWorker();checker.DoWork+=delegate(object sender,DoWorkEventArgs e){e.Result=ReleaseClient.Check(feed);};
        checker.RunWorkerCompleted+=delegate(object sender,RunWorkerCompletedEventArgs e){updateChecking=false;checker.Dispose();if(IsDisposed||exiting)return;if(feed!=(settings.UpdateFeed??"").Trim()){if(done!=null)done("Release source changed. Check again.",null);return;}
            if(e.Error!=null){Diagnostics.Write("Online update check failed: "+e.Error.Message);if(done!=null)done("Update check failed: "+e.Error.Message,null);return;}
            var release=(OnlineRelease)e.Result;bool newer=new Version(release.Version)>new Version(ReleaseClient.Current);availableRelease=newer?release:null;string message=newer?"Update "+release.Version+" is available.":"You are up to date ("+ReleaseClient.Current+").";
            if(newer&&notifiedVersion!=release.Version){notifiedVersion=release.Version;tray.ShowBalloonTip(6000,"Launcher update available",release.Version+" is ready. Open Launcher updater in the tray menu.",ToolTipIcon.Info);}
            if(done!=null)done(message,newer?release:null);
        };checker.RunWorkerAsync();
    }
    bool StartZipUpdater(string package,string expectedVersion=""){string script=Path.Combine(Data,"Update.ps1");if(!File.Exists(script)){MessageBox.Show(this,"Run Launch Steam Monitor.bat once to install the updater.");return false;}try{string shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe");Process.Start(new ProcessStartInfo(shell,"-NoLogo -NoProfile -ExecutionPolicy Bypass -File \""+script+"\" -PackagePath \""+package+"\""+(expectedVersion.Length==0?"":" -ExpectedVersion "+expectedVersion)){UseShellExecute=true});Say("Updater opened. It will check the new build before replacing the app.");return true;}catch(Exception ex){MessageBox.Show(this,ex.Message);return false;}}
    void OpenUpdater(){using(var form=ToolForm("Live launcher updater")){
        var body=ToolStack(form);body.Controls.Add(new Label{Text="Installed version: "+ReleaseClient.Current,Width=620,Height=32});
        body.Controls.Add(new Label{Text="Release feed URL (HTTPS latest.json)",Width=620,Height=24});var feed=new TextBox{Width=620,Text=settings.UpdateFeed??""};body.Controls.Add(feed);
        var auto=new CheckBox{Text="Check on startup and every 6 hours; notify when an update is ready",AutoSize=true,Checked=settings.AutoCheckUpdates};body.Controls.Add(auto);
        var info=new Label{Width=620,Height=95,Text=String.IsNullOrWhiteSpace(settings.UpdateFeed)?"No live release source configured. Enter the URL where launcher updates are published. The app cannot discover releases from this chat automatically.":"Use Check now to retrieve the published version."};body.Controls.Add(info);
        OnlineRelease selected=availableRelease;if(selected!=null)info.Text="Update "+selected.Version+" is available.\n"+selected.Notes;var download=Btn("Download and install update",delegate{});download.Width=620;download.Height=42;download.AutoSize=false;download.Enabled=selected!=null;
        Action save=delegate{string url=feed.Text.Trim();if(url.Length>0)ReleaseClient.Https(url);if(auto.Checked&&url.Length==0)throw new InvalidDataException("Enter a release URL before enabling automatic checks.");string old=settings.UpdateFeed;bool oldAuto=settings.AutoCheckUpdates;settings.UpdateFeed=url;settings.AutoCheckUpdates=auto.Checked;if(!Save()){settings.UpdateFeed=old;settings.AutoCheckUpdates=oldAuto;throw new IOException("Could not save update settings.");}if(old!=url){availableRelease=null;selected=null;download.Enabled=false;notifiedVersion="";}nextUpdateCheck=DateTime.UtcNow.AddHours(6);};
        ToolButton(body,"Save update settings",delegate{try{save();info.Text="Update settings saved.";}catch(Exception ex){info.Text=ex.Message;}});
        ToolButton(body,"Check now",delegate{try{save();download.Enabled=false;selected=null;info.Text="Checking the live release feed...";CheckRelease(delegate(string message,OnlineRelease item){if(form.IsDisposed)return;selected=item;download.Enabled=item!=null;info.Text=message+(item==null?"":"\n"+item.Notes);});}catch(Exception ex){info.Text=ex.Message;}});
        download.Click+=delegate{try{save();if(selected==null)return;DownloadRelease(selected);}catch(Exception ex){info.Text=ex.Message;}};body.Controls.Add(download);
        ToolButton(body,"Install a downloaded ZIP...",delegate{using(var picker=new OpenFileDialog{Title="Select launcher update ZIP",Filter="Launcher update|*.zip"})if(picker.ShowDialog(form)==DialogResult.OK&&StartZipUpdater(picker.FileName))form.Close();});
        ToolButton(body,"Open settings backups",delegate{string path=Path.Combine(Data,"backups");Directory.CreateDirectory(path);Process.Start("explorer.exe",path);});
        body.Controls.Add(new Label{Text="Downloads are verified against the release SHA-256 hash. Installation backs up settings and checks the new build. Automatic checks only notify; they do not interrupt a game to install.",Width=620,Height=90});form.ShowDialog(this);
    }}
    void DownloadRelease(OnlineRelease release){using(var form=ToolForm("Downloading launcher "+release.Version)){
        var body=ToolStack(form);var label=new Label{Text="Connecting...",Width=620,Height=70};body.Controls.Add(label);var progress=new ProgressBar{Width=620,Height=28};body.Controls.Add(progress);
        string folder=Path.Combine(Data,"downloads");Directory.CreateDirectory(folder);string package=Path.Combine(folder,"launcher-"+release.Version+"-"+Guid.NewGuid().ToString("N")+".zip");
        var worker=new BackgroundWorker{WorkerReportsProgress=true,WorkerSupportsCancellation=true};bool busy=true,verified=false;var install=Btn("Install verified update",delegate{if(verified&&StartZipUpdater(package,release.Version))form.Close();});install.Width=620;install.Enabled=false;body.Controls.Add(install);
        var cancel=Btn("Cancel download",delegate{if(busy){worker.CancelAsync();label.Text="Canceling download...";}else form.Close();});cancel.Width=620;body.Controls.Add(cancel);
        worker.DoWork+=delegate{ReleaseClient.Download(release,package,worker);};worker.ProgressChanged+=delegate(object sender,ProgressChangedEventArgs e){if(form.IsDisposed)return;progress.Value=e.ProgressPercentage;label.Text=Convert.ToString(e.UserState);};
        worker.RunWorkerCompleted+=delegate(object sender,RunWorkerCompletedEventArgs e){busy=false;worker.Dispose();if(form.IsDisposed)return;cancel.Text="Close";if(e.Error!=null){label.Text=e.Error.Message;return;}verified=true;progress.Value=100;label.Text="Version "+release.Version+" downloaded and SHA-256 verified. Install when you are ready.";install.Enabled=true;};
        form.FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy){worker.CancelAsync();e.Cancel=true;label.Text="Canceling... Please wait for the current network read to finish.";}};
        worker.RunWorkerAsync();form.ShowDialog(this);
    }}
}
