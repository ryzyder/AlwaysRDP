# Third-party software

## VirtualDesktopAccessor

- Upstream: https://github.com/Ciantic/VirtualDesktopAccessor
- Upstream author: Jari Otto Oskari Pennanen, with upstream contributors.
- License: MIT; the complete notice is in `third_party/VirtualDesktopAccessor/LICENSE.txt`.
- Pinned release: `2024-12-16-windows11`.
- Binary: `VirtualDesktopAccessor.dll`, x64.
- Download: https://github.com/Ciantic/VirtualDesktopAccessor/releases/download/2024-12-16-windows11/VirtualDesktopAccessor.dll
- SHA-256: `8740C572A1C000E3B87FFEB1E4C397EAE9AF3BD4A2ABDC3BCFFACAB4493F8FF5`.

The binary is retrieved during the build, verified against that hash and copied into the output folder. It is not included in the source repository. Its MIT notice must accompany distributed copies. Its upstream license is not superseded by AlwaysRDP's proprietary notice.

Windows, Remote Desktop Connection (`mstsc.exe`), Windows Credential Manager and .NET Framework are Microsoft components used from the user's Windows installation. They are not redistributed as part of this source project.
