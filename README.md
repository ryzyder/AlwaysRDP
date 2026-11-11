# AlwaysRDP

<img src="assets/RDP-Permanente.png" alt="AlwaysRDP logo" width="96">

Persistent Windows Remote Desktop connections, with **English, Español and Português** interfaces.

Project owner and maintainer: **[ryzyder](https://github.com/ryzyder)**. Copyright © 2026 ryzyder. All rights reserved, subject to the separate third-party notices below.

## Features

- Multiple saved connection profiles, including separate usernames and optional encrypted passwords.
- Full-screen sessions with an included Desktop 2 helper that runs in the background by default.
- Automatic recovery after a real client exit or network interruption.
- A grace period for intermediate RDP events, preventing premature client termination during sign-in.
- Optional Windows startup and per-profile resource sharing. **Clipboard only** is the default.
- Profile deletion, including the saved encrypted password, with confirmation.
- A live language selector that preserves edited fields and the running session.

Only one RDP session is supervised at a time. Windows or the remote server can still require authentication, impose sharing restrictions, or reject access. AlwaysRDP does not bypass those restrictions.

## Requirements

- Windows 11 x64, version 24H2 or later, with compatible virtual-desktop interfaces.
- .NET Framework 4.8 and the built-in `mstsc.exe` client.
- Network access to a Windows host that permits Remote Desktop for the selected account.
- Internet access on the first build to retrieve the pinned VirtualDesktopAccessor dependency.

The third-party desktop library uses Windows interfaces that may change with Windows updates. Compatibility with every future build is not guaranteed.

## Build and test

From PowerShell in the repository root:

```powershell
.\build.ps1 -Test
```

The script compiles the app with the Windows .NET Framework compiler, verifies the native dependency's SHA-256, copies runtime assets and notices, and runs synthetic regression tests. Output: `build/Release/AlwaysRDP.exe`.

For the standalone portable build, run `./build-portable.ps1 -Test`. It includes the Desktop 2 helper and its pinned native dependency; Windows startup remains a Store-only capability.

You can also open `AlwaysRDP.sln` in Visual Studio with the .NET desktop development workload and .NET Framework 4.8 targeting pack. Run `build.ps1` first to obtain the native dependency.

Tests do not connect to real RDP servers. Credential tests use randomly generated `.invalid` destinations and temporary synthetic Windows Credential Manager entries, then restore or remove them.

## Use

Launch the executable, choose a language, and create or import a connection. Enter its computer/IP and username; optionally enter its password. **Save** stores the profile, **Connect** activates it, **View session** opens Desktop 2, and **Stop** pauses it. Closing the manager leaves supervision running in the tray.

Under **Sharing & startup**, choose optional sharing resources and Windows startup. Profiles start with clipboard sharing only, including imported profiles.

- [English guide](docs/guide.en.txt)
- [Guía en español](docs/guide.es.txt)
- [Guia em português](docs/guide.pt.txt)

## Private runtime data

Runtime profiles, encrypted credentials, RDP templates, preferences, logs and backups are intentionally absent from this repository. They are also excluded by `.gitignore`. Never add your live application folder wholesale to Git.

Passwords are protected with Windows DPAPI for the current Windows user. They are not portable merely by copying profile files to another account or computer. The credential bridge temporarily supplies the selected credential to Windows and restores the previous entry after sign-in or normal client cleanup; abrupt application termination may interrupt cleanup.

The application retains some legacy internal filenames and the original DPAPI entropy identifier to preserve compatibility with existing AlwaysRDP profiles. Do not change the entropy identifier without a migration plan.

## Ownership, license and provenance

This repository is publicly visible for transparency, issue reporting and community feedback. It is **source-available, not open source**: the project-owned portions remain under the proprietary terms in [LICENSE](LICENSE). See [AUTHORS.md](AUTHORS.md) and [COPYRIGHT.md](COPYRIGHT.md) for ownership, provenance and third-party notices. Public visibility does not grant permission to redistribute, sell, repackage or create derivative works.

Git history and the source manifest document this repository version; they are not an official copyright registration or a legal determination of authorship. The project was developed using AI-assisted tooling under the maintainer's direction. See the authorship notes for the distinction between project ownership and legal copyright eligibility.

The VirtualDesktopAccessor dependency remains under its own MIT license. See [THIRD_PARTY.md](THIRD_PARTY.md). The proprietary notice does not replace or restrict that license.
