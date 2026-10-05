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

public static class Native {
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    public struct PROCESSENTRY32 {
        public uint dwSize,cntUsage,th32ProcessID; public UIntPtr th32DefaultHeapID;
        public uint th32ModuleID,cntThreads,th32ParentProcessID; public int pcPriClassBase; public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string szExeFile;
    }
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr CreateToolhelp32Snapshot(uint flags,uint pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool Process32FirstW(IntPtr snapshot,ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool Process32NextW(IntPtr snapshot,ref PROCESSENTRY32 entry);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    public static Dictionary<uint,uint> Parents(){
        var result=new Dictionary<uint,uint>();IntPtr snapshot=CreateToolhelp32Snapshot(2,0);
        if(snapshot==new IntPtr(-1))return result;
        try{var entry=new PROCESSENTRY32();entry.dwSize=(uint)Marshal.SizeOf(typeof(PROCESSENTRY32));
            if(Process32FirstW(snapshot,ref entry))do{result[entry.th32ProcessID]=entry.th32ParentProcessID;}while(Process32NextW(snapshot,ref entry));
        }finally{CloseHandle(snapshot);}return result;
    }
    [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool QueryFullProcessImageNameW(IntPtr process,uint flags,StringBuilder name,ref int size);
    [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetProcessTimes(IntPtr process,out long creation,out long exit,out long kernel,out long user);
    public static bool ProcessInfo(uint pid,out string path,out DateTime born){path="";born=DateTime.MinValue;IntPtr handle=OpenProcess(0x1000,false,pid);if(handle==IntPtr.Zero)return false;
        try{long c,x,k,u;if(!GetProcessTimes(handle,out c,out x,out k,out u))return false;born=DateTime.FromFileTimeUtc(c);var text=new StringBuilder(32768);int length=text.Capacity;if(QueryFullProcessImageNameW(handle,0,text,ref length))path=text.ToString();return true;}finally{CloseHandle(handle);}
    }
    public delegate bool EnumProc(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h,StringBuilder s,int count);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out RECT rect);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h,int n);
    [DllImport("user32.dll",SetLastError=true)] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")] static extern int Get32(IntPtr h,int index);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr Get64(IntPtr h,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongW",SetLastError=true)] static extern int Set32(IntPtr h,int index,int value);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)] static extern IntPtr Set64(IntPtr h,int index,IntPtr value);
    public static long Style(IntPtr h){return IntPtr.Size==8?Get64(h,-16).ToInt64():Get32(h,-16);}
    public static void Style(IntPtr h,long value){if(IntPtr.Size==8)Set64(h,-16,new IntPtr(value));else Set32(h,-16,(int)value);}
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
    public static List<WindowItem> Windows(){
        var list=new List<WindowItem>();
        EnumWindows(delegate(IntPtr h,IntPtr p){if(IsWindowVisible(h)){var s=new StringBuilder(1024);GetWindowText(h,s,s.Capacity);if(s.Length>0)list.Add(new WindowItem{Handle=h,Title=s.ToString()});}return true;},IntPtr.Zero);
        return list;
    }
    public static bool Move(IntPtr h,Screen screen,bool borderless){
        if(!IsWindow(h))return false;
        RECT r;if(!GetWindowRect(h,out r))return false;
        if(IsZoomed(h)||IsIconic(h))ShowWindowAsync(h,9);
        Rectangle b=borderless?screen.Bounds:screen.WorkingArea;
        int w=borderless?b.Width:Math.Min(b.Width,Math.Max(320,r.Right-r.Left));
        int height=borderless?b.Height:Math.Min(b.Height,Math.Max(240,r.Bottom-r.Top));
        if(borderless)Style(h,Style(h)&~0x00CF0000L);
        // No activation or Z-order changes. Async positioning avoids a hung game blocking the UI.
        return SetWindowPos(h,IntPtr.Zero,b.X+(b.Width-w)/2,b.Y+(b.Height-height)/2,w,height,0x0004|0x0010|0x0020|0x4000);
    }
}
