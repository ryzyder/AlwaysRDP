// Copyright (c) 2026 ryzyder. See LICENSE.
using System;
using System.Diagnostics;
using System.IO;

internal static class Desktop2Controller {
    const string StateFile="desktop2.enabled";
    internal static bool Enabled {
        get {
            string path=Path.Combine(AppPaths.DataFolder,StateFile);
            return !File.Exists(path) || File.ReadAllText(path)=="1";
        }
    }
    internal static void Set(bool enabled) {
        File.WriteAllText(Path.Combine(AppPaths.DataFolder,StateFile),enabled?"1":"0");
        if(enabled)Start();else Stop();
    }
    internal static void Start() {Run("");}
    internal static void Stop() {Run("/stop");}
    static void Run(string arguments) {
        if(arguments.Length==0 && !Enabled)return;
        string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"AlwaysRDP.Desktop2.exe");
        if(File.Exists(path))Process.Start(new ProcessStartInfo(path,arguments) {UseShellExecute=false,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory});
    }
}
