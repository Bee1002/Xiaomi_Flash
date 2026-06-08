# Architecture — Xiaomi Flash

## Folder layout

```
Xiaomi_Flash/
├── App.xaml, MainWindow.xaml     # Application shell (v2 UI)
├── Engine/                       # Fastboot + payload core (no WPF)
├── Generated/                    # Protobuf (do not edit)
├── Ui/                           # v2 workflow, device service, controls
├── Legacy/                       # Hidden legacy UI hosts + handlers
├── ThirdParty/                   # BZip2, LZMA
├── Assets/                       # fastboot.exe, native DLLs, icon, fonts (source)
├── Data/                         # xiaomi_codenames.json
├── Properties/                   # Resources.resx
├── Themes/                       # Cyberpunk XAML resources
└── tools/                        # Build-Production.ps1, XiaomiFlash.iss (installer)
```

## Entry points for contributors

| Task | Start here |
|------|------------|
| LOAD / START / flash workflow | `Ui/FastbootFlashSession.cs` |
| Device panel / USB detection | `Ui/FastbootDeviceService.cs`, `Ui/FastbootAutoProbe.cs` |
| ROM scripts / payload choice | `Ui/RomPackageResolver.cs`, `Ui/RomBatScriptParser.cs` |
| Fastboot process / gate | `Engine/Fastboot.cs`, `Engine/FastbootGate.cs` |
| payload.bin extract + flash | `Engine/Payload.cs`, `Ui/PayloadFlashExecutor.cs` |
| Legacy manual flash (hidden) | `Legacy/FastbootUI.cs` |
| Payload dumper (hidden) | `Legacy/PayloadUI.cs` |

## v2 vs legacy

- **v2** uses `FastbootDeviceService` (public API) and never touches legacy XAML directly.
- **Legacy** controls live in a collapsed grid in `MainWindow.xaml`; `Legacy/FastbootUI.init()` wires their events.
- Shared device state (poll thread, `getvar` cache) lives inside `Legacy/FastbootUI.cs` until a future split.
