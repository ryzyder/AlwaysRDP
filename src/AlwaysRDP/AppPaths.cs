// Copyright (c) 2026 ryzyder. See LICENSE.
using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

internal static class AppPaths {
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]
    static extern int GetCurrentPackageFullName(ref uint length,StringBuilder name);
    internal static bool IsPackaged {get {uint length=0;return GetCurrentPackageFullName(ref length,null)==122;}}
    internal static string ResolveDataFolder(bool packaged,string localData,string installation) {return packaged?Path.Combine(localData,"AlwaysRDP"):installation;}
    internal static string DataFolder {
        get {string path=ResolveDataFolder(IsPackaged,Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),AppDomain.CurrentDomain.BaseDirectory);Directory.CreateDirectory(path);return path;}
    }
}
