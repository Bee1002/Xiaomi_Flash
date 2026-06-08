# Engine — flash core

Motor fastboot/payload sin dependencias de WPF (salvo mensajes puntuales en capas superiores).

| File | Role |
|------|------|
| `Fastboot.cs` | Lanza `fastboot.exe`, lee stdout/stderr |
| `FastbootGate.cs` | Mutex lógico: un solo fastboot a la vez |
| `FastbootData.cs` | Parseo de `getvar all` |
| `FastbootAllVars.cs` / `FastbootVarReader.cs` | Lectura de variables |
| `Payload.cs` | Extracción y flash de `payload.bin` |
| `Helper.cs` | Utilidades compartidas (archivos, UI helpers) |
