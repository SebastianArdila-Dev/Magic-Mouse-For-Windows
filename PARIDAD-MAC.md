# Comparación con un Magic Mouse en macOS

Referencia oficial consultada: https://support.apple.com/en-us/102482 (sección Magic Mouse gestures), y guía del dispositivo: https://cdsassets.apple.com/live/6GJYWVAV/user/ma1982_magic-mouse-2-ug.pdf.

| Función del Magic Mouse en Mac | Implementación en esta app para Windows | Límite |
|---|---|---|
| Clic secundario en un lado | Windows conserva clic físico; botón explícito para intercambiar principal/secundario | Cambio global a todos los mouse |
| Scroll con un dedo | Decoder HID + motor de desplazamiento + SendInput; dirección natural, sensibilidad, velocidad e inercia | Acceso físico HID y evitar conflicto con otros drivers aún sin verificar |
| Swipe de un dedo entre páginas | Acciones Atrás/Adelante | Depende de la aplicación activa |
| Swipe de dos dedos entre escritorios/apps a pantalla completa | Windows + Control + flechas para escritorios virtuales | Windows no reproduce Spaces de macOS exactamente |
| Doble tap de un dedo: zoom inteligente | Reconocedor de doble tap; Ctrl + más y Ctrl + 0 para alternar zoom | Aproximación para navegadores/PDF, sin ajuste semántico al objeto ni restauración del zoom previo |
| Doble tap de dos dedos: Mission Control | Windows + Tab para Vista de tareas | Equivalente de Windows, no Mission Control nativo |

Configura el preset con Gestos > Usar gestos de macOS. Este botón no finge un dispositivo conectado ni activa entrada por sí solo. Activa acciones del dispositivo y conecta la superficie táctil cuando tengas el mouse. Si la opción se mantiene activada, la app intenta reconectar ante la detección del dispositivo.

Hay además acciones opcionales, taps de tres dedos, controles de multimedia y zoom mediante Control + scroll. Los gestos de trackpad (pinch, rotación, cuatro dedos) no son el inventario oficial del Magic Mouse y no se presentan como funciones nativas del mouse.

## Verificación real y pendiente

43 pruebas unitarias aprobadas. WinUI: navegación, persistencia, modal, simulaciones, doble taps y cambios de tema comprobados. Windows cargó los cinco estilos de cursor y aceptó SetSystemCursor para macOS al 180%, seguido de restauración. El cliente nativo ocupa toda la ventana, eliminando el margen superior residual.

La versión todavía necesita pruebas con un Magic Mouse físico. El decoder 0x29/0x12, solicitud de modo táctil y dispatcher están conectados en el código, pero no se valida compatibilidad física hasta probar la colección HID real. Windows puede reservarla para el controlador. No se instaló driver de filtro. Las zonas físicas personalizadas, clic simultáneo y detección de levantamiento siguen desactivados. El indicador de clic es una previsualización local.

Fuentes técnicas: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setsystemcursor ; https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput ; https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/sending-hid-reports .
La estructura del protocolo se investigó como hechos de formato en la implementación upstream Linux: https://github.com/torvalds/linux/blob/master/drivers/hid/hid-magicmouse.c . No se incorporó el controlador Linux ni su código de implementación.

Actualización: las colecciones mouse estándar de Windows se abren exclusivamente por el sistema según Microsoft: https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/keyboard-and-mouse-hid-client-drivers . Esta revisión enumera todas las interfaces Apple conocidas y prueba también sus colecciones específicas; si ninguna permite lectura, el acceso táctil sigue pendiente de un controlador compatible. Eso impide confirmar todos los gestos físicos como terminados.
