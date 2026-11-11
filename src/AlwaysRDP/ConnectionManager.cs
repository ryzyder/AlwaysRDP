// Copyright (c) 2026 ryzyder. All rights reserved.
// Project-owned portions are subject to the root LICENSE; third-party notices remain applicable.
using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml.Linq;

internal sealed class ConnectionProfile {
    internal string Id, Name, Address, User, Template;
    internal string ProtectedPassword="";
    internal bool HasPassword {get{return !string.IsNullOrEmpty(ProtectedPassword);}}
    internal SharedResources Resources=new SharedResources();
    internal string Key { get { return Id+"|"+Address+"|"+User+"|"+Resources.Key+"|"+ProtectedPassword; } }
    public override string ToString() { return Name; }
}

internal sealed class ProfileStore {
    readonly string folder;
    internal readonly List<ConnectionProfile> Items=new List<ConnectionProfile>();
    internal string ActiveId;
    internal bool Enabled=true;
    internal ProfileStore(string directory,string initialRdp) {
        folder=directory;
        Directory.CreateDirectory(Path.Combine(folder,"perfiles"));
        string file=Path.Combine(folder,"conexiones.xml");
        if(File.Exists(file)) {
            var doc=XDocument.Load(file);
            ActiveId=(string)doc.Root.Attribute("active");
            Enabled=(bool?)doc.Root.Attribute("enabled") ?? true;
            foreach(var e in doc.Root.Elements("connection"))
                Items.Add(new ConnectionProfile { Id=(string)e.Attribute("id"), Name=(string)e.Element("name") ?? L.T("Conexión"),
                    Address=(string)e.Element("address") ?? "", User=(string)e.Element("user") ?? "",
                    Template=(string)e.Element("template") ?? "",ProtectedPassword=(string)e.Element("protectedPassword") ?? "",Resources=SharedResources.FromXml(e.Element("resources")) });
            if(doc.Root.Elements("connection").Any(e=>e.Element("resources")==null))Save();
        }
        if(Items.Count==0 && !File.Exists(file) && File.Exists(initialRdp)) {
            var p=Import(initialRdp); p.Name="Administrator"; ActiveId=p.Id; Save();
        }
        // A fresh or empty store must not start supervision when its first profile
        // is merely saved or imported. Only Connect explicitly enables it.
        if(Items.Count==0)Enabled=false;
    }
    internal ConnectionProfile Active { get { return Items.FirstOrDefault(p=>p.Id==ActiveId) ?? Items.FirstOrDefault(); } }
    internal static string Setting(string[] lines,string key) {
        var line=lines.LastOrDefault(s=>s.StartsWith(key+":s:",StringComparison.OrdinalIgnoreCase));
        return line==null ? "" : line.Substring(key.Length+3);
    }
    internal static void Validate(string name,string address,string user) {
        if(string.IsNullOrWhiteSpace(name)) throw new ArgumentException(L.T("Escribe un nombre para identificar la conexión."));
        if(string.IsNullOrWhiteSpace(address) || address.Any(c=>char.IsWhiteSpace(c) || c=='\"' || char.IsControl(c)))
            throw new ArgumentException(L.T("Escribe un equipo o IP válido, opcionalmente seguido de :puerto."));
        if(user.Any(c=>char.IsControl(c))) throw new ArgumentException(L.T("El usuario contiene caracteres no válidos."));
    }
    internal ConnectionProfile Import(string path) {
        var lines=File.ReadAllLines(path);
        string address=Setting(lines,"full address").Trim(), user=Setting(lines,"username").Trim();
        string name=Path.GetFileNameWithoutExtension(path);
        Validate(name,address,user);
        string id=Guid.NewGuid().ToString("N");
        string template=Path.Combine("perfiles",id+".rdp");
        // Passwords remain in Windows Credential Manager, never in our profiles.
        File.WriteAllLines(Path.Combine(folder,template),lines.Where(line=>
            !line.StartsWith("password",StringComparison.OrdinalIgnoreCase) &&
            !line.StartsWith("signature:",StringComparison.OrdinalIgnoreCase) &&
            !line.StartsWith("signscope:",StringComparison.OrdinalIgnoreCase)),System.Text.Encoding.Unicode);
        var profile=new ConnectionProfile { Id=id,Name=name,Address=address,User=user,Template=template };
        Items.Add(profile); return profile;
    }
    internal string[] TemplateLines(ConnectionProfile p) {
        if(p==null || string.IsNullOrEmpty(p.Template)) return new string[0];
        string path=Path.GetFullPath(Path.Combine(folder,p.Template));
        string allowed=Path.GetFullPath(Path.Combine(folder,"perfiles"))+Path.DirectorySeparatorChar;
        if(!path.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException(L.T("Ruta de perfil no válida."));
        return File.Exists(path) ? File.ReadAllLines(path) : new string[0];
    }
    internal void Save() {
        var root=new XElement("connections",new XAttribute("active",ActiveId ?? ""),new XAttribute("enabled",Enabled));
        foreach(var p in Items) root.Add(new XElement("connection",new XAttribute("id",p.Id),
            new XElement("name",p.Name),new XElement("address",p.Address),new XElement("user",p.User),new XElement("template",p.Template ?? ""),new XElement("protectedPassword",p.ProtectedPassword),p.Resources.ToXml()));
        string target=Path.Combine(folder,"conexiones.xml"), temp=target+".tmp";
        new XDocument(root).Save(temp);
        if(File.Exists(target)) File.Replace(temp,target,target+".bak"); else File.Move(temp,target);
    }
    internal void Delete(ConnectionProfile profile) {
        var saved=Items.FirstOrDefault(p=>p.Id==profile.Id);
        if(saved==null)throw new ArgumentException(L.T("El perfil ya no existe."));
        string templatePath=null;
        if(!string.IsNullOrEmpty(saved.Template) && !Items.Any(p=>p.Id!=saved.Id && p.Template==saved.Template)) {
            templatePath=Path.GetFullPath(Path.Combine(folder,saved.Template));
            string allowed=Path.GetFullPath(Path.Combine(folder,"perfiles"))+Path.DirectorySeparatorChar;
            if(!templatePath.StartsWith(allowed,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException(L.T("Ruta de perfil no válida."));
        }
        int index=Items.IndexOf(saved);string previous=ActiveId;bool enabled=Enabled;
        Items.Remove(saved);
        if(ActiveId==saved.Id || Items.Count==0){ActiveId=Items.Count==0 ? "" : Items[0].Id;Enabled=false;}
        try {Save();}catch{Items.Insert(index,saved);ActiveId=previous;Enabled=enabled;throw;}
        // Do not leave the deleted profile's encrypted password in our rotation backup.
        File.Copy(Path.Combine(folder,"conexiones.xml"),Path.Combine(folder,"conexiones.xml.bak"),true);
        if(templatePath!=null && File.Exists(templatePath)) {
            try{File.Delete(templatePath);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
        saved.ProtectedPassword="";
    }
}

internal sealed class ConnectionManager : Form {
    readonly Supervisor supervisor;
    readonly ProfileStore store;
    readonly ComboBox profiles;
    readonly TextBox name, address, user;
    readonly TextBox password;
    readonly Label passwordHint;
    bool clearPassword;
    readonly Label status, active;
    readonly Label resourceSummary;
    readonly Dictionary<string,CheckBox> resourceChecks=new Dictionary<string,CheckBox>();
    readonly CheckBox startup;
    readonly CheckBox desktop2;
    readonly ComboBox language;
    readonly System.Windows.Forms.Timer refresh;
    ConnectionProfile editing;
    static readonly Color Navy=Color.FromArgb(19,34,55), Teal=Color.FromArgb(0,121,111), Ink=Color.FromArgb(29,43,58);
    internal ConnectionManager(Supervisor owner,ProfileStore profilesStore) {
        supervisor=owner; store=profilesStore;
        Text=L.T("AlwaysRDP — Conexiones");
        Icon=Supervisor.AppIcon;
        Font=new Font("Segoe UI",10F); AutoScaleMode=AutoScaleMode.Font;
        ClientSize=new Size(740,690); MinimumSize=new Size(720,722); StartPosition=FormStartPosition.CenterScreen;
        BackColor=Color.FromArgb(245,247,250); ForeColor=Ink;
        var root=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,94)); root.RowStyles.Add(new RowStyle(SizeType.Percent,100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,91));
        Controls.Add(root);
        var header=new Panel { Dock=DockStyle.Fill,BackColor=Navy,Margin=Padding.Empty,Width=ClientSize.Width };
        header.Controls.Add(new PictureBox { Image=Image.FromFile(Path.Combine(Supervisor.Folder,"RDP-Permanente.png")),SizeMode=PictureBoxSizeMode.Zoom,Bounds=new Rectangle(26,22,48,48) });
        header.Controls.Add(new Label { Text="AlwaysRDP",Font=new Font("Segoe UI Semibold",19F),ForeColor=Color.White,AutoSize=true,Location=new Point(90,15) });
        header.Controls.Add(new Label { Text=L.T("Tus conexiones, siempre a mano."),ForeColor=Color.FromArgb(191,210,223),AutoSize=true,Location=new Point(92,56) });
        header.Controls.Add(new Label { Text="v"+Application.ProductVersion,ForeColor=Color.FromArgb(191,210,223),AutoSize=true,Location=new Point(26,75),Font=new Font("Segoe UI",8F) });
        header.Controls.Add(new Label {Text=L.T("Idioma"),ForeColor=Color.FromArgb(191,210,223),AutoSize=true,Location=new Point(552,12),Anchor=AnchorStyles.Top|AnchorStyles.Right,Font=new Font("Segoe UI",9F)});
        language=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Bounds=new Rectangle(552,35,160,30),Anchor=AnchorStyles.Top|AnchorStyles.Right,AccessibleName=L.T("Idioma")};
        language.Items.AddRange(new object[]{"English","Español","Português"});language.SelectedIndex=Array.IndexOf(new[]{L.T("en"),"es","pt"},L.Language);header.Controls.Add(language);
        language.SelectedIndexChanged+=delegate {try{L.Change(new[]{L.T("en"),"es","pt"}[language.SelectedIndex]);}catch(Exception ex){MessageBox.Show(this,ex.Message,L.T("No se pudo guardar el idioma"),MessageBoxButtons.OK,MessageBoxIcon.Information);}};
        root.Controls.Add(header,0,0);
        var content=new TableLayoutPanel { Dock=DockStyle.Fill,Padding=new Padding(26,14,26,8),ColumnCount=1,RowCount=14,Margin=Padding.Empty };
        foreach(int height in new int[]{22,34,40,22,32,22,32,22,32,32,22,32,42,24}) content.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
        var tabs=new TabControl {Dock=DockStyle.Fill,Margin=new Padding(16,12,16,0)};
        var connectionTab=new TabPage(L.T("Conexión")) {BackColor=BackColor};
        var optionsTab=new TabPage(L.T("Recursos e inicio")) {BackColor=BackColor};
        tabs.TabPages.Add(connectionTab);tabs.TabPages.Add(optionsTab);connectionTab.Controls.Add(content);root.Controls.Add(tabs,0,1);
        AddLabel(content,L.T("CONEXIONES GUARDADAS"),0,true);
        profiles=new ComboBox { Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList,Margin=new Padding(0,0,0,7),AccessibleName=L.T("Conexiones guardadas") };
        content.Controls.Add(profiles,0,1);
        var tools=new FlowLayoutPanel { Dock=DockStyle.Fill,Margin=Padding.Empty };
        tools.Controls.Add(Button(L.T("+ Nueva conexión"),delegate { NewProfile(); },false,155));
        tools.Controls.Add(Button(L.T("Importar .rdp…"),delegate { ImportProfile(); },false,145));
        tools.Controls.Add(Button(L.T("Eliminar perfil"),delegate { DeleteSelected(); },false,135));
        content.Controls.Add(tools,0,2);
        AddLabel(content,L.T("Nombre de la conexión"),3,false); name=Field(L.T("Nombre de la conexión")); content.Controls.Add(name,0,4);
        AddLabel(content,L.T("Equipo o dirección IP"),5,false); address=Field(L.T("Equipo o dirección IP")); content.Controls.Add(address,0,6);
        AddLabel(content,L.T("Usuario"),7,false); user=Field(L.T("Usuario")); content.Controls.Add(user,0,8);
        content.Controls.Add(new Label { Text=L.T("DOMINIO\\usuario o usuario@dominio. Vacío: usar la cuenta guardada."),AutoSize=false,Dock=DockStyle.Fill,ForeColor=Color.FromArgb(88,102,117),Font=new Font("Segoe UI",9F),Margin=new Padding(0,1,0,0) },0,9);
        AddLabel(content,L.T("Contraseña"),10,false);
        var passwordRow=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
        passwordRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));passwordRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,85));
        password=Field(L.T("Contraseña"));password.UseSystemPasswordChar=true;
        passwordRow.Controls.Add(password,0,0);
        var clear=new Button {Text=L.T("Borrar"),Dock=DockStyle.Fill,Margin=new Padding(8,0,0,6),FlatStyle=FlatStyle.Flat};
        clear.Click+=delegate {password.Clear();clearPassword=true;passwordHint.Text=L.T("Se quitará la contraseña del perfil al guardar.");};passwordRow.Controls.Add(clear,1,0);content.Controls.Add(passwordRow,0,11);
        passwordHint=new Label {Dock=DockStyle.Fill,Font=new Font("Segoe UI",9F),ForeColor=Color.FromArgb(88,102,117),Margin=new Padding(0,0,0,0)};
        content.Controls.Add(passwordHint,0,12);
        resourceSummary=new Label { Text="Pantalla completa  ·  Escritorio 2  ·  Solo portapapeles",Dock=DockStyle.Fill,ForeColor=Teal,Font=new Font("Segoe UI Semibold",9F),Margin=Padding.Empty };
        content.Controls.Add(resourceSummary,0,13);
        var optionsLayout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=9,Padding=new Padding(20,16,20,10),AutoScroll=true};
        foreach(int h in new[]{26,36,194,26,36,32,28,36,50})optionsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
        optionsTab.Controls.Add(optionsLayout);
        AddLabel(optionsLayout,L.T("RECURSOS DE ESTA CONEXIÓN"),0,true);
        optionsLayout.Controls.Add(new Label {Text=L.T("Elige qué compartir con el equipo remoto. Pulsa Guardar o Conectar para aplicar."),Dock=DockStyle.Fill,Font=new Font("Segoe UI",9F),ForeColor=Color.FromArgb(88,102,117)},0,1);
        var resourcesGrid=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=6,Margin=Padding.Empty};
        resourcesGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));resourcesGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        for(int i=0;i<6;i++)resourcesGrid.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
        for(int i=0;i<SharedResources.Options.Length;i++) {
            var option=SharedResources.Options[i];
            var check=new CheckBox {Text=option.Label,Dock=DockStyle.Fill,AutoSize=true,AccessibleName=option.Label,Margin=new Padding(0,0,6,0)};
            resourceChecks.Add(option.Id,check);resourcesGrid.Controls.Add(check,i%2,i/2);
            check.CheckedChanged += delegate {UpdateResourceSummary();};
        }
        optionsLayout.Controls.Add(resourcesGrid,0,2);
        AddLabel(optionsLayout,L.T("ESCRITORIO VIRTUAL · TODAS LAS CONEXIONES"),3,true);
        desktop2=new CheckBox {Text=L.T("Mover sesiones a Escritorio 2"),Dock=DockStyle.Fill,AutoSize=true,Checked=Desktop2Controller.Enabled};
        optionsLayout.Controls.Add(desktop2,0,4);
        optionsLayout.Controls.Add(new Label {Text=L.T("Activo por defecto. Se inicia en segundo plano y mueve cada sesión nueva a Escritorio 2."),Dock=DockStyle.Fill,Font=new Font("Segoe UI",9F),ForeColor=Color.FromArgb(88,102,117)},0,5);
        desktop2.CheckedChanged+=delegate {try {Desktop2Controller.Set(desktop2.Checked);}catch(Exception ex){MessageBox.Show(this,ex.Message,L.T("Escritorio 2"),MessageBoxButtons.OK,MessageBoxIcon.Information);}};
        AddLabel(optionsLayout,L.T("INICIO DE WINDOWS · TODAS LAS CONEXIONES"),6,true);
        startup=new CheckBox {Text=L.T("Iniciar AlwaysRDP al entrar en Windows"),Dock=DockStyle.Fill,AutoSize=true,Checked=StartupSettings.Enabled};
        optionsLayout.Controls.Add(startup,0,7);
        optionsLayout.Controls.Add(new Label {Text=L.T("Se inicia minimizado en la bandeja y recupera la última conexión si no la dejaste detenida."),Dock=DockStyle.Fill,Font=new Font("Segoe UI",9F),ForeColor=Color.FromArgb(88,102,117)},0,8);
        startup.CheckedChanged += delegate {
            if(changingStartup)return;
            try {StartupSettings.Set(startup.Checked);}
            catch(Exception ex) {changingStartup=true;try{startup.Checked=StartupSettings.Enabled;}finally{changingStartup=false;}MessageBox.Show(this,ex.Message,L.T("Inicio de Windows"),MessageBoxButtons.OK,MessageBoxIcon.Information);}
        };
        var footer=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Padding=new Padding(26,7,22,12),BackColor=Color.White,Margin=Padding.Empty };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute,24)); footer.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        status=new Label { Dock=DockStyle.Fill,Font=new Font("Segoe UI Semibold",10F),ForeColor=Teal,Text=L.T("Preparando…"),Margin=Padding.Empty };
        active=new Label { Dock=DockStyle.Fill,Font=new Font("Segoe UI",9F),ForeColor=Color.FromArgb(88,102,117),Margin=Padding.Empty,AutoEllipsis=true };
        footer.Controls.Add(status,0,0); footer.Controls.Add(active,0,1);
        var actions=new FlowLayoutPanel { AutoSize=true,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,Dock=DockStyle.Fill,Margin=Padding.Empty };
        actions.Controls.Add(Button(L.T("Guardar"),delegate { SaveProfile(false); },false,88));
        actions.Controls.Add(Button(L.T("Conectar"),delegate { SaveProfile(true); },true,98));
        actions.Controls.Add(Button(L.T("Ver sesión"),delegate { supervisor.ShowSession(); },false,100));
        actions.Controls.Add(Button(L.T("Detener"),delegate { supervisor.PauseSession(); UpdateStatus(); },false,85));
        footer.Controls.Add(actions,1,0); footer.SetRowSpan(actions,2); root.Controls.Add(footer,0,2);
        profiles.SelectedIndexChanged += delegate { LoadProfile(profiles.SelectedItem as ConnectionProfile); };
        Reload(store.Active);
        refresh=new System.Windows.Forms.Timer { Interval=500 }; refresh.Tick += delegate { UpdateStatus(); }; refresh.Start();
        FormClosing += delegate(object sender,FormClosingEventArgs e) { e.Cancel=true; Hide(); };
        UpdateStatus();
        L.Changed+=ApplyLanguage;ApplyLanguage();
    }
    static Button Button(string text,Action action,bool primary,int width) {
        var b=new Button { Text=text,Width=width,Height=36,FlatStyle=FlatStyle.Flat,BackColor=primary ? Teal : Color.White,ForeColor=primary ? Color.White : Ink,Margin=new Padding(0,0,8,0),Cursor=Cursors.Hand };
        b.FlatAppearance.BorderColor=primary ? Teal : Color.FromArgb(208,217,225); b.Click += delegate { action(); }; return b;
    }
    static void AddLabel(TableLayoutPanel p,string text,int row,bool heading) {
        p.Controls.Add(new Label { Text=text,Dock=DockStyle.Fill,Margin=Padding.Empty,Font=new Font(heading ? "Segoe UI Semibold" : "Segoe UI",heading ? 9F : 10F),ForeColor=heading ? Teal : Ink },0,row);
    }
    static TextBox Field(string label) { return new TextBox { Dock=DockStyle.Fill,Margin=new Padding(0,0,0,6),AccessibleName=label,BorderStyle=BorderStyle.FixedSingle }; }
    void Reload(ConnectionProfile selected) {
        profiles.Items.Clear(); foreach(var p in store.Items) profiles.Items.Add(p);
        if(selected!=null) profiles.SelectedItem=selected; else NewProfile();
    }
    void LoadProfile(ConnectionProfile p) {
        editing=p; name.Text=p==null ? "" : p.Name; address.Text=p==null ? "" : p.Address; user.Text=p==null ? "" : p.User;
        password.Clear();clearPassword=false;passwordHint.Text=p!=null && p.HasPassword ? L.T("Contraseña guardada y cifrada por Windows. Vacío: conservarla.") : L.T("Se guarda cifrada para tu usuario de Windows. Vacío: usar las credenciales de Windows.");
        var resources=p==null ? new SharedResources() : p.Resources;
        foreach(var option in SharedResources.Options)resourceChecks[option.Id].Checked=resources.Get(option.Id);
        UpdateResourceSummary();
    }
    bool changingStartup;
    SharedResources ReadResources() {var resources=new SharedResources();foreach(var pair in resourceChecks)resources.Set(pair.Key,pair.Value.Checked);return resources;}
    void UpdateResourceSummary() {if(resourceChecks.Count==SharedResources.Options.Length)resourceSummary.Text=L.F("Pantalla completa  ·  {0}",ReadResources().Summary);}
    void NewProfile() { profiles.SelectedIndex=-1; LoadProfile(null); name.Focus(); }
    void DeleteSelected() {
        if(editing==null){MessageBox.Show(this,L.T("Selecciona un perfil guardado para eliminarlo."),L.T("Eliminar perfil"),MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
        if(MessageBox.Show(this,L.F("¿Eliminar el perfil «{0}» y su contraseña guardada?",editing.Name),L.T("Eliminar perfil"),MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
        try{supervisor.DeleteProfile(editing);Reload(store.Active);UpdateStatus();}
        catch(Exception ex){MessageBox.Show(this,ex.Message,L.T("No se pudo eliminar el perfil"),MessageBoxButtons.OK,MessageBoxIcon.Information);}
    }
    void ImportProfile() {
        using(var dialog=new OpenFileDialog { Title=L.T("Importar una conexión RDP"),Filter=L.T("Conexión de Escritorio remoto (*.rdp)|*.rdp"),InitialDirectory=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) }) {
            if(dialog.ShowDialog(this)!=DialogResult.OK) return;
            try { var p=store.Import(dialog.FileName); store.Save(); Reload(p); }
            catch(Exception ex) { MessageBox.Show(this,ex.Message,L.T("No se pudo importar"),MessageBoxButtons.OK,MessageBoxIcon.Information); }
        }
    }
    void SaveProfile(bool connect) {
        try {
            ProfileStore.Validate(name.Text.Trim(),address.Text.Trim(),user.Text.Trim());
            if(password.Text.Length>0 && string.IsNullOrWhiteSpace(user.Text))throw new ArgumentException(L.T("Escribe el usuario al que corresponde la contraseña."));
            if(editing==null) { editing=new ConnectionProfile { Id=Guid.NewGuid().ToString("N"),Template="" }; store.Items.Add(editing); }
            bool identityChanged=editing.Address!=address.Text.Trim() || editing.User!=user.Text.Trim();
            if(identityChanged || clearPassword)editing.ProtectedPassword="";
            editing.Name=name.Text.Trim(); editing.Address=address.Text.Trim(); editing.User=user.Text.Trim();
            if(password.Text.Length>0)editing.ProtectedPassword=ProfileSecret.Encrypt(editing,password.Text);
            password.Clear();
            editing.Resources=ReadResources();
            store.Save(); Reload(editing);
            if(connect) supervisor.ConnectProfile(editing);
            else { status.Text=L.T("Conexión guardada"); active.Text=L.T("Pulsa Conectar para usarla."); }
        } catch(Exception ex) { MessageBox.Show(this,ex.Message,L.T("Revisar conexión"),MessageBoxButtons.OK,MessageBoxIcon.Information); }
    }
    void UpdateStatus() { status.Text=supervisor.StateText; active.Text=supervisor.ActiveName; }
    void ApplyLanguage() {L.Apply(this);UpdateStatus();UpdateResourceSummary();}
    protected override void Dispose(bool disposing) {if(disposing)L.Changed-=ApplyLanguage; if(disposing && refresh!=null) refresh.Dispose(); base.Dispose(disposing); }
}
