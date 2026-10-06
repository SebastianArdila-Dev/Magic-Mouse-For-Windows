# Validación física del controlador

Estado: **pendiente**. Las pruebas de código y la compilación no sustituyen estas comprobaciones. Utilizar un Windows de laboratorio con recuperación y depuración kernel disponibles. El instalador público no instala este prototipo.

Registrar modelo, firmware, versión de Windows, adaptador Bluetooth, descriptor HID original y ubicación real del filtro. No publicar direcciones Bluetooth, números de serie ni identificadores personales en incidencias públicas.

| Prueba | Criterio de aceptación |
| --- | --- |
| Filtro presente, puente desactivado | Movimiento y clics conservan su comportamiento; no se activa el modo táctil. |
| Activación táctil | Solo se activa para el dispositivo seleccionado, con descriptor compatible; los datos recibidos corresponden a ese mouse. |
| Movimiento y clics | Movimiento positivo/negativo y rápido sin saltos ni truncamiento; clic, doble clic y arrastre mantienen los botones hasta su liberación. |
| Cola saturada | Se informa la discontinuidad, el motor cancela el gesto anterior y no dispara acciones con datos antiguos. |
| Cerrar y reabrir el cliente | El cliente nuevo recibe una sesión limpia, sin reproducir reportes anteriores; el movimiento nativo sigue funcionando. |
| Suspensión y reanudación | Se invalida la sesión anterior; la app vuelve a verificar el dispositivo y reactiva la captura sin acciones fantasma. |
| Apagar y reconectar | No quedan solicitudes o botones bloqueados; la app conserva su configuración y recupera la captura. |
| Dos dispositivos | El puente no se vincula a otro mouse del mismo modelo por coincidencia parcial de identidad. |
| Retirada durante lectura o activación | Las solicitudes finalizan con error controlado; no hay bloqueo, uso de memoria liberada ni caída de Windows. |
| Acceso sin autorización | Un usuario sin privilegios no abre el PDO experimental ni envía IOCTLs. |
| Driver Verifier | Sin errores de pool, IRQL, I/O, bloqueos ni fugas durante estrés, retirada y suspensión. |

Ejecutar las pruebas por cada modelo y adaptador que se declare compatible. Revisar el reporte 7F añadido, sus longitudes y posibles colisiones con el descriptor original antes de habilitar la captura. Actualmente los ejes nativos deben ser relativos, de 16 bits y tener rango -32768..32767; otros formatos quedan sin activar.

Antes de distribuir: resolver cualquier fallo, habilitar un servicio intermediario seguro para la app sin elevación, preparar catálogo y firma de producción y repetir la matriz con el paquete final. No presentar un binario sin firma como una descarga plug and play.
