// Copyright (c) 2026 ryzyder. All rights reserved.
// Project-owned portions are subject to the root LICENSE; third-party notices remain applicable.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml.Linq;

internal static class L {
    internal static string Language="es";
    static string preferences;
    internal static event Action Changed;
    static readonly string[][] Rows={
        new[]{"AlwaysRDP — Conexiones","AlwaysRDP — Connections","AlwaysRDP — Conexões"},
        new[]{"Tus conexiones, siempre a mano.","Your connections, always within reach.","Suas conexões, sempre à mão."},
        new[]{"Idioma","Language","Idioma"},
        new[]{"Conexión","Connection","Conexão"},
        new[]{"Recursos e inicio","Sharing & startup","Recursos e inicialização"},
        new[]{"CONEXIONES GUARDADAS","SAVED CONNECTIONS","CONEXÕES SALVAS"},
        new[]{"Conexiones guardadas","Saved connections","Conexões salvas"},
        new[]{"+ Nueva conexión","+ New connection","+ Nova conexão"},
        new[]{"Importar .rdp…","Import .rdp…","Importar .rdp…"},
        new[]{"Eliminar perfil","Delete profile","Excluir perfil"},
        new[]{"Nombre de la conexión","Connection name","Nome da conexão"},
        new[]{"Equipo o dirección IP","Computer or IP address","Computador ou endereço IP"},
        new[]{"Usuario","Username","Usuário"},
        new[]{"DOMINIO\\usuario o usuario@dominio. Vacío: usar la cuenta guardada.","DOMAIN\\user or user@domain. Leave blank to use the saved account.","DOMÍNIO\\usuário ou usuário@domínio. Vazio: usar a conta salva."},
        new[]{"Contraseña","Password","Senha"},
        new[]{"Borrar","Clear","Limpar"},
        new[]{"Se quitará la contraseña del perfil al guardar.","The profile password will be removed when you save.","A senha do perfil será removida ao salvar."},
        new[]{"Contraseña guardada y cifrada por Windows. Vacío: conservarla.","Password saved and encrypted by Windows. Leave blank to keep it.","Senha salva e criptografada pelo Windows. Deixe vazio para mantê-la."},
        new[]{"Se guarda cifrada para tu usuario de Windows. Vacío: usar las credenciales de Windows.","Encrypted for your Windows account. Leave blank to use Windows credentials.","Criptografada para seu usuário do Windows. Vazio: usar as credenciais do Windows."},
        new[]{"Pantalla completa  ·  {0}","Full screen  ·  {0}","Tela cheia  ·  {0}"},
        new[]{"RECURSOS DE ESTA CONEXIÓN","SHARING FOR THIS CONNECTION","RECURSOS DESTA CONEXÃO"},
        new[]{"Elige qué compartir con el equipo remoto. Pulsa Guardar o Conectar para aplicar.","Choose what to share with the remote computer. Click Save or Connect to apply.","Escolha o que compartilhar com o computador remoto. Clique em Salvar ou Conectar."},
        new[]{"Portapapeles (copiar y pegar)","Clipboard (copy and paste)","Área de transferência (copiar e colar)"},
        new[]{"Unidades y discos","Drives and disks","Unidades e discos"},
        new[]{"Impresoras","Printers","Impressoras"},
        new[]{"Sonido del equipo remoto","Remote computer audio","Áudio do computador remoto"},
        new[]{"Micrófono","Microphone","Microfone"},
        new[]{"Cámaras","Cameras","Câmeras"},
        new[]{"Tarjetas inteligentes","Smart cards","Cartões inteligentes"},
        new[]{"Puertos COM","COM ports","Portas COM"},
        new[]{"USB y otros dispositivos","USB and other devices","USB e outros dispositivos"},
        new[]{"Ubicación","Location","Localização"},
        new[]{"Windows Hello / WebAuthn","Windows Hello / WebAuthn","Windows Hello / WebAuthn"},
        new[]{"INICIO DE WINDOWS · TODAS LAS CONEXIONES","WINDOWS STARTUP · ALL CONNECTIONS","INICIALIZAÇÃO DO WINDOWS · TODAS AS CONEXÕES"},
        new[]{"Iniciar AlwaysRDP al entrar en Windows","Start AlwaysRDP when I sign in to Windows","Iniciar AlwaysRDP ao entrar no Windows"},
        new[]{"Se inicia en segundo plano. Recupera la última conexión si no la dejaste detenida.\r\nEsta opción se aplica al marcarla; los recursos se guardan por conexión.","Runs in the background. Resumes the last connection unless you stopped it.\r\nThis option applies immediately; sharing settings are saved per connection.","Inicia em segundo plano. Retoma a última conexão se ela não foi interrompida.\r\nEsta opção é aplicada imediatamente; os recursos são salvos por conexão."},
        new[]{"Inicio de Windows","Windows startup","Inicialização do Windows"},
        new[]{"Preparando…","Preparing…","Preparando…"},
        new[]{"Guardar","Save","Salvar"},
        new[]{"Conectar","Connect","Conectar"},
        new[]{"Ver sesión","View session","Ver sessão"},
        new[]{"Detener","Stop","Parar"},
        new[]{"Selecciona un perfil guardado para eliminarlo.","Select a saved profile to delete.","Selecione um perfil salvo para excluir."},
        new[]{"¿Eliminar el perfil «{0}» y su contraseña guardada?","Delete profile “{0}” and its saved password?","Excluir o perfil “{0}” e sua senha salva?"},
        new[]{"No se pudo eliminar el perfil","Could not delete profile","Não foi possível excluir o perfil"},
        new[]{"Importar una conexión RDP","Import an RDP connection","Importar uma conexão RDP"},
        new[]{"Conexión de Escritorio remoto (*.rdp)|*.rdp","Remote Desktop connection (*.rdp)|*.rdp","Conexão de Área de Trabalho Remota (*.rdp)|*.rdp"},
        new[]{"No se pudo importar","Could not import","Não foi possível importar"},
        new[]{"Escribe el usuario al que corresponde la contraseña.","Enter the username for this password.","Informe o usuário desta senha."},
        new[]{"Conexión guardada","Connection saved","Conexão salva"},
        new[]{"Pulsa Conectar para usarla.","Click Connect to use it.","Clique em Conectar para usá-la."},
        new[]{"Revisar conexión","Check connection","Verificar conexão"},
        new[]{"Escribe un nombre para identificar la conexión.","Enter a name for this connection.","Informe um nome para identificar a conexão."},
        new[]{"Escribe un equipo o IP válido, opcionalmente seguido de :puerto.","Enter a valid computer or IP address, optionally followed by :port.","Informe um computador ou IP válido, opcionalmente seguido de :porta."},
        new[]{"El usuario contiene caracteres no válidos.","The username contains invalid characters.","O usuário contém caracteres inválidos."},
        new[]{"Ruta de perfil no válida.","Invalid profile path.","Caminho de perfil inválido."},
        new[]{"El perfil ya no existe.","The profile no longer exists.","O perfil não existe mais."},
        new[]{"Sin conexión seleccionada","No connection selected","Nenhuma conexão selecionada"},
        new[]{"Activa: {0}","Active: {0}","Ativa: {0}"},
        new[]{"Sin recursos compartidos","No sharing","Nenhum recurso compartilhado"},
        new[]{"Solo portapapeles","Clipboard only","Somente área de transferência"},
        new[]{"{0} recursos compartidos","{0} shared resources","{0} recursos compartilhados"},
        new[]{"Iniciando","Starting","Iniciando"},
        new[]{"detenida","Stopped","Parada"},
        new[]{"sin perfiles","No profiles","Sem perfis"},
        new[]{"conectado","Connected","Conectado"},
        new[]{"autenticando","Signing in","Autenticando"},
        new[]{"acceso pendiente ({0})","Sign-in pending ({0})","Acesso pendente ({0})"},
        new[]{"error; revisa la conexión","Error; check connection","Erro; verifique a conexão"},
        new[]{"esperando credenciales","Credentials needed","Credenciais necessárias"},
        new[]{"conectando","Connecting","Conectando"},
        new[]{"revisa usuario y contraseña","Check username and password","Verifique o usuário e a senha"},
        new[]{"reintento en {0} segundos","Retrying in {0} seconds","Nova tentativa em {0} segundos"},
        new[]{"Conexiones…","Connections…","Conexões…"},
        new[]{"Ver sesión","View session","Ver sessão"},
        new[]{"Detener sesión","Stop session","Parar sessão"},
        new[]{"Detener y cerrar esta conexión","Stop and exit AlwaysRDP","Parar e sair do AlwaysRDP"},
        new[]{"Agrega una conexión en AlwaysRDP.","Add a connection in AlwaysRDP.","Adicione uma conexão no AlwaysRDP."},
        new[]{"La dirección de Administrator.rdp no es válida para una conexión directa.","The connection address is not valid for a direct connection.","O endereço da conexão não é válido para uma conexão direta."},
        new[]{"No se puede acceder a los escritorios virtuales.","Cannot access virtual desktops.","Não é possível acessar as áreas de trabalho virtuais."},
        new[]{"No se pudo crear el escritorio 2.","Could not create Desktop 2.","Não foi possível criar a área de trabalho 2."},
        new[]{"Recurso no reconocido","Unknown resource","Recurso desconhecido"},
        new[]{"Ya existe un acceso de inicio con ese nombre para otra aplicación.","A startup shortcut with that name already belongs to another app.","Já existe um atalho de inicialização com esse nome para outro aplicativo."},
        new[]{"AlwaysRDP en segundo plano al iniciar sesión","AlwaysRDP in the background at sign-in","AlwaysRDP em segundo plano ao entrar no Windows"},
        new[]{"No se puede abrir la contraseña guardada. Vuelve a escribirla y guarda la conexión.","Cannot decrypt the saved password. Enter it again and save the connection.","Não é possível descriptografar a senha salva. Digite-a novamente e salve a conexão."},
        new[]{"La credencial de Windows tiene atributos especiales. Usa el diálogo de Windows para esta conexión.","This Windows credential has special attributes. Use the Windows sign-in dialog.","Esta credencial do Windows possui atributos especiais. Use a janela de acesso do Windows."},
        new[]{"Escribe también el usuario de esta contraseña.","Also enter the username for this password.","Informe também o usuário desta senha."},
        new[]{"No se pudo guardar el idioma","Could not save language","Não foi possível salvar o idioma"},
        new[]{"Windows no respondió al consultar el inicio automático.","Windows did not respond when checking startup settings.","O Windows não respondeu ao consultar a inicialização."},
        new[]{"Preferencias de RDP de Windows","Windows RDP preferences","Preferências de RDP do Windows"},
        new[]{"Para aplicar la conexión y los recursos elegidos, AlwaysRDP actualizará los valores predeterminados de Conexión a Escritorio remoto de Windows. Esto puede afectar a otras conexiones que abras con mstsc sin un archivo RDP propio. Guardaremos una copia previa de la configuración. ¿Quieres permitirlo?","To apply your connection and sharing choices, AlwaysRDP will update Windows Remote Desktop Connection defaults. This can affect other connections you open with mstsc without their own RDP file. A copy of the previous settings will be kept. Do you want to allow this?","Para aplicar a conexão e os recursos escolhidos, AlwaysRDP atualizará os padrões da Conexão de Área de Trabalho Remota do Windows. Isso pode afetar outras conexões que você abrir com mstsc sem um arquivo RDP próprio. Uma cópia das configurações anteriores será mantida. Deseja permitir?"},
        new[]{"Abre AlwaysRDP y autoriza las preferencias de RDP antes de conectar.","Open AlwaysRDP and approve the RDP preferences before connecting.","Abra AlwaysRDP e autorize as preferências de RDP antes de conectar."},
        new[]{"Habilita AlwaysRDP en Configuración de Windows > Aplicaciones > Inicio. Windows mantiene el control de esta opción.","Enable AlwaysRDP in Windows Settings > Apps > Startup. Windows remains in control of this setting.","Ative AlwaysRDP em Configurações do Windows > Aplicativos > Inicialização. O Windows mantém o controle desta opção."},
        new[]{"ESCRITORIO VIRTUAL · TODAS LAS CONEXIONES","VIRTUAL DESKTOP · ALL CONNECTIONS","ÁREA DE TRABALHO VIRTUAL · TODAS AS CONEXÕES"},
        new[]{"Escritorio 2","Desktop 2","Área de trabalho 2"},
        new[]{"Mover sesiones a Escritorio 2","Move sessions to Desktop 2","Mover sessões para a Área de trabalho 2"},
        new[]{"Activo por defecto. Se inicia en segundo plano y mueve cada sesión nueva a Escritorio 2.","Enabled by default. It starts in the background and moves each new session to Desktop 2.","Ativo por padrão. Inicia em segundo plano e move cada nova sessão para a Área de trabalho 2."},
        new[]{"¿Quieres que AlwaysRDP se inicie con Windows y permanezca en la bandeja del sistema?","Do you want AlwaysRDP to start with Windows and remain in the system tray?","Deseja que o AlwaysRDP inicie com o Windows e permaneça na bandeja do sistema?"},
        new[]{"Se inicia minimizado en la bandeja y recupera la última conexión si no la dejaste detenida.","It starts minimized in the system tray and resumes the last connection unless you stopped it.","Inicia minimizado na bandeja do sistema e retoma a última conexão, caso você não a tenha interrompido."}
    };
    internal static IEnumerable<string> Keys {get{return Rows.Select(r=>r[0]);}}
    internal static string T(string text) {
        if(text==null)return "";
        int column=Language=="en"?1:Language=="pt"?2:0;
        var row=Rows.FirstOrDefault(r=>r.Any(s=>string.Equals(s,text,StringComparison.OrdinalIgnoreCase)));
        return row==null?text:row[column];
    }
    internal static string F(string key,params object[] args) {return string.Format(T(key),args);}
    internal static void Initialize(string folder) {
        preferences=Path.Combine(folder,"preferences.xml");Language="es";
        if(File.Exists(preferences))try {var code=(string)XDocument.Load(preferences).Root.Attribute("language");if(new[]{"en","es","pt"}.Contains(code))Language=code;}catch(System.Xml.XmlException){}
    }
    internal static void Change(string code) {
        if(!new[]{"en","es","pt"}.Contains(code))throw new ArgumentException("Invalid language");
        if(preferences!=null) {
            string temp=preferences+".tmp";new XDocument(new XElement("preferences",new XAttribute("language",code))).Save(temp);
            if(File.Exists(preferences))File.Replace(temp,preferences,null);else File.Move(temp,preferences);
        }
        Language=code;var changed=Changed;if(changed!=null)changed();
    }
    internal static void Apply(Control control) {
        if(!(control is TextBoxBase) && !(control is ComboBox))control.Text=T(control.Text);
        if(!string.IsNullOrEmpty(control.AccessibleName))control.AccessibleName=T(control.AccessibleName);
        foreach(Control child in control.Controls)Apply(child);
    }
    internal static void Apply(ToolStripItem item) {
        item.Text=T(item.Text);var drop=item as ToolStripDropDownItem;
        if(drop!=null)foreach(ToolStripItem child in drop.DropDownItems)Apply(child);
    }
}
