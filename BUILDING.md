# Compilar y empaquetar

En Windows x64, instala .NET SDK 8 y ejecuta desde la raíz del repositorio:

```powershell
dotnet restore MagicMouseForWindows.sln
dotnet build MagicMouseForWindows.sln -c Release -m:1 -nodeReuse:false --disable-build-servers
dotnet test tests/MagicMouse.Core.Tests/MagicMouse.Core.Tests.csproj -c Release
./scripts/package.ps1
```

El ZIP portable y el instalador ejecutable aparecen en `artifacts/`. El instalador incluye el ZIP como recurso y abre el asistente de la app; no requiere descargar .NET. Incluye el runtime, la aplicación, las licencias y las instrucciones de instalación. No incluye símbolos de depuración, código de pruebas, ajustes personales ni logs.

Las 60 pruebas del núcleo cubren gestos, scroll, perfiles, persistencia, batería, archivos de cursor y política de reconexión. GitHub Actions las ejecuta y genera el paquete. No sustituyen la validación Bluetooth/HID con hardware real.
