// Copyright (c) 2026 ryzyder. All rights reserved.
// Project-owned portions are subject to the root LICENSE; third-party notices remain applicable.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Windows.Forms;

internal static class Native {
    [DllImport("VirtualDesktopAccessor.dll")] internal static extern int GetDesktopCount();
    [DllImport("VirtualDesktopAccessor.dll")] internal static extern int GetCurrentDesktopNumber();
    [DllImport("VirtualDesktopAccessor.dll")] internal static extern int GetWindowDesktopNumber(IntPtr h);
    [DllImport("VirtualDesktopAccessor.dll")] internal static extern int MoveWindowToDesktopNumber(IntPtr h, int n);
    [DllImport("VirtualDesktopAccessor.dll")] internal static extern int CreateDesktop();
    [DllImport("VirtualDesktopAccessor.dll")] internal static extern int GoToDesktopNumber(int n);
    internal delegate bool EnumProc(IntPtr h, IntPtr p);
    internal delegate void EventProc(IntPtr hook, uint e, IntPtr h, int obj, int child, uint thread, uint time);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumProc cb, IntPtr p);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr h, uint flag);
    [DllImport("user32.dll")] internal static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr mod, EventProc cb, uint pid, uint tid, uint flags);
    [DllImport("user32.dll")] internal static extern bool UnhookWinEvent(IntPtr h);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)] internal struct Startup {
        internal int cb; internal string reserved, desktop, title;
        internal int x,y,cx,cy,xchars,ychars,fill,flags;
        internal short show, reserved2; internal IntPtr reservedPtr, input, output, error;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct ProcInfo { internal IntPtr process, thread; internal uint pid, tid; }
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    internal static extern bool CreateProcess(string app, StringBuilder command, IntPtr pa, IntPtr ta, bool inherit, uint flags, IntPtr env, string cwd, ref Startup startup, out ProcInfo proc);
    [DllImport("kernel32.dll")] internal static extern bool CloseHandle(IntPtr h);
}

internal sealed class ProbeForm : Form {
    protected override bool ShowWithoutActivation { get { return true; } }
}

internal sealed class Supervisor : ApplicationContext {
    const string EventName = "Local\\OscarRdpSupervisorStopV1";
    const string OpenEventName = "Local\\OscarRdpSupervisorOpenV1";
    const string ManageEventName = "Local\\OscarRdpSupervisorManageV1";
    const string MutexName = "Local\\OscarRdpSupervisorV1";
    internal static readonly string Folder = AppDomain.CurrentDomain.BaseDirectory;
    static readonly string Source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Administrator.rdp");
    static readonly string Rdp = Path.Combine(Folder, "Administrator-supervisado.rdp");
    static readonly string LogFile = Path.Combine(Folder, "supervisor.log");
    static readonly object LogLock = new object();
    static ProfileStore Profiles;
    internal static readonly Icon AppIcon=new Icon(Path.Combine(Folder,"RDP-Permanente.ico"));
    readonly EventWaitHandle stop;
    readonly EventWaitHandle open;
    readonly EventWaitHandle manage;
    readonly NotifyIcon tray;
    readonly System.Windows.Forms.Timer timer;
    readonly Control dispatcher;
    EventLogWatcher watcher;
    Process client;
    int clientPid, failures;
    bool stopping, connected, moving, awaitingUser;
    bool allowSessionActivation;
    readonly DisconnectGuard disconnect=new DisconnectGuard();
    CredentialLease credentialLease;
    DateTime launched, nextLaunch = DateTime.UtcNow, connectedAt;
    IntPtr lastForeground, foregroundHook, showHook;
    int lastDesktop;
    readonly Native.EventProc hookCallback;
    readonly HashSet<IntPtr> moved = new HashSet<IntPtr>();
    ConnectionManager manager;
    string stateText=L.T("Iniciando"), runningKey="";
    object[] stateArguments=new object[0];
    internal string StateText { get {string value=L.F(stateText,stateArguments);return value.Length==0?value:char.ToUpper(value[0])+value.Substring(1);} }
    internal string ActiveName { get { return Profiles.Active==null ? L.T("Sin conexión seleccionada") : L.F("Activa: {0}",Profiles.Active.Name); } }
    internal static void Log(string text) {
        lock(LogLock) {
            if (File.Exists(LogFile) && new FileInfo(LogFile).Length > 1024*1024)
                File.Copy(LogFile, LogFile+".previous", true);
            if (File.Exists(LogFile) && new FileInfo(LogFile).Length > 1024*1024) File.WriteAllText(LogFile, "");
            File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" "+text+Environment.NewLine);
        }
    }
    internal static void PrepareRdp() {
        var profile=Profiles==null ? null : Profiles.Active;
        if(profile==null && !File.Exists(Source)) throw new FileNotFoundException(L.T("Agrega una conexión en AlwaysRDP."));
        var settings = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
            {"screen mode id","i:2"}, {"use multimon","i:0"},
            {"redirectclipboard","i:1"}, {"redirectdrives","i:0"}, {"drivestoredirect","s:"},
            {"redirectprinters","i:0"}, {"redirectcomports","i:0"}, {"redirectsmartcards","i:0"},
            {"redirectposdevices","i:0"}, {"redirectdirectx","i:0"}, {"devicestoredirect","s:"},
            {"usbdevicestoredirect","s:"}, {"camerastoredirect","s:"},
            {"audiomode","i:2"}, {"audiocapturemode","i:0"}, {"redirectlocation","i:0"},
            {"redirectwebauthn","i:0"}, {"redirected video capture encoding quality","i:0"},
            {"autoreconnection enabled","i:0"}, {"displayconnectionbar","i:1"},
            {"keyboardhook","i:0"}, {"disableconnectionsharing","i:1"}
        };
        if(profile!=null) {
            ProfileStore.Validate(profile.Name,profile.Address,profile.User);
            settings["full address"]="s:"+profile.Address;
            settings["username"]="s:"+profile.User;
            settings["prompt for credentials"]="i:0";
            profile.Resources.Apply(settings);
        }
        var sourceLines=profile==null ? File.ReadAllLines(Source) : Profiles.TemplateLines(profile);
        var lines = sourceLines.Where(line => {
            string key=line.Split(':')[0];
            return !settings.ContainsKey(key) && !key.Equals("signature",StringComparison.OrdinalIgnoreCase)
                && !key.Equals("signscope",StringComparison.OrdinalIgnoreCase) && !key.StartsWith("password",StringComparison.OrdinalIgnoreCase);
        }).ToList();
        foreach(var pair in settings) lines.Add(pair.Key+":"+pair.Value);
        File.WriteAllLines(Rdp, lines, Encoding.Unicode);
    }
    static string PrepareDirectConnection() {
        PrepareRdp();
        var lines=File.ReadAllLines(Rdp);
        string addressLine=lines.FirstOrDefault(line=>line.StartsWith("full address:s:",StringComparison.OrdinalIgnoreCase));
        string address=addressLine==null ? "" : addressLine.Substring("full address:s:".Length).Trim();
        if(string.IsNullOrWhiteSpace(address) || address.Any(c=>char.IsWhiteSpace(c) || c=='\"' || c=='\0'))
            throw new InvalidOperationException(L.T("La dirección de Administrator.rdp no es válida para una conexión directa."));
        string defaults=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"Default.rdp");
        string backup=Path.Combine(Folder,"Default-anterior.rdp.bak");
        if(File.Exists(defaults) && !File.Exists(backup)) File.Copy(defaults,backup);
        // mstsc /v reads the user's default preferences. No explicit RDP file is opened.
        // FileMode.Create fails for an existing hidden file on Windows. Open it
        // explicitly and truncate the stream, preserving the hidden attribute.
        using(var stream=new FileStream(defaults,FileMode.OpenOrCreate,FileAccess.Write,FileShare.Read)) {
            stream.SetLength(0);
            using(var writer=new StreamWriter(stream,Encoding.Unicode))
                foreach(string line in lines) writer.WriteLine(line);
        }
        File.SetAttributes(defaults,File.GetAttributes(defaults) | FileAttributes.Hidden);
        return address;
    }
    internal Supervisor(bool foregroundRequested) {
        dispatcher = new Control(); var unused = dispatcher.Handle;
        stop = new EventWaitHandle(false, EventResetMode.ManualReset, EventName); stop.Reset();
        open = new EventWaitHandle(false, EventResetMode.AutoReset, OpenEventName);
        manage = new EventWaitHandle(false, EventResetMode.AutoReset, ManageEventName);
        if(foregroundRequested) manage.Set();
        tray = new NotifyIcon { Icon=AppIcon, Visible=true, Text="AlwaysRDP" };
        var menu = new ContextMenuStrip();
        menu.Items.Add(L.T("Conexiones…"), null, delegate { ShowManager(); });
        menu.Items.Add(L.T("Ver sesión en escritorio 2"), null, delegate { ShowSession(); });
        menu.Items.Add(L.T("Detener sesión"), null, delegate { PauseSession(); });
        menu.Items.Add(L.T("Detener y cerrar esta conexión"), null, delegate { Shutdown(); });
        tray.ContextMenuStrip=menu;
        L.Changed+=RefreshLanguage;
        tray.DoubleClick += delegate { ShowManager(); };
        lastForeground=Native.GetForegroundWindow(); lastDesktop=Native.GetCurrentDesktopNumber();
        hookCallback=WindowEvent;
        foregroundHook=Native.SetWinEventHook(3,3,IntPtr.Zero,hookCallback,0,0,0);
        showHook=Native.SetWinEventHook(0x8002,0x8002,IntPtr.Zero,hookCallback,0,0,0);
        string query="*[System[(EventID=1025 or EventID=1026 or EventID=1027)]]";
        watcher=new EventLogWatcher(new EventLogQuery("Microsoft-Windows-TerminalServices-RDPClient/Operational",PathType.LogName,query));
        watcher.EventRecordWritten += Record;
        watcher.Enabled=true;
        timer=new System.Windows.Forms.Timer { Interval=250 };
        timer.Tick += Tick; timer.Start();
        if(!Profiles.Enabled || Profiles.Active==null) Status(L.T("detenida"));
        Log("Supervisor iniciado; solo se controla su propia instancia. Recursos según el perfil seleccionado.");
    }
    void Status(string text,params object[] arguments) {stateText=text;stateArguments=arguments;RefreshLanguage();}
    void RefreshLanguage() {string text="AlwaysRDP: "+StateText;tray.Text=text.Substring(0,Math.Min(63,text.Length));foreach(ToolStripItem item in tray.ContextMenuStrip.Items)L.Apply(item);}
    void ShowManager() {
        if(manager==null || manager.IsDisposed) manager=new ConnectionManager(this,Profiles);
        int desktop=Native.GetCurrentDesktopNumber();
        manager.Show();
        Native.MoveWindowToDesktopNumber(manager.Handle,desktop);
        if(manager.WindowState==FormWindowState.Minimized) manager.WindowState=FormWindowState.Normal;
        manager.Activate();
    }
    internal void ConnectProfile(ConnectionProfile profile) {
        bool reuse=connected && client!=null && !client.HasExited && runningKey==profile.Key;
        Profiles.ActiveId=profile.Id; Profiles.Enabled=true; Profiles.Save(); failures=0;
        if(!reuse) CloseClient();
        nextLaunch=DateTime.UtcNow; ShowSession();
    }
    internal void ShowSession() {
        if(Profiles.Active==null) { ShowManager(); return; }
        if(Native.GetDesktopCount()<2) Native.CreateDesktop();
        Native.GoToDesktopNumber(1);
        if(client==null || client.HasExited) {
            CloseClient(); Profiles.Enabled=true; Profiles.Save(); failures=0; nextLaunch=DateTime.UtcNow; Launch(true);
        }
        var window=client.MainWindowHandle;
        if(window!=IntPtr.Zero) { if(Native.IsIconic(window)) Native.ShowWindow(window,9); Native.SetForegroundWindow(window); }
    }
    internal void PauseSession() {
        Profiles.Enabled=false; Profiles.Save(); CloseClient(); Status(L.T("detenida")); Log("Sesión detenida desde la interfaz.");
    }
    internal void DeleteProfile(ConnectionProfile profile) {
        if(Profiles.Active!=null && Profiles.Active.Id==profile.Id)PauseSession();
        Profiles.Delete(profile);
        if(Profiles.Active==null)Status(L.T("sin perfiles"));
    }
    void Record(object sender, EventRecordWrittenEventArgs e) {
        if(e.EventException != null) { Log("Error de lectura de eventos: "+e.EventException.GetType().Name); return; }
        using(var r=e.EventRecord) {
            if(r==null || r.ProcessId!=clientPid || (r.TimeCreated.HasValue && r.TimeCreated.Value.ToUniversalTime()<launched)) return;
            int id=r.Id;
            int expectedPid=clientPid;
            string reason=(id==1026 && r.Properties.Count>1) ? Convert.ToString(r.Properties[1].Value) : "";
            try { dispatcher.BeginInvoke((Action)delegate {
                if(stopping || clientPid!=expectedPid) return;
                if(id==1027) {
                    disconnect.Progress();
                    connected=true; connectedAt=DateTime.UtcNow;
                    if(awaitingUser) {Profiles.Enabled=true;Profiles.Save();awaitingUser=false;}
                    failures=0;ReleaseCredential();
                    Status(L.T("conectado en escritorio 2")); Log("Conexión establecida. PID="+clientPid);
                } else if(id==1025) {
                    disconnect.Progress();
                    if(!connected)Status(L.T("autenticando en escritorio 2"));
                } else if(id==1026) {
                    Log("Desconexión detectada. Motivo="+reason+". PID="+clientPid);
                    int code;int.TryParse(reason,out code);disconnect.Disconnected(code,DateTime.UtcNow);
                }
            }); } catch(InvalidOperationException) { }
        }
    }
    void WindowEvent(IntPtr hook,uint evt,IntPtr h,int obj,int child,uint thread,uint time) {
        if(stopping || moving || h==IntPtr.Zero || obj!=0 || child!=0) return;
        uint pid; Native.GetWindowThreadProcessId(h,out pid);
        if(pid==(uint)clientPid && clientPid!=0) {
            if(Native.GetAncestor(h,2)==h) Move(h,evt==3);
        } else if(evt==3) {
            int desktop=Native.GetWindowDesktopNumber(h);
            if(desktop>=0) { lastForeground=h; lastDesktop=desktop; }
        }
    }
    void Move(IntPtr h,bool foreground) {
        moving=true;
        try {
            bool justMoved=false;
            if(!moved.Contains(h) || Native.GetWindowDesktopNumber(h)!=1) {
                int result=Native.MoveWindowToDesktopNumber(h,1);
                if(result<0) return;
                moved.Add(h);
                justMoved=true;
            }
            bool backgroundConnecting=!allowSessionActivation && DateTime.UtcNow-launched<TimeSpan.FromSeconds(15);
            if((justMoved || backgroundConnecting) && foreground && lastDesktop!=1 && lastForeground!=IntPtr.Zero && Native.IsWindow(lastForeground)) {
                if(Native.GetCurrentDesktopNumber()!=lastDesktop) Native.GoToDesktopNumber(lastDesktop);
                Native.SetForegroundWindow(lastForeground);
            }
        } finally { moving=false; }
    }
    void Tick(object sender, EventArgs e) {
        try {
            if(stop.WaitOne(0)) { Shutdown(); return; }
            if(manage.WaitOne(0)) ShowManager();
            if(open.WaitOne(0)) ShowSession();
            if(!Profiles.Enabled || Profiles.Active==null) {
                if(client!=null && client.HasExited)CloseClient();
                return;
            }
            if(client!=null) {
                if(client.HasExited) { Restart(); return; }
                if(disconnect.Settled(DateTime.UtcNow)) {
                    if(RecoveryPolicy.NeedsAttention(connected,disconnect.Reason)) {
                        Profiles.Enabled=false;Profiles.Save();awaitingUser=true;
                        Status(L.T("acceso pendiente ({0})"),disconnect.Reason);
                        Log("Reintentos pausados; se conserva la ventana de Windows para revisar el acceso. Motivo="+disconnect.Reason);
                    } else Restart();
                    return;
                }
                Native.EnumWindows(delegate(IntPtr h,IntPtr p) {
                    uint pid; Native.GetWindowThreadProcessId(h,out pid);
                    if(pid==(uint)clientPid) Move(h,Native.GetForegroundWindow()==h);
                    return true;
                },IntPtr.Zero);
                if(connected && DateTime.UtcNow-connectedAt>TimeSpan.FromMinutes(2)) failures=0;
                return;
            }
            if(DateTime.UtcNow>=nextLaunch) Launch();
        } catch(Exception ex) {
            Log("Error: "+ex.GetType().Name+" "+ex.Message);
            CloseClient();Profiles.Enabled=false;Profiles.Save();
            Status(L.T("error; revisa la conexión"));
        }
    }
    void Launch(bool foreground=false) {
        int count=Native.GetDesktopCount();
        if(count<1) throw new InvalidOperationException(L.T("No se puede acceder a los escritorios virtuales."));
        if(count<2 && Native.CreateDesktop()<0) throw new InvalidOperationException(L.T("No se pudo crear el escritorio 2."));
        string address=PrepareDirectConnection();
        bool prompt=!Profiles.Active.HasPassword && !CredentialLease.HasMatchingUser(Profiles.Active);
        credentialLease=CredentialLease.Install(Profiles.Active);
        allowSessionActivation=foreground;
        runningKey=Profiles.Active==null ? "" : Profiles.Active.Key;
        moved.Clear(); connected=false;awaitingUser=false;disconnect.Progress();
        lastForeground=Native.GetForegroundWindow(); lastDesktop=Native.GetCurrentDesktopNumber();
        var startup=new Native.Startup(); startup.cb=Marshal.SizeOf(typeof(Native.Startup));
        startup.flags=1; startup.show=4; // SW_SHOWNOACTIVATE
        var executable=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"mstsc.exe");
        var command=new StringBuilder("\""+executable+"\" /v:\""+address+"\" /f"+(prompt?" /prompt":""));
        Native.ProcInfo info;
        launched=DateTime.UtcNow;
        if(!Native.CreateProcess(executable,command,IntPtr.Zero,IntPtr.Zero,false,0,IntPtr.Zero,Folder,ref startup,out info))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        clientPid=(int)info.pid;
        Native.CloseHandle(info.process); Native.CloseHandle(info.thread);
        client=Process.GetProcessById(clientPid);
        Status(prompt?L.T("esperando credenciales en escritorio 2"):L.T("conectando en escritorio 2")); Log("Cliente iniciado directamente al servidor, a pantalla completa. "+Profiles.Active.Resources.Summary+". PID="+clientPid);
    }
    void ReleaseCredential() {
        var lease=credentialLease;credentialLease=null;
        if(lease!=null)try{lease.Dispose();}catch(Exception ex){Log("No se pudo restaurar la credencial previa de Windows: "+ex.GetType().Name);}
    }
    void CloseClient() {
        var p=client; client=null; clientPid=0; connected=false;awaitingUser=false;disconnect.Progress();
        if(p==null) {ReleaseCredential();return;}
        try { if(!p.HasExited) { p.CloseMainWindow(); if(!p.WaitForExit(1500)) p.Kill(); } }
        catch(InvalidOperationException) { }
        finally { p.Dispose();ReleaseCredential(); }
    }
    void Restart() {
        bool needsAttention=RecoveryPolicy.NeedsAttention(connected,disconnect.Reason);
        CloseClient();
        if(needsAttention) {
            Profiles.Enabled=false;Profiles.Save();Status(L.T("revisa usuario y contraseña"));
            Log("Reintentos pausados: no se completó el inicio de sesión o Windows requiere revisar el acceso.");return;
        }
        failures=Math.Min(10,failures+1);
        int seconds=Math.Min(300,1 << Math.Min(9,failures-1));
        nextLaunch=DateTime.UtcNow.AddSeconds(seconds);
        Status(L.T("reintento en {0} segundos"),seconds); Log("Instancia cerrada. Reintento en "+seconds+" segundos.");
    }
    void Shutdown() {
        if(stopping) return; stopping=true;
        timer.Stop(); watcher.Enabled=false; watcher.Dispose();
        Native.UnhookWinEvent(foregroundHook); Native.UnhookWinEvent(showHook);
        CloseClient(); if(manager!=null) manager.Dispose();L.Changed-=RefreshLanguage; tray.Visible=false; tray.Dispose(); stop.Dispose(); open.Dispose(); manage.Dispose(); dispatcher.Dispose();
        Log("Supervisor detenido por el usuario."); ExitThread();
    }
    [STAThread] internal static int Main(string[] args) {
        Directory.SetCurrentDirectory(Folder);
        L.Initialize(Folder);
        if(args.Contains("/stop")) {
            try { using(var e=EventWaitHandle.OpenExisting(EventName)) e.Set(); } catch(WaitHandleCannotBeOpenedException) { }
            return 0;
        }
        try {
            Native.SetProcessDPIAware();
            if(args.Contains("/check")) {
                Profiles=new ProfileStore(Folder,Source);
                PrepareRdp();
                int count=Native.GetDesktopCount();
                if(count<2) throw new InvalidOperationException("Se necesitan dos escritorios virtuales.");
                using(var form=new ProbeForm { Text="Verificación de RDP", Opacity=0.01, ShowInTaskbar=false, Width=100, Height=100 }) {
                    form.Show(); Application.DoEvents();
                    var h=form.Handle;
                    int result=-1;
                    for(int attempt=0;attempt<20;attempt++) {
                        Application.DoEvents(); Thread.Sleep(50);
                        result=Native.MoveWindowToDesktopNumber(h,1);
                        if(result>=0 && Native.GetWindowDesktopNumber(h)==1) break;
                    }
                    if(result<0 || Native.GetWindowDesktopNumber(h)!=1)
                        throw new InvalidOperationException("No se pudo verificar el escritorio 2.");
                    form.Close();
                }
                using(var session=new EventLogSession()) {
                    var info=session.GetLogInformation("Microsoft-Windows-TerminalServices-RDPClient/Operational",PathType.LogName);
                }
                File.WriteAllText(Path.Combine(Folder,"verificacion.txt"),"OK: cliente de Windows, configuración, registro de eventos y movimiento al escritorio 2 verificados.\r\nNo se prueba aquí la conexión ni las credenciales.\r\n");
                Environment.Exit(0); return 0;
            }
            bool first;
            using(var mutex=new Mutex(true,MutexName,out first)) {
                if(!first) {
                    if(!args.Contains("/background"))
                        try { using(var request=EventWaitHandle.OpenExisting(args.Contains("/open") ? OpenEventName : ManageEventName)) request.Set(); }
                        catch(WaitHandleCannotBeOpenedException) { }
                    Environment.Exit(0); return 0;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Profiles=new ProfileStore(Folder,Source);
                Application.Run(new Supervisor(!args.Contains("/background")));
                mutex.ReleaseMutex();
            }
            Environment.Exit(0); return 0;
        } catch(Exception ex) {
            Log("Fallo de inicio: "+ex.GetType().Name+" "+ex.Message);
            File.WriteAllText(Path.Combine(Folder,"error.txt"),ex.Message);
            Environment.Exit(1); return 1;
        }
    }
}
