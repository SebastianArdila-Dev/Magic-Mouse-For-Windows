# Magic Mouse Bridge — controlador experimental

Filtro KMDF x64 propio para el transporte Bluetooth/HID del Magic Mouse. Conserva la versión de la app 0.1.2 y no modifica su diseño.

## Estado

Este código es un prototipo para revisión y pruebas. Compilar un archivo SYS no demuestra que su posición en la pila Bluetooth sea correcta ni que funcione con un mouse real. No se instala en el equipo del usuario ni se añade al instalador público. Falta validar hardware, suspensión, retirada, reconexión, consumo de CPU y memoria y firma de producción.

## Implementación

- INF de extensión limitado a los identificadores Bluetooth Apple de los modelos 030D, 0269 y 0323; no hay filtro global de clase Mouse.
- Comprueba también VID/PID en ejecución. Un dispositivo desconocido no activa comandos táctiles.
- Añade una colección opaca al descriptor para ampliar el buffer de lectura del transporte. Copia los reportes táctiles en un anillo acotado de 64 entradas.
- Antes de ampliar comprueba el recorrido de los items HID, rechaza colisiones con 7F, reportes sin numerar y estructuras incompletas. La colección añadida conserva los globales con Push/Pop. No activa la superficie si el descriptor ampliado no se ha entregado correctamente a Windows.
- Reconstruye movimiento y botones con el descriptor nativo original mediante HidP. Solo permite activar el modo táctil si reconoce un formato de mouse relativo firmado de 16 bits y rango -32768..32767 en X/Y y botones principal/secundario.
- Los descriptores con ejes de 8 bits se rechazan por ahora; la declaración del identificador 030D no implica compatibilidad táctil completa con Magic Mouse 1.
- PDO raw independiente y exclusivo: SYSTEM y administradores tienen acceso completo; usuarios interactivos tienen acceso de lectura para los tres IOCTLs acotados. Se deniega el acceso de red y se rechazan rutas secundarias. No se requiere elevar la app ni ejecutar un servicio privilegiado. Esta política todavía requiere validación física y de seguridad con distintas cuentas.
- IOCTLs limitados a identidad, lectura de reportes y un comando de activación fijo. No acepta direcciones ni comandos HID arbitrarios del usuario.
- Protocolo versionado, tamaño fijo, identidad exacta del dispositivo padre y detección de pérdidas de reportes.

La app incluye un cliente opcional. La clave local `driver.experimental=true` permite probarlo en un entorno de desarrollo; está desactivada por defecto. No se añaden controles visuales. Si el puente no está accesible, se conserva la lectura HID existente.

## Compilar

En Visual Studio Build Tools 2022 con C++:

```powershell
nuget restore drivers/packages.config -PackagesDirectory drivers/packages -NonInteractive
msbuild drivers/MagicMouseBridge/MagicMouseBridge.vcxproj /p:Configuration=Release /p:Platform=x64 /p:SignMode=Off
```

Los paquetes SDK y WDK de Microsoft están fijados a 10.0.26100.1. GitHub Actions compila el SYS y verifica el INF. El artefacto incluye SYS, INF y catálogo CAT **experimental y sin firma**, no un instalador para el público.

## Validación pendiente antes de instalar para uso real

1. Máquina de pruebas aislada con depurador kernel, recuperación disponible y otro mouse. Verificar los hardware IDs reales antes de intentar enlazar el INF.
2. Confirmar con WinDbg la posición del filtro y la llegada de IOCTL_HID_READ_REPORT. Un INF válido no acredita esa posición.
3. Inspeccionar el descriptor original y el ampliado; verificar que los IDs no colisionan, que las longitudes coinciden y que HidClass/MouHid conservan movimiento y clics. La colección 7F no está validada con firmware real.
4. Probar todos los modelos, clic izquierdo/derecho, movimiento negativo y rápido, scroll y gestos, caída de enlace, apagado, suspensión y múltiples dispositivos.
5. Driver Verifier: Special Pool, I/O verification, Force IRQL Checking, deadlock detection y KMDF Verifier. Realizar pruebas de estrés y retirada mientras hay solicitudes pendientes.
6. Resolver los límites de movimiento de Magic Mouse 1 y verificar la política de acceso con usuarios estándar, cuentas de red y sesiones distintas.
7. Preparar catálogo, pruebas HLK y firma mediante Microsoft antes de integrar la instalación. No se desactiva Secure Boot ni la comprobación de firmas desde la app.

La matriz de aceptación está en [VALIDATION.md](VALIDATION.md). El flujo también ejecuta pruebas C de los tamaños y modelos admitidos; abrir una sesión limpia la cola y marca la discontinuidad inicial.

## Referencias

- [Arquitectura HID de Microsoft](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/hid-architecture).
- [Filtros de dispositivo](https://learn.microsoft.com/en-us/windows-hardware/drivers/install/installing-a-filter-driver).
- [WDK oficial con NuGet](https://learn.microsoft.com/en-us/windows-hardware/drivers/install-the-wdk-using-nuget).
- [Firma de controladores](https://learn.microsoft.com/windows-hardware/drivers/install/windows-driver-signing-tutorial).
- [Formatos públicos del protocolo](https://github.com/torvalds/linux/blob/master/drivers/hid/hid-magicmouse.c). No se incorpora su código GPL ni drivers comerciales.

## Preparar una distribución firmada

Tras recibir el paquete firmado, ejecutar Prepare-Distribution.ps1 indicando el directorio, SignTool oficial y un directorio de salida nuevo. Comprueba imagen PE x64, versión, política de firma kernel y firmas/pertenencia del SYS y del INF al catálogo antes de generar un ZIP con solo INF, SYS y CAT. El flujo comprueba con SignTool real que el artefacto sin firma no genera distribución. La verificación de firma no sustituye la matriz física y no publica ni instala archivos automáticamente.
