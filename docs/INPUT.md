# Entrada del Magic Mouse

La versión 0.1.2 usa lectura HID en modo usuario, reconoce reportes táctiles de Magic Mouse y Magic Mouse 2 (incluido USB-C), y envía acciones mediante SendInput. El movimiento y los clics físicos siguen a cargo del controlador de Windows.

Las colecciones mouse de Windows son exclusivas. La app prueba las colecciones disponibles y solicita el modo táctil únicamente a los identificadores Apple reconocidos; no basta con que Windows detecte un mouse para disponer de sus contactos. No se distribuyen controladores comerciales ni se modifica la pila de controladores del sistema.

La entrada compartida no permite sustituir de forma fiable un clic físico por otro, detectar levantamiento o reproducir todas las funciones del controlador de Magic Mouse Utilities. Para esa compatibilidad hace falta desarrollar y firmar un controlador propio y validar dispositivos reales. Las pruebas sintéticas no certifican Bluetooth ni compatibilidad física.

## Comportamiento verificado por software

- Los gestos desactivados o sin acción no reservan ejes de scroll.
- Los gestos asignados reservan su eje para evitar scroll y navegación simultáneos.
- Selección de uno o dos dedos, tap-to-click y perfiles usan los ajustes efectivos.
- Los movimientos pequeños conservan las fracciones de rueda entre reportes.
- La inercia usa el tiempo real del temporizador y se cancela al iniciar un contacto nuevo.
- Los reportes cortos de movimiento no producen taps ficticios. Un contacto sin nuevos datos se cancela, sin ejecutar acciones pendientes.
- Las aperturas HID se serializan y las colecciones que rechazan el modo táctil se descartan.

## Referencias

- [Funciones de Magic Mouse Utilities](https://www.magicutilities.net/magic-mouse/features).
- [FAQ del fabricante: controlador y límites del zoom](https://cms-bcdn.magicutilities.net/magic-mouse/faqs).
- [Arquitectura HID de Windows](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/hid-architecture).
- [Firma de controladores](https://learn.microsoft.com/windows-hardware/drivers/install/windows-driver-signing-tutorial).
- [Referencia pública de formatos de reportes](https://github.com/torvalds/linux/blob/master/drivers/hid/hid-magicmouse.c). Se contrastan datos del protocolo; no se incorpora el controlador Linux.
