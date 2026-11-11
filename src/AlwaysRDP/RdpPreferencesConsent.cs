// Copyright (c) 2026 ryzyder. See LICENSE.
using System;
using System.IO;
using System.Windows.Forms;
using System.Xml.Linq;

internal static class RdpPreferencesConsent {
    internal static bool IsApproved(string folder) {
        string file=Path.Combine(folder,"rdp-preferences-consent.xml");
        if(!File.Exists(file))return false;
        try {var root=XDocument.Load(file).Root;return (string)root.Attribute("version")=="1" && (bool?)root.Attribute("accepted")==true;}
        catch(System.Xml.XmlException){return false;}
    }
    internal static void Accept(string folder) {
        Directory.CreateDirectory(folder);
        new XDocument(new XElement("rdpConsent",new XAttribute("version","1"),new XAttribute("accepted",true),new XAttribute("acceptedUtc",DateTime.UtcNow.ToString("o")))).Save(Path.Combine(folder,"rdp-preferences-consent.xml"));
    }
    internal static bool Request(string folder,IWin32Window owner) {
        if(IsApproved(folder))return true;
        string text=L.T("Para aplicar la conexión y los recursos elegidos, AlwaysRDP actualizará los valores predeterminados de Conexión a Escritorio remoto de Windows. Esto puede afectar a otras conexiones que abras con mstsc sin un archivo RDP propio. Guardaremos una copia previa de la configuración. ¿Quieres permitirlo?");
        if(MessageBox.Show(owner,text,L.T("Preferencias de RDP de Windows"),MessageBoxButtons.YesNo,MessageBoxIcon.Information,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return false;
        Accept(folder);return true;
    }
}
