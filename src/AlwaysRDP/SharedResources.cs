// Copyright (c) 2026 ryzyder. All rights reserved.
// Project-owned portions are subject to the root LICENSE; third-party notices remain applicable.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Xml.Linq;

internal sealed class ResourceOption {
    internal string Id, Label;
    internal ResourceOption(string id,string label) {Id=id;Label=label;}
}
internal sealed class SharedResources {
    internal static readonly ResourceOption[] Options={
        new ResourceOption("clipboard",L.T("Portapapeles (copiar y pegar)")),
        new ResourceOption("drives",L.T("Unidades y discos")),
        new ResourceOption("printers",L.T("Impresoras")),
        new ResourceOption("sound",L.T("Sonido del equipo remoto")),
        new ResourceOption("microphone",L.T("Micrófono")),
        new ResourceOption("camera",L.T("Cámaras")),
        new ResourceOption("smartcards",L.T("Tarjetas inteligentes")),
        new ResourceOption("ports",L.T("Puertos COM")),
        new ResourceOption("devices",L.T("USB y otros dispositivos")),
        new ResourceOption("location",L.T("Ubicación")),
        new ResourceOption("webauthn",L.T("Windows Hello / WebAuthn"))
    };
    readonly Dictionary<string,bool> values=new Dictionary<string,bool>();
    internal SharedResources() { foreach(var o in Options) values[o.Id]=o.Id=="clipboard"; }
    internal bool Get(string id) {return values[id];}
    internal void Set(string id,bool enabled) {if(!values.ContainsKey(id))throw new ArgumentException(L.T("Recurso no reconocido"));values[id]=enabled;}
    internal string Key {get{return string.Join("",Options.Select(o=>Get(o.Id)?"1":"0"));}}
    internal string Summary {get {
        int n=Options.Count(o=>Get(o.Id));
        return n==0 ? L.T("Sin recursos compartidos") : n==1 && Get("clipboard") ? L.T("Solo portapapeles") : L.F("{0} recursos compartidos",n);
    }}
    internal XElement ToXml() {return new XElement("resources",Options.Select(o=>new XAttribute(o.Id,Get(o.Id))));}
    internal static SharedResources FromXml(XElement e) {
        var r=new SharedResources(); if(e!=null)foreach(var o in Options)r.Set(o.Id,(bool?)e.Attribute(o.Id) ?? o.Id=="clipboard");return r;
    }
    internal void Apply(Dictionary<string,string> s) {
        s["redirectclipboard"]=Get("clipboard")?"i:1":"i:0";
        s["redirectdrives"]=Get("drives")?"i:1":"i:0";
        s["drivestoredirect"]=Get("drives")?"s:*":"s:";
        s["redirectprinters"]=Get("printers")?"i:1":"i:0";
        s["audiomode"]=Get("sound")?"i:0":"i:2";
        s["audiocapturemode"]=Get("microphone")?"i:1":"i:0";
        s["camerastoredirect"]=Get("camera")?"s:*":"s:";
        s["redirectsmartcards"]=Get("smartcards")?"i:1":"i:0";
        s["redirectcomports"]=Get("ports")?"i:1":"i:0";
        s["redirectposdevices"]=Get("devices")?"i:1":"i:0";
        s["devicestoredirect"]=Get("devices")?"s:*":"s:";
        s["usbdevicestoredirect"]=Get("devices")?"s:*":"s:";
        s["redirectlocation"]=Get("location")?"i:1":"i:0";
        s["redirectwebauthn"]=Get("webauthn")?"i:1":"i:0";
    }
}

internal static class StartupSettings {
    internal static string ShortcutPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup),"AlwaysRDP.lnk");}}
    static object Read(object obj,string property) {return obj.GetType().InvokeMember(property,BindingFlags.GetProperty,null,obj,null);}
    static void Write(object obj,string property,object value) {obj.GetType().InvokeMember(property,BindingFlags.SetProperty,null,obj,new[]{value});}
    internal static bool Enabled {get {return IsEnabled(ShortcutPath,System.Windows.Forms.Application.ExecutablePath);}}
    internal static bool IsEnabled(string path,string executable) {
        if(!File.Exists(path))return false;
        object shell=null,link=null;
        try {
            shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            link=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});
            return string.Equals(Path.GetFullPath((string)Read(link,"TargetPath")),Path.GetFullPath(executable),StringComparison.OrdinalIgnoreCase);
        } finally {if(link!=null)Marshal.FinalReleaseComObject(link);if(shell!=null)Marshal.FinalReleaseComObject(shell);}
    }
    internal static void Set(bool enabled) {SetAt(ShortcutPath,System.Windows.Forms.Application.ExecutablePath,enabled);}
    internal static void SetAt(string path,string executable,bool enabled) {
        if(File.Exists(path) && !IsEnabled(path,executable))throw new InvalidOperationException(L.T("Ya existe un acceso de inicio con ese nombre para otra aplicación."));
        if(!enabled) {if(File.Exists(path))File.Delete(path);return;}
        object shell=null,link=null;
        try {
            shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
            link=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});
            Write(link,"TargetPath",executable);Write(link,"Arguments","/background");Write(link,"WorkingDirectory",Path.GetDirectoryName(executable));
            Write(link,"WindowStyle",7);Write(link,"Description",L.T("AlwaysRDP en segundo plano al iniciar sesión"));
            Write(link,"IconLocation",Path.Combine(Path.GetDirectoryName(executable),"RDP-Permanente.ico")+",0");
            link.GetType().InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);
        } finally {if(link!=null)Marshal.FinalReleaseComObject(link);if(shell!=null)Marshal.FinalReleaseComObject(shell);}
    }
}
