// Copyright (c) 2026 ryzyder. All rights reserved.
// Project-owned portions are subject to the root LICENSE; third-party notices remain applicable.
using System;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.ComponentModel;

internal static class ProfileSecret {
    static byte[] Entropy(ConnectionProfile p) {return Encoding.UTF8.GetBytes("RDP-Permanente|"+p.Id+"|"+p.Address+"|"+p.User);}
    internal static string Encrypt(ConnectionProfile p,string password) {
        byte[] bytes=Encoding.Unicode.GetBytes(password);
        try {return Convert.ToBase64String(ProtectedData.Protect(bytes,Entropy(p),DataProtectionScope.CurrentUser));}
        finally {Array.Clear(bytes,0,bytes.Length);}
    }
    internal static byte[] Decrypt(ConnectionProfile p) {
        try {return ProtectedData.Unprotect(Convert.FromBase64String(p.ProtectedPassword),Entropy(p),DataProtectionScope.CurrentUser);}
        catch(CryptographicException) {throw new InvalidOperationException(L.T("No se puede abrir la contraseña guardada. Vuelve a escribirla y guarda la conexión."));}
    }
}

// Supplies the selected profile to mstsc without putting a password in arguments
// or RDP files. Restores the previous Windows credential once login completes.
internal sealed class CredentialLease : IDisposable {
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Credential {
        internal uint Flags,Type; internal string TargetName,Comment;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        internal uint BlobSize; internal IntPtr Blob; internal uint Persist,AttributeCount;
        internal IntPtr Attributes; internal string TargetAlias,UserName;
    }
    sealed class Saved : IDisposable {
        internal string Target,User,Comment,Alias;internal uint Flags,Persist;internal byte[] Blob;
        public void Dispose(){if(Blob!=null){Array.Clear(Blob,0,Blob.Length);Blob=null;}}
    }
    [DllImport("advapi32.dll",EntryPoint="CredReadW",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern bool CredRead(string target,uint type,uint flags,out IntPtr credential);
    [DllImport("advapi32.dll",EntryPoint="CredWriteW",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern bool CredWrite(ref Credential credential,uint flags);
    [DllImport("advapi32.dll",EntryPoint="CredDeleteW",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern bool CredDelete(string target,uint type,uint flags);
    [DllImport("advapi32.dll")]static extern void CredFree(IntPtr p);
    Saved original,injected;
    CredentialLease(){}
    static string Target(ConnectionProfile p){return "TERMSRV/"+p.Address;}
    static Saved Read(string target) {
        IntPtr pointer;
        if(!CredRead(target,1,0,out pointer)) {
            int error=Marshal.GetLastWin32Error();if(error==1168)return null;throw new Win32Exception(error);
        }
        try {
            var c=(Credential)Marshal.PtrToStructure(pointer,typeof(Credential));
            if(c.AttributeCount!=0)throw new InvalidOperationException(L.T("La credencial de Windows tiene atributos especiales. Usa el diálogo de Windows para esta conexión."));
            var s=new Saved {Target=c.TargetName,User=c.UserName,Comment=c.Comment,Alias=c.TargetAlias,Flags=c.Flags,Persist=c.Persist,Blob=new byte[c.BlobSize]};
            if(c.BlobSize>0)Marshal.Copy(c.Blob,s.Blob,0,s.Blob.Length);return s;
        } finally {CredFree(pointer);}
    }
    static void Write(Saved s) {
        IntPtr pointer=Marshal.AllocCoTaskMem(s.Blob.Length);
        try {
            Marshal.Copy(s.Blob,0,pointer,s.Blob.Length);
            var c=new Credential {Flags=s.Flags,Type=1,TargetName=s.Target,UserName=s.User,Comment=s.Comment,TargetAlias=s.Alias,BlobSize=(uint)s.Blob.Length,Blob=pointer,Persist=s.Persist};
            if(!CredWrite(ref c,0))throw new Win32Exception(Marshal.GetLastWin32Error());
        } finally {for(int i=0;i<s.Blob.Length;i++)Marshal.WriteByte(pointer,i,0);Marshal.FreeCoTaskMem(pointer);}
    }
    internal static bool HasMatchingUser(ConnectionProfile p) {
        if(string.IsNullOrWhiteSpace(p.User))return true;
        using(var s=Read(Target(p))) {
            if(s==null)return false;
            return string.Equals(s.User,p.User,StringComparison.OrdinalIgnoreCase) ||
                (!p.User.Contains("\\") && !p.User.Contains("@") && (s.User ?? "").EndsWith("\\"+p.User,StringComparison.OrdinalIgnoreCase));
        }
    }
    internal static CredentialLease Install(ConnectionProfile p) {
        if(!p.HasPassword)return null;
        if(string.IsNullOrWhiteSpace(p.User))throw new InvalidOperationException(L.T("Escribe también el usuario de esta contraseña."));
        var lease=new CredentialLease();
        try {
            lease.original=Read(Target(p));
            lease.injected=new Saved {Target=Target(p),User=p.User,Blob=ProfileSecret.Decrypt(p),Persist=1};
            Write(lease.injected);return lease;
        } catch {if(lease.original!=null)lease.original.Dispose();if(lease.injected!=null)lease.injected.Dispose();throw;}
    }
    public void Dispose() {
        if(injected==null)return;
        try {
            using(var current=Read(injected.Target)) {
                // Do not overwrite a credential changed by the user or another app.
                if(current!=null && current.User==injected.User && current.Blob.SequenceEqual(injected.Blob)) {
                    if(original!=null)Write(original);
                    else if(!CredDelete(injected.Target,1,0) && Marshal.GetLastWin32Error()!=1168)
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        } finally {if(original!=null)original.Dispose();injected.Dispose();original=null;injected=null;}
    }
}

internal static class RecoveryPolicy {
    internal static bool NeedsAttention(bool loginComplete,int reason) {
        if(new[]{2055,2567,2823,3079,3335,3591,3847,4615,5639,5895,6151,8455}.Contains(reason))return true;
        if(loginComplete)return false;
        // A confirmed network error may recover. An unclassified pre-login
        // failure or cancellation needs user input, rather than an endless loop.
        return !new[]{260,264,516,520,772,1028,1288,1540,1796,2308}.Contains(reason);
    }
}

internal sealed class DisconnectGuard {
    internal bool Pending {get;private set;}
    internal int Reason {get;private set;}
    DateTime at;
    internal void Disconnected(int reason,DateTime now) {Pending=true;Reason=reason;at=now;}
    internal void Progress() {Pending=false;Reason=0;}
    internal bool Settled(DateTime now) {return Pending && now-at>=TimeSpan.FromSeconds(8);}
}
