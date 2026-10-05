<p align="center"><img src="docs/images/liquid-glass-hero.png" width="100%" alt="Magic Mouse for Windows — Free · Open source"></p>
<h1 align="center">Magic Mouse for Windows</h1>
<p align="center">Tu Magic Mouse en Windows, con una experiencia cuidada.</p>

Desarrolle esta app gratuita para aprovechar mejor nuestro Magic Mouse en Windows. Si te sirve, puedes apoyar el proyecto con una estrella, compartirlo o proponer una mejora.

## Así se ve

Capturas reales de la interfaz. Pulsa una imagen para verla completa.

<table>
<tr>
<td width="50%" align="center"><a href="docs/images/appearance-light.png"><img src="docs/images/appearance-light.png" alt="Apariencia clara" width="100%"></a><br><sub>Modo claro</sub></td>
<td width="50%" align="center"><a href="docs/images/appearance-dark.png"><img src="docs/images/appearance-dark.png" alt="Apariencia oscura" width="100%"></a><br><sub>Modo oscuro</sub></td>
</tr>
<tr>
<td width="50%" align="center"><a href="docs/images/gestures-dark.png"><img src="docs/images/gestures-dark.png" alt="Configuración de gestos" width="100%"></a><br><sub>Gestos</sub></td>
<td width="50%" align="center"><a href="docs/images/pointer-light.png"><img src="docs/images/pointer-light.png" alt="Puntero y desplazamiento" width="100%"></a><br><sub>Puntero y desplazamiento</sub></td>
</tr>
</table>

## Instalar

Requiere **Windows 10 (19041+) o Windows 11, x64**.

1. [Descarga el ZIP para Windows](https://github.com/SebastianArdila-Dev/Magic-Mouse-For-Windows/releases/tag/v0.1.2).
2. Para instalar directamente, descarga y abre **Magic Mouse Windows x64 0.1.2 Setup.exe**. Si prefieres la carpeta portable, descarga **Magic Mouse Windows x64 0.1.2.zip**, extrae todos sus archivos y abre `MagicMouse.Windows.App.exe`.
3. Arrastra el logo a **Aplicaciones** o pulsa **Instalar y continuar**. También puedes elegir **Usar sin instalar**.
4. Completa las preferencias y pulsa **Finalizar**. Empareja el mouse en Bluetooth cuando lo tengas.

No necesitas instalar .NET ni ejecutar como administrador. Conserva todos los archivos del ZIP: los DLL y las carpetas de idiomas son dependencias de la aplicación.

<p align="center"><img src="docs/images/welcome-dark.png" width="440" alt="Bienvenida y configuración inicial"></p>

## Funciones

- Temas claro, oscuro y del sistema.
- Ajustes de puntero, velocidad, botones y desplazamiento.
- Configuración de gestos y simulador local.
- Perfiles por aplicación, guardados en tu usuario.
- Batería cuando Windows proporciona el dato.
- Funcionamiento en la bandeja al cerrar la ventana y reintentos de lectura del dispositivo.

**Versión preliminar:** los gestos físicos requieren acceso HID y todavía necesitan pruebas con un Magic Mouse real. No se incluye un controlador táctil firmado. Sigue pendiente una discrepancia del tamaño del cursor en algunos controles. Consulta [las limitaciones](KNOWN_LIMITATIONS.md).

## Datos y desinstalación

La app no pide una cuenta ni envía telemetría. Guarda ajustes y diagnósticos localmente en `%LOCALAPPDATA%\MagicMouseForWindows`. Los diagnósticos pueden contener rutas e identificadores del dispositivo; revísalos antes de compartirlos.

Para desinstalar, desactiva **Iniciar con Windows**, sal desde la bandeja y elimina `%LOCALAPPDATA%\Programs\MagicMouseForWindows` y su acceso del menú Inicio. Borra la carpeta de ajustes solo si también quieres perder tus preferencias.

## Desarrollo

C#, .NET 8 y WinUI 3. [Cómo compilar, probar y empaquetar](BUILDING.md).

`src/` contiene la app; `tests/` conserva las pruebas del núcleo usadas en la compilación automática. No se incluyen en el ZIP instalable. Los informes de pruebas, capturas de diagnóstico, configuraciones personales y herramientas internas de QA quedan fuera de la distribución.

Para reportar un problema, abre un issue con tu versión de Windows, modelo del mouse y pasos para reproducirlo. No adjuntes contraseñas ni datos privados.

[GitHub · SebastianArdila-Dev](https://github.com/SebastianArdila-Dev) · [X · SebastianardSWE](https://x.com/SebastianardSWE)

Código bajo [MIT](LICENSE). [Licencias de dependencias](THIRD_PARTY_LICENSES.md). Proyecto independiente, sin afiliación con Apple.
