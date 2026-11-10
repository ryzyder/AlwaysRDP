using System;
using System.IO;
using System.Linq;
internal static class ProfileTests {
    static void Require(bool condition,string message) { if(!condition) throw new Exception(message); }
    static int Main() {
        string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"profile-tests-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string fixture=Path.Combine(root,"Conexión de prueba.rdp");
        File.WriteAllLines(fixture,new[]{"full address:s:equipo-prueba.invalid:3390","username:s:DOMINIO\\usuario","password 51:b:valor-ficticio","signature:s:valor-ficticio","redirectprinters:i:1"});
        var store=new ProfileStore(root,fixture);
        Require(store.Items.Count==1,"Importación inicial");
        var profile=store.Active;
        Require(profile.Address=="equipo-prueba.invalid:3390" && profile.User=="DOMINIO\\usuario","Importación de equipo y usuario");
        Require(!store.TemplateLines(profile).Any(s=>s.StartsWith("password") || s.StartsWith("signature:")),"No guardar contraseñas ni firmas");
        profile.Name="Producción & administración"; store.Enabled=false; store.Save();
        var loaded=new ProfileStore(root,fixture);
        Require(!loaded.Enabled && loaded.Active.Id==profile.Id && loaded.Active.Name==profile.Name,"Persistencia de perfil, selección y estado");
        string persisted=Path.Combine(root,"conexiones.xml");
        var legacy=System.Xml.Linq.XDocument.Load(persisted);foreach(var element in legacy.Root.Elements("connection"))element.Element("resources").Remove();legacy.Save(persisted);
        var migrated=new ProfileStore(root,fixture);
        Require(migrated.Active.Resources.Key=="10000000000" && System.Xml.Linq.XDocument.Load(persisted).Root.Element("connection").Element("resources")!=null,"Migración de perfiles anteriores a solo portapapeles");
        var second=new ConnectionProfile { Id=Guid.NewGuid().ToString("N"),Name="Otro usuario",Address=profile.Address,User="OTRO\\persona",Template="" };
        store.Items.Add(second); store.ActiveId=second.Id; store.Save();
        var multiple=new ProfileStore(root,fixture);
        Require(multiple.Items.Count==2 && multiple.Active.User=="OTRO\\persona" && multiple.Active.Id==second.Id,"Varios usuarios, selección y persistencia");
        Require(multiple.TemplateLines(multiple.Active).Length==0,"Conexión manual sin plantilla");
        Require(multiple.Active.Resources.Key=="10000000000","Migración y valor inicial: solo portapapeles");
        multiple.Active.Resources.Set("drives",true);multiple.Active.Resources.Set("printers",true);multiple.Save();
        var shared=new ProfileStore(root,fixture);
        Require(shared.Active.Resources.Get("drives") && shared.Active.Resources.Get("printers") && shared.Active.Resources.Get("clipboard"),"Persistencia de los recursos por conexión");
        Require(!shared.Items.First(p=>p.Id==profile.Id).Resources.Get("drives"),"Recursos independientes para cada perfil");
        var properties=new System.Collections.Generic.Dictionary<string,string>();
        new SharedResources().Apply(properties);
        Require(properties["redirectclipboard"]=="i:1" && properties["drivestoredirect"]=="s:" && properties["redirectprinters"]=="i:0" && properties["audiomode"]=="i:2","Solo portapapeles deshabilita las otras redirecciones");
        foreach(var option in SharedResources.Options)shared.Active.Resources.Set(option.Id,true);
        shared.Active.Resources.Apply(properties);
        Require(properties["drivestoredirect"]=="s:*" && properties["redirectprinters"]=="i:1" && properties["camerastoredirect"]=="s:*" && properties["audiomode"]=="i:0" && properties["redirectwebauthn"]=="i:1","Mapeo de opciones a las propiedades RDP");
        string shortcut=Path.Combine(root,"startup-test.lnk"),exe=System.Windows.Forms.Application.ExecutablePath;
        StartupSettings.SetAt(shortcut,exe,true);Require(StartupSettings.IsEnabled(shortcut,exe),"Crear acceso de inicio");
        StartupSettings.SetAt(shortcut,exe,false);Require(!File.Exists(shortcut),"Desactivar inicio");
        profile.Template="..\\fuera.rdp";
        bool blocked=false; try {store.TemplateLines(profile);} catch(InvalidDataException){blocked=true;}
        Require(blocked,"Restringir las plantillas a la carpeta de perfiles");
        bool rejected=false; try {ProfileStore.Validate("Prueba","servidor /prompt","");} catch(ArgumentException){rejected=true;}
        Require(rejected,"Validación de dirección");
        var secretProfile=new ConnectionProfile {Id=Guid.NewGuid().ToString("N"),Name="Prueba de cifrado",Address="rdp-test-"+Guid.NewGuid().ToString("N")+".invalid",User="usuario-ficticio",Template=""};
        string testSecret="clave-ficticia-"+Guid.NewGuid().ToString("N");
        secretProfile.ProtectedPassword=ProfileSecret.Encrypt(secretProfile,testSecret);
        Require(secretProfile.ProtectedPassword!=testSecret,"No guardar la contraseña en claro");
        byte[] decoded=ProfileSecret.Decrypt(secretProfile);
        try{Require(System.Text.Encoding.Unicode.GetString(decoded)==testSecret,"Cifrado DPAPI reversible para este usuario");}finally{Array.Clear(decoded,0,decoded.Length);}
        store.Items.Add(secretProfile);store.Save();
        Require(!File.ReadAllText(persisted).Contains(testSecret),"XML y perfiles sin contraseña en claro");
        var restoredSecret=new ProfileStore(root,fixture).Items.First(p=>p.Id==secretProfile.Id);
        Require(restoredSecret.HasPassword,"Persistencia de la contraseña cifrada");
        Require(!CredentialLease.HasMatchingUser(secretProfile),"Destino ficticio sin credencial previa");
        using(var lease=CredentialLease.Install(secretProfile)) {
            Require(CredentialLease.HasMatchingUser(secretProfile),"Entrega nativa de la credencial del perfil");
            var alternate=new ConnectionProfile {Id=Guid.NewGuid().ToString("N"),Address=secretProfile.Address,User="otro-ficticio"};
            alternate.ProtectedPassword=ProfileSecret.Encrypt(alternate,"otra-clave-ficticia");
            using(var nested=CredentialLease.Install(alternate))Require(CredentialLease.HasMatchingUser(alternate),"Cambio de usuario para el mismo equipo");
            Require(CredentialLease.HasMatchingUser(secretProfile),"Restauración de la credencial anterior");
        }
        Require(!CredentialLease.HasMatchingUser(secretProfile),"Retirada de la credencial temporal");
        Require(RecoveryPolicy.NeedsAttention(false,263),"Fallo antes de iniciar sesión no produce bucle");
        Require(RecoveryPolicy.NeedsAttention(false,2055),"Fallo de contraseña pausa reintentos");
        Require(RecoveryPolicy.NeedsAttention(false,0),"Cancelar credenciales no las vuelve a pedir sin fin");
        Require(!RecoveryPolicy.NeedsAttention(true,0) && !RecoveryPolicy.NeedsAttention(true,2308),"Recuperación tras cerrar o perder una sesión iniciada");
        Require(!RecoveryPolicy.NeedsAttention(false,516),"Los fallos de red conservan la reconexión");
        var guard=new DisconnectGuard();var time=DateTime.UtcNow;
        guard.Disconnected(263,time);
        Require(!guard.Settled(time.AddSeconds(1)),"No cerrar mstsc por el evento intermedio 263");
        guard.Progress(); // observed event 1025, then 1027 in the same process
        Require(!guard.Pending && !guard.Settled(time.AddSeconds(20)),"El progreso posterior cancela la desconexión intermedia");
        guard.Disconnected(519,time);guard.Progress();
        Require(!guard.Pending,"La recuperación del código 519 tampoco debe cerrar el cliente");
        guard.Disconnected(2055,time);
        Require(guard.Settled(time.AddSeconds(9)) && RecoveryPolicy.NeedsAttention(false,guard.Reason),"Error de acceso confirmado pasa a intervención, sin reiniciar");
        guard.Progress();Require(!guard.Pending,"Completar el acceso cancela el error pendiente");
        string deletionFolder=Path.Combine(root,"deletion");var deletion=new ProfileStore(deletionFolder,fixture);
        var first=deletion.Active;var other=deletion.Import(fixture);deletion.Enabled=true;deletion.Save();
        string templateToDelete=Path.Combine(deletionFolder,other.Template);deletion.Delete(other);
        Require(deletion.Active.Id==first.Id && deletion.Enabled && !File.Exists(templateToDelete),"Borrar perfil inactivo conserva la sesión seleccionada");
        Require(!File.ReadAllText(Path.Combine(deletionFolder,"conexiones.xml.bak")).Contains(other.Id),"El perfil borrado tampoco queda en la copia rotativa");
        deletion.Delete(first);
        Require(deletion.Items.Count==0 && !deletion.Enabled,"Borrar el último perfil deja la supervisión detenida");
        var empty=new ProfileStore(deletionFolder,fixture);
        Require(empty.Items.Count==0 && File.Exists(fixture),"Los perfiles eliminados no reaparecen al reiniciar ni se borra el RDP original");
        string languageFolder=Path.Combine(root,"language");Directory.CreateDirectory(languageFolder);L.Initialize(languageFolder);
        foreach(string code in new[]{"en","es","pt"}) {
            L.Change(code);foreach(string key in L.Keys)Require(!string.IsNullOrWhiteSpace(L.T(key)),"Traducciones completas");
            foreach(string key in L.Keys)string.Format(L.T(key),"test");
        }
        L.Change("en");Require(L.T("Guardar")=="Save" && L.F("Activa: {0}","Perfil") == "Active: Perfil","Inglés y textos dinámicos");
        using(var form=new System.Windows.Forms.Form()) {
            var label=new System.Windows.Forms.Label {Text="Guardar"};var input=new System.Windows.Forms.TextBox {Text="Guardar",UseSystemPasswordChar=true};form.Controls.Add(label);form.Controls.Add(input);
            L.Apply(form);Require(label.Text=="Save" && input.Text=="Guardar","Traducir sin modificar datos ni contraseñas escritos");
            L.Change("pt");L.Apply(form);Require(label.Text=="Salvar" && input.Text=="Guardar","Cambiar idioma en vivo sin perder campos");
        }
        L.Initialize(languageFolder);Require(L.Language=="pt","Recordar idioma al reiniciar");
        L.Change("es");
        Console.WriteLine("OK: perfiles, cifrado, recuperación, eliminación, recursos, inicio y tres idiomas verificados.");
        return 0;
    }
}
