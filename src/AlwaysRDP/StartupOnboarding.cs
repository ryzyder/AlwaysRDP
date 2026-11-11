// Copyright (c) 2026 ryzyder. See LICENSE.
using System;
using System.IO;
using System.Windows.Forms;

internal static class StartupOnboarding {
    const string PromptedFile="startup-prompted";
    internal static void AskOnce(Form owner) {
        if(!AppPaths.IsPackaged)return;
        string path=Path.Combine(AppPaths.DataFolder,PromptedFile);
        if(File.Exists(path))return;
        File.WriteAllText(path,"1");
        if(MessageBox.Show(owner,L.T("¿Quieres que AlwaysRDP se inicie con Windows y permanezca en la bandeja del sistema?"),L.T("Inicio de Windows"),MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button1)!=DialogResult.Yes)return;
        try {StartupSettings.Set(true);}
        catch(Exception error) {MessageBox.Show(owner,error.Message,L.T("Inicio de Windows"),MessageBoxButtons.OK,MessageBoxIcon.Information);}
    }
}
