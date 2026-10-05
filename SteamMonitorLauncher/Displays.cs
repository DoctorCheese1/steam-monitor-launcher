using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

public class DisplayIdentity {
    public Screen Screen;public string Key="",Interface="";
    public override string ToString(){return Screen.DeviceName+" | "+Screen.Bounds.Width+" x "+Screen.Bounds.Height+(Screen.Primary?" (Main)":"");}
}
public static class Displays {
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Device {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Description;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)] public string Registry;
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool EnumDisplayDevicesW(string name,uint number,ref Device device,uint flags);
    static List<DisplayIdentity> cached=new List<DisplayIdentity>();static DateTime when=DateTime.MinValue;
    public static void Invalidate(){when=DateTime.MinValue;}
    public static List<DisplayIdentity> All(){
        if((DateTime.UtcNow-when).TotalSeconds<5)return cached;
        var list=new List<DisplayIdentity>();
        foreach(var screen in Screen.AllScreens){var item=new DisplayIdentity{Screen=screen};var d=new Device();d.cb=Marshal.SizeOf(typeof(Device));
            if(EnumDisplayDevicesW(screen.DeviceName,0,ref d,1)){
                item.Interface=d.Id??"";item.Key=item.Interface;
                // Prefer a monitor-provided serial identity when EDID exposes one.
                try{var parts=item.Interface.Split('#');if(parts.Length>=3){using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\DISPLAY\"+parts[1]+"\\"+parts[2]+@"\Device Parameters")){
                    byte[] edid=key==null?null:key.GetValue("EDID") as byte[];
                    if(edid!=null&&edid.Length>=128){uint serial=BitConverter.ToUInt32(edid,12);if(serial!=0&&serial!=UInt32.MaxValue)item.Key="EDID:"+BitConverter.ToString(edid,8,4)+":"+serial;}
                }}}catch{}
            }
            if(item.Key.Length==0)item.Key="GDI:"+screen.DeviceName;
            list.Add(item);
        }
        // Duplicate serials are not a reliable physical identity.
        var duplicateKeys=new HashSet<string>(list.GroupBy(x=>x.Key).Where(g=>g.Count()>1).Select(g=>g.Key));
        foreach(var item in list)if(duplicateKeys.Contains(item.Key))item.Key=item.Interface.Length>0?item.Interface:"GDI:"+item.Screen.DeviceName;
        cached=list;when=DateTime.UtcNow;return list;
    }
    public static DisplayIdentity Resolve(string key,string legacy){
        var list=All();if(!String.IsNullOrEmpty(key))return list.FirstOrDefault(x=>String.Equals(x.Key,key,StringComparison.OrdinalIgnoreCase));
        return list.FirstOrDefault(x=>x.Screen.DeviceName==legacy);
    }
    public static string Key(Screen screen){var item=All().FirstOrDefault(x=>x.Screen.DeviceName==screen.DeviceName);return item==null?"":item.Key;}
}
