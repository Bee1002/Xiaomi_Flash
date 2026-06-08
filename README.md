# Xiaomi Flash

Fastboot flasher for Xiaomi devices. Version **2.0.0** is the first full UI redesign focused on a guided ROM flash workflow.

**Repository:** [github.com/Bee1002/Xiaomi_Flash](https://github.com/Bee1002/Xiaomi_Flash)

## Features (v2 UI)

- Automatic fastboot device detection and hardware info panel
- Load Xiaomi ROM packages (`flash_all.bat`, `flash_all_lock.bat`, `flash_all_except_storage.bat`, `payload.bin`, or loose `images\`)
- Step-by-step flash progress with partition table and terminal log
- Payload.bin flashing in fastbootd
- Options: bypass anti-rollback, auto reboot
- Advanced: Reset EFS, Fix / Brick, Slot A/B switch
- Reboot to system, fastboot, or recovery

### Engine features (available in code, not exposed in v2 UI yet)

The application still ships the full fastboot/payload engine from earlier versions. These capabilities are wired in hidden legacy controls (`MainWindow.xaml`) for future integration:

- Multi-device selector
- Manual per-partition flash and erase
- Logical partition management (create, delete, resize)
- Standalone Payload.bin dumper (extract partition images)

See the `LEGACY UI HOST` comment block in `MainWindow.xaml` for integration notes.

## Requirements

- Windows 10/11 (x86 build)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- Xiaomi USB drivers (device in fastboot mode)
- Unlocked bootloader (for most flash operations)
- Valid ROM package matching your device codename

## Typical workflow

```
Connect phone (fastboot) → LOAD FIRMWARE → choose Mode → START → wait → reboot
```

### Step by step

1. **Boot the phone into fastboot**
   - Power off, then hold **Volume Down + Power**, or use `adb reboot bootloader`.
   - Install Xiaomi USB drivers if the tool shows **NO DEVICE DETECTED**.

2. **Connect and verify**
   - The left panel should show **FASTBOOT MODE DETECTED** or **FASTBOOTD MODE DETECTED**.
   - Check serial, codename, and firmware version before flashing.

3. **Load firmware**
   - Click **[ LOAD FIRMWARE ]** and select the ROM root folder or a file inside it.
   - Supported layouts:
     - `flash_all.bat` / `flash_all_lock.bat` / `flash_all_except_storage.bat`
     - `payload.bin` (OTA-style ROM)
     - `images\*.img` (manual image set)

4. **Choose flash mode**
   - Use the **Mode** dropdown:
     - `flash_all.bat` — clean install, **wipes user data**
     - `flash_all_lock.bat` — clean install + **locks bootloader**
     - `flash_all_except_storage.bat` — update, keeps internal storage
     - `payload.bin` — flashes via fastbootd (tool reboots as needed)
     - `Images only` — flashes every `.img` in `images\`

5. **Set options**
   - **Bypass anti_RB** — flashes the `anti` partition from the ROM before other steps (use only when you understand rollback implications).
   - **Auto reboot** — reboots when the script does not include `reboot`.

6. **Start flashing**
   - Click **[ START ]**, confirm the summary dialog, and monitor the partition table and terminal log.
   - Use **[ STOP ]** to request cancellation between steps (may not interrupt an active fastboot transfer immediately).

7. **Finish**
   - On success, the terminal shows a completion banner. Let the device reboot if auto-reboot is enabled.

## Fastboot vs fastbootd

| Mode | When you need it |
|------|------------------|
| **Fastboot** (bootloader) | Classic `.img` flashes, `erase`, slot changes, `oem` commands, reboot to fastbootd |
| **Fastbootd** (userspace) | **Payload.bin** flashing, logical/dynamic partition operations |

- The status panel shows which mode is active.
- For `payload.bin` ROMs, the tool handles rebooting into fastbootd automatically.
- If a flash fails with partition or logical-partition errors, reboot to **fastbootd** via **[ REBOOT ] → Fastboot** and retry if appropriate.

## Warnings

> **Flashing can permanently brick your device or erase all data. Proceed only if you know your device codename and ROM source.**

- **`flash_all.bat` wipes userdata** — back up photos, apps, and accounts first.
- **`flash_all_lock.bat` locks the bootloader** — may restrict future flashing and custom ROMs.
- **Anti-rollback** — flashing an older ROM than the device allows can fail or brick. The anti-rollback index is shown in the device panel. Use **Bypass anti_RB** only with a matching `anti` image and when you accept the risk.
- **Wrong ROM** — never flash a package for a different codename/model.
- **USB stability** — use a good cable and USB 2.0/3.0 port; do not unplug during transfers.
- **Battery** — keep the device charged; avoid flashing on a dying battery.
- **Reset EFS** (Advanced) — can break radio, IMEI, or network until restored from backup.
- **Fix / Brick** (Advanced) — only clears `misc` and reboots; it is not a substitute for a full ROM flash.

## Screenshots

Place UI captures in `docs/screenshots/` for documentation or release notes:

| File | Suggested content |
|------|-------------------|
| `01-device-connected.png` | Device panel with fastboot detected |
| `02-firmware-loaded.png` | Mode dropdown and partition table populated |
| `03-flash-progress.png` | Flash in progress with terminal log |
| `04-completed.png` | Success banner after flash |

Example (after adding images):

```markdown
![Device connected](docs/screenshots/01-device-connected.png)
```

## Build

```bash
dotnet build -c Release -p:Platform=x86
```

Output: `bin\Release\net8.0-windows\Xiaomi_Flash.exe`

No external NuGet restore is required; dependencies are bundled in the repository.

### Visual Studio (XAML designer)

If the designer shows *"Could not load file or assembly Xiaomi_Flash"*:

1. Close Visual Studio.
2. Delete the project `obj\` folder (or run `dotnet clean`).
3. Open the solution and build **Debug | x86** once before opening `.xaml` files.

## Distribution (publish)

### Framework-dependent (smaller, requires .NET 8 Runtime on the PC)

```bash
dotnet publish -c Release -p:Platform=x86 -o publish\framework-dependent
```

Copy the entire `publish\framework-dependent\` folder. The user must have .NET 8 Desktop Runtime installed.

### Self-contained (larger, no separate runtime install)

```bash
dotnet publish -c Release -p:Platform=x86 -r win-x86 --self-contained true -o publish\self-contained
```

The published folder must include (copied automatically by the project):

- `Xiaomi_Flash.exe`
- `fastboot.exe`, `AdbWinApi.dll`, `AdbWinUsbApi.dll`
- `liblzma.dll`, `liblzma64.dll`
- `Google.Protobuf.dll`
- `Data\xiaomi_codenames.json`

Zip the output folder for distribution.

## Dependencies (no NuGet)

| Component | Implementation |
|-----------|----------------|
| ZIP (ROM `.zip`) | `System.IO.Compression` (.NET 8) |
| BZip2 (`ReplaceBz`) | `ThirdParty/BZip2/` (MIT, DotNetZip-derived) |
| XZ/LZMA (`ReplaceXz`) | P/Invoke to `liblzma.dll` (`ThirdParty/Lzma/`) |
| Protobuf (payload manifest) | `lib/Google.Protobuf.dll` v3.28.3 (local reference) |
| Fastboot | Bundled `fastboot.exe` + ADB USB DLLs |

## Third-party components

- Android Platform Tools (`fastboot.exe`)
- [DotNetZip](https://github.com/DinoChiesa/DotNetZip) — BZip2 decompressor module (MIT)
- [Google Protobuf](https://github.com/protocolbuffers/protobuf) (BSD-3-Clause)

## License

MIT — see [LICENSE](LICENSE)
