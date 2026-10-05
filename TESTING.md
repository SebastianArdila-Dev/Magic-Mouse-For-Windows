# Pruebas

43 pruebas unitarias cubren reconocimiento de gestos, doble tap, scroll, perfiles, ajustes, batería, archivos de cursor y reconexión con backoff.

```powershell
dotnet test tests/MagicMouse.Core.Tests/MagicMouse.Core.Tests.csproj
```

Diagnóstico integrado sobre la aplicación compilada:

```powershell
./MagicMouse.Windows.App.exe --stability-test --smoke-test --visual-qa --visual-creator --visual-introduction
```

`stability-test.txt` registra clics reales mediante los AutomationPeer de WinUI: 100 invocaciones del logo, 200 aperturas/cierres de tarjetas, doble clic, callbacks de layout obsoletos, 100 diálogos, seis páginas, diez cambios de tema, setup compacto y fade.

`smoke-test.txt` comprueba navegación, ajustes, simulador, APIs reales de mouse, temas, bandeja y registro de inicio. La prueba restaura velocidad e intercambio de botones. La comprobación del cursor visible requiere que la ventana esté al frente y la posición no esté cubierta; sigue existiendo una incidencia de tamaño visible que debe revisarse antes de declarar resuelto todo el soporte de cursor.

`visual-qa` y `visual-qa-dark` contienen capturas reales de WinUI. No se incluyen archivos personales de ajustes ni logs en el repositorio.

Las pruebas locales no sustituyen pruebas físicas. Falta validar cada generación de Magic Mouse, conectar/desconectar Bluetooth, suspensión/reanudación, reportes HID y batería. No presentar contactos ni lecturas del simulador como datos reales.
