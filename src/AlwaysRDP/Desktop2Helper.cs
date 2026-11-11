// Copyright (c) 2026 ryzyder. All rights reserved.
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("AlwaysRDP Desktop 2")]
[assembly: System.Reflection.AssemblyVersion("1.0.1.0")]
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.ApplicationDirectory | DllImportSearchPath.System32)]

internal static class Desktop2Helper {
#if TEST_PACKAGE
    const string ProductFamily="Zadark.AlwaysRDP.Test_ccj8dd7zr7baw";
#else
    const string ProductFamily="Zadark.AlwaysRDP_ca496q8ba4sre";
#endif
    const string StopEvent="Local\\ZadarkAlwaysRDPDesktop2StopV1";
    const string MutexName="Local\\ZadarkAlwaysRDPDesktop2V1";
    const int TargetDesktop=1;

    [DllImport("VirtualDesktopAccessor.dll")] static extern int GetDesktopCount();
    [DllImport("VirtualDesktopAccessor.dll")] static extern int CreateDesktop();
    [DllImport("VirtualDesktopAccessor.dll")] static extern int GetWindowDesktopNumber(IntPtr window);
    [DllImport("VirtualDesktopAccessor.dll")] static extern int MoveWindowToDesktopNumber(IntPtr window,int desktop);
    [DllImport("user32.dll")] static extern bool EnumWindows(WindowCallback callback,IntPtr data);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access,bool inherit,uint process);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")] static extern bool ProcessIdToSessionId(uint process,out uint session);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] static extern int GetPackageFamilyName(IntPtr process,ref uint length,StringBuilder name);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process,int flags,StringBuilder name,ref int length);
    [DllImport("kernel32.dll")] static extern IntPtr CreateToolhelp32Snapshot(uint flags,uint process);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,EntryPoint="Process32FirstW")] static extern bool Process32First(IntPtr snapshot,ref ProcessEntry entry);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,EntryPoint="Process32NextW")] static extern bool Process32Next(IntPtr snapshot,ref ProcessEntry entry);

    delegate bool WindowCallback(IntPtr window,IntPtr state);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct ProcessEntry {
        internal uint Size,Usage,Id;internal UIntPtr Heap;internal uint Module,Threads,ParentId;internal int Priority;internal uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] internal string Name;
    }

    [STAThread] static int Main(string[] args) {
        if(Array.IndexOf(args,"/stop")>=0) {
            try {using(var stop=EventWaitHandle.OpenExisting(StopEvent)) stop.Set();} catch(WaitHandleCannotBeOpenedException) {}
            return 0;
        }
        try {
            CheckWindows();
            bool first;
            using(var mutex=new Mutex(true,MutexName,out first)) {
                if(!first)return 0;
                using(var stop=new EventWaitHandle(false,EventResetMode.ManualReset,StopEvent)) {
                    var app=new HelperContext(stop);
                    Application.Run(app);
                }
                mutex.ReleaseMutex();
            }
            return 0;
        } catch(Exception error) {
            MessageBox.Show(error.Message,"AlwaysRDP Desktop 2",MessageBoxButtons.OK,MessageBoxIcon.Information);
            return 1;
        }
    }

    static void CheckWindows() {
        using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")) {
            int build;
            if(key==null || !int.TryParse(Convert.ToString(key.GetValue("CurrentBuildNumber")),out build)
                || !((build==26100 && Convert.ToInt32(key.GetValue("UBR",0))>=2605) || build==26200))
                throw new InvalidOperationException("AlwaysRDP Desktop 2 requiere Windows 11 24H2 (26100.2605+) o 25H2.");
        }
        if(GetDesktopCount()<1)throw new InvalidOperationException("Windows no permite acceder a los escritorios virtuales en esta sesión.");
    }

    static bool IsAlwaysRdp(uint process) {
        IntPtr handle=OpenProcess(0x1000,false,process);
        if(handle==IntPtr.Zero)return false;
        try {
#if PORTABLE_BUILD
            int imageLength=1024;var image=new StringBuilder(imageLength);
            return QueryFullProcessImageName(handle,0,image,ref imageLength)
                && string.Equals(image.ToString(),Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"AlwaysRDP.exe"),StringComparison.OrdinalIgnoreCase);
#else
            uint length=128;var name=new StringBuilder((int)length);
            return GetPackageFamilyName(handle,ref length,name)==0 && string.Equals(name.ToString(),ProductFamily,StringComparison.OrdinalIgnoreCase);
#endif
        } finally {CloseHandle(handle);}
    }

    static bool IsMstsc(uint process) {
        IntPtr handle=OpenProcess(0x1000,false,process);
        if(handle==IntPtr.Zero)return false;
        try {
            int length=1024;var path=new StringBuilder(length);
            return QueryFullProcessImageName(handle,0,path,ref length)
                && string.Equals(path.ToString(),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"mstsc.exe"),StringComparison.OrdinalIgnoreCase);
        } finally {CloseHandle(handle);}
    }

    static Dictionary<uint,long> FindClients() {
        var result=new Dictionary<uint,long>();var all=new Dictionary<uint,ProcessEntry>();
        IntPtr snapshot=CreateToolhelp32Snapshot(2,0);
        if(snapshot==new IntPtr(-1))return result;
        try {
            var entry=new ProcessEntry {Size=(uint)Marshal.SizeOf(typeof(ProcessEntry))};
            if(Process32First(snapshot,ref entry))do {all[entry.Id]=entry;} while(Process32Next(snapshot,ref entry));
        } finally {CloseHandle(snapshot);}
        int currentSession;using(var self=Process.GetCurrentProcess())currentSession=self.SessionId;
        foreach(var child in all.Values) {
            ProcessEntry parent;uint session;
            if(!string.Equals(child.Name,"mstsc.exe",StringComparison.OrdinalIgnoreCase)
                || !all.TryGetValue(child.ParentId,out parent) || !string.Equals(parent.Name,"AlwaysRDP.exe",StringComparison.OrdinalIgnoreCase)
                || !ProcessIdToSessionId(child.Id,out session) || session!=(uint)currentSession
                || !IsAlwaysRdp(parent.Id) || !IsMstsc(child.Id))continue;
            try {
                using(var client=Process.GetProcessById((int)child.Id))
                using(var owner=Process.GetProcessById((int)parent.Id))
                    if(owner.StartTime<=client.StartTime)result[child.Id]=client.StartTime.Ticks;
            } catch(ArgumentException) {} catch(InvalidOperationException) {} catch(System.ComponentModel.Win32Exception) {}
        }
        return result;
    }

    static bool Move(IntPtr window) {
        int count=GetDesktopCount();
        if(count==1){CreateDesktop();count=GetDesktopCount();}
        return count>=2 && (GetWindowDesktopNumber(window)==TargetDesktop
            || (MoveWindowToDesktopNumber(window,TargetDesktop)>=0 && GetWindowDesktopNumber(window)==TargetDesktop));
    }

    sealed class HelperContext:ApplicationContext {
        readonly EventWaitHandle stop;
        readonly System.Windows.Forms.Timer timer;
        readonly NotifyIcon icon;
        readonly HashSet<Tuple<uint,long,IntPtr>> placed=new HashSet<Tuple<uint,long,IntPtr>>();
        internal HelperContext(EventWaitHandle stopEvent) {
            stop=stopEvent;
            var menu=new ContextMenuStrip();
            menu.Items.Add("Mover sesión actual",null,delegate {placed.Clear();Scan();});
            menu.Items.Add("Salir",null,delegate {ExitThread();});
            icon=new NotifyIcon {Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath),Text="AlwaysRDP Desktop 2",ContextMenuStrip=menu,Visible=true};
            timer=new System.Windows.Forms.Timer {Interval=900};
            timer.Tick+=delegate {if(stop.WaitOne(0))ExitThread();else Scan();};
            timer.Start();
        }
        void Scan() {
            var clients=FindClients();var visible=new HashSet<Tuple<uint,long,IntPtr>>();
            EnumWindows(delegate(IntPtr window,IntPtr state) {
                uint process;long started;GetWindowThreadProcessId(window,out process);
                if(clients.TryGetValue(process,out started) && IsWindowVisible(window)) {
                    var item=Tuple.Create(process,started,window);visible.Add(item);
                    if(!placed.Contains(item) && Move(window))placed.Add(item);
                }
                return true;
            },IntPtr.Zero);
            placed.IntersectWith(visible);
            icon.Text=placed.Count>0?"AlwaysRDP Desktop 2 · Sesión ubicada":"AlwaysRDP Desktop 2 · Esperando sesión";
        }
        protected override void Dispose(bool disposing) {
            if(disposing){timer.Dispose();icon.Visible=false;icon.Dispose();}
            base.Dispose(disposing);
        }
    }
}
