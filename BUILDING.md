# Compilar en Windows

Requisitos: Windows x64 10 (19041+) o 11 y .NET SDK 8. Se usan paquetes oficiales NuGet para WinUI y las herramientas de recursos PRI.

```powershell
dotnet restore MagicMouseForWindows.sln
dotnet build MagicMouseForWindows.sln -m:1 -nodeReuse:false --disable-build-servers
dotnet test tests/MagicMouse.Core.Tests/MagicMouse.Core.Tests.csproj
```

Para producir el ZIP de desarrollo con el simulador incluido:

```powershell
./scripts/package.ps1
```

El script publica una carpeta self-contained y añade el acceso al instalador visual. Todos los archivos deben distribuirse juntos. No se incluye certificado de firma de código ni controlador kernel. Antes de etiquetar una release, consulta el informe de pruebas y las limitaciones conocidas.
