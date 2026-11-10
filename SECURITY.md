# Security and private data

Do not commit real connection profiles, passwords, Windows credential exports, tokens, private keys, RDP files, logs or live configuration backups. Runtime data is intentionally excluded from this source project and its archive.

Passwords are protected with Windows DPAPI for the current Windows account. That encryption does not make a profile appropriate for publication, and it does not replace access controls on the local device.

The application temporarily places a profile credential in Windows Credential Manager for the target server and normally restores the previous entry. Avoid forcibly terminating the supervisor during credential handoff. The remote server and Windows authentication policy remain authoritative.

The build retrieves a pinned native library only over HTTPS and checks its SHA-256 before use. Update both the pinned release and expected hash deliberately when maintaining that dependency.

Report vulnerabilities privately to the repository owner using an established private channel. Do not include real passwords or live profile files in issues or pull requests.
