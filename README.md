# Xiaomi Flash

Herramienta Fastboot y Payload Dumper para dispositivos Xiaomi, basada en [FastbootEnhance](https://github.com/libxzr/FastbootEnhance).

## Funcionalidades

- Ver variables fastboot del dispositivo
- Cambiar entre fastbootd, bootloader, recovery y sistema
- Cambiar entre slot A y B
- **Flashear Payload.bin en fastbootd**
- Flashear imágenes individuales
- Borrar particiones
- Gestionar particiones lógicas (crear, eliminar, redimensionar)
- Extraer imágenes de Payload.bin
- Ver metadatos de particiones dinámicas

## Requisitos

- Windows 10/11
- .NET 8 Runtime
- Drivers USB del dispositivo (modo fastboot)

## Compilación

```bash
dotnet build -c Release -p:Platform=x86
```

El ejecutable se genera en `bin\Release\net8.0-windows\`.

No requiere `dotnet restore` de paquetes NuGet externos: las dependencias están integradas en el repositorio.

### Visual Studio (diseñador XAML)

Si el diseñador muestra *"Could not load file or assembly Xiaomi_Flash"*:

1. Cierra Visual Studio.
2. Borra la carpeta `obj\` del proyecto (o ejecuta `dotnet clean`).
3. Abre la solución y compila **Debug | x86** una vez antes de abrir los `.xaml`.

## Dependencias (control total, sin NuGet)

| Componente | Implementación |
|------------|----------------|
| ZIP (ROM `.zip`) | `System.IO.Compression` (.NET 8) |
| BZip2 (`ReplaceBz`) | Código propio en `ThirdParty/BZip2/` (MIT, basado en DotNetZip) |
| XZ/LZMA (`ReplaceXz`) | P/Invoke directo a `liblzma.dll` (`ThirdParty/Lzma/`) |
| Protobuf (manifiesto payload) | `lib/Google.Protobuf.dll` v3.28.3 (referencia local) |
| Fastboot | `fastboot.exe` + DLLs ADB incluidos |

## Créditos

- [FastbootEnhance](https://github.com/libxzr/FastbootEnhance) por LibXZR (MIT)
- Android Platform Tools
- [DotNetZip](https://github.com/DinoChiesa/DotNetZip) — solo módulo BZip2 descompresor (MIT)
- [Google Protobuf](https://github.com/protocolbuffers/protobuf) (BSD-3-Clause)

## Licencia

MIT — ver [LICENSE](LICENSE)
