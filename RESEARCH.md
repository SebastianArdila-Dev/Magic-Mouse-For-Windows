# Investigación técnica inicial

Fecha: 2026-10-04. Investigación web inicial; debe ampliarse antes de afirmar compatibilidad por modelo o publicar software.

## Referencias visuales

**Imagen A, contrato de diseño.** Fondo claro con translucidez tipo Liquid Glass, barra lateral, azul brillante para selección y controles, tarjetas redondeadas, sombras ligeras, jerarquía tipográfica amplia, render blanco grande del mouse y preview en vivo. La navegación fija seis destinos: Gestos; Puntero y desplazamiento; Botones; Aplicaciones; Aspecto; Avanzado. Las vistas pequeñas de abajo son previews de esas áreas, no una instrucción para copiar su estructura literalmente.

**Imagen B, referencia de funciones solamente.** Identidad/modelo/conexión/batería; scrolling con ejes, inversión, bloqueo, velocidad y sensibilidad; gestos rápidos de uno y dos dedos, dirección, sensibilidad y cambio de escritorio; scroll con uno o dos dedos; click central; taps de uno, dos y tres dedos; opciones de botones, velocidad base y comportamiento al levantar; filtros. No se adoptan su tema oscuro, layout ni recursos gráficos.

## Hallazgos técnicos

- Microsoft documenta **Raw Input** para recibir datos de dispositivos HID y distinguir dispositivo de origen. La app registra top-level collections y procesa `WM_INPUT`; esto no implica acceso garantizado a cualquier colección HID exclusiva del sistema.
- La **HID API** de Windows ofrece parsing de descriptores y reportes mediante APIs `HidP_*`. La API WinRT `HidDevice` expone lectura de reportes, con acceso dependiente de que el dispositivo y permisos permitan abrir la colección.
- `GetRawInputDeviceInfo` permite obtener nombre de interfaz/preparsed data; en condiciones de acceso compartido el nombre puede abrirse para leer reportes. La disponibilidad real debe probarse con cada generación y stack Bluetooth.
- **WinUI 3/Windows App SDK** es la opción preferida para una app nueva de escritorio Windows y permite conservar una UI personalizada. No es posible compilarla ni validarla en este entorno macOS: `dotnet` no está instalado y no hay SDK/Windows.
- Las apps WinUI de escritorio no reciben la gestión de ciclo de vida de suspensión de UWP; se debe manejar reinicio y reconexión explícitamente.
- El repositorio Linux `hid-magicmouse` reconoce el protocolo de informes de Apple y ofrece evidencia útil para investigación de formatos. Está bajo `GPL-2.0-or-later`; no copiar código ni derivados en una implementación con licencia distinta. Usar documentación pública y un parser limpio independiente o adoptar explícitamente una licencia compatible tras revisión.
- Repositorios Windows recientes encontrados en búsqueda no se consideran evidencia suficiente de calidad, actividad o seguridad. El proyecto `magicmouse-ptp` declara licencia CC-BY-NC-4.0, requiere driver/test-signing y no es utilizable como base de un producto comercial. No usar su código.
- La base de IDs de Linux lista Magic Mouse con product IDs `0x030D`, `0x0269` y `0x0323`, con vendor Apple `0x05AC` (USB) y `0x004C` en Bluetooth. El descubrimiento inicial reconoce esos IDs como candidatos; el mapeo debe validarse en Windows físico porque Bluetooth puede exponer otro formato de instancia.

## Fuentes

- Microsoft, [Raw Input Overview](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input).
- Microsoft, [Raw Input APIs](https://learn.microsoft.com/en-us/windows/win32/inputdev/raw-input).
- Microsoft, [HID API](https://learn.microsoft.com/en-us/windows-hardware/drivers/hid/hid-api).
- Microsoft, [GetRawInputDeviceInfo](https://learn.microsoft.com/en-us/windows/desktop/api/winuser/nf-winuser-getrawinputdeviceinfoa).
- Microsoft, [HidDevice](https://learn.microsoft.com/en-us/uwp/api/windows.devices.humaninterfacedevice.hiddevice).
- Microsoft, [WinUI](https://learn.microsoft.com/en-us/windows/apps/winui).
- Microsoft, [Windows App SDK app lifecycle](https://learn.microsoft.com/en-us/windows/apps/develop/launch/app-lifecycle).
- Linux upstream, [`drivers/hid/hid-magicmouse.c`](https://github.com/torvalds/linux/blob/master/drivers/hid/hid-magicmouse.c), GPL-2.0-or-later.
- Linux upstream, [`hid-ids.h`](https://github.com/torvalds/linux/blob/master/drivers/hid/hid-ids.h), IDs de producto Apple consultados para filtrar candidatos de Magic Mouse.
- GitHub, [magicmouse-ptp](https://github.com/Agash/magicmouse-ptp), CC-BY-NC-4.0 declarado por el repositorio; proyecto joven, sin historial suficiente para considerarlo base confiable.
- GitHub, [magic-mouse-windows](https://github.com/chrischip/magic-mouse-windows), explora instalación de controlador Apple Boot Camp; implica cambios de kernel/firmado y no es arquitectura inicial apropiada para nuestra app.

## Decisiones y preguntas abiertas

1. Probar primero lectura user mode en un Windows real con Magic Mouse conectado. Windows podría consumir la colección o exponer solo mouse HID estándar.
2. Separar Raw Input (atribución y eventos estándar) de lector HID directo (reportes vendor/táctiles cuando sea accesible).
3. No inferir número de serie, firmware, estado de carga ni generación exacta desde el nombre. Exponer como no disponible hasta obtener campos verificables.
4. No afirmar que USB está soportado en todas las generaciones. Validar IDs e interfaz reales.
5. Mantener una capa de acciones que usa `SendInput`/atajos estándar solo tras revisar límites de UIPI y foco; acciones de ventana y escritorios tienen restricciones entre aplicaciones.
6. La inyección de scroll de alta resolución y el control de taps pueden competir con el controlador HID estándar. Evaluar primero remapeo por user mode y documentar cuando requiera filtro/driver.

## Fuera de alcance de evidencia actual

No se ha verificado descriptor HID de hardware físico, batería en Windows, formatos de reportes por generación, acceso a colecciones exclusivas, ni compatibilidad de un instalador MSIX con un proceso auxiliar persistente. Estas son tareas de investigación y prueba de la siguiente fase.
