# Arquitectura propuesta

## Capas

```text
WinUI 3 (MVVM, design tokens, vistas)
  └─ Application services (settings, profiles, capability-aware view state)
       ├─ Device discovery + connection lifecycle
       ├─ HID transport (Raw Input / direct HID; Windows-specific)
       ├─ Protocol parser (pure .NET, fixtures)
       ├─ Touch normalization + gesture recognizers (pure .NET)
       ├─ Action mapping + Windows action adapters
       ├─ Profile resolver (foreground process, explicit opt-in)
       └─ Persistence + structured diagnostics
```

## Principios

- `IAppleInputDevice` describe datos observados, no conjeturas. Capacidades y cada campo opcional deben incluir fuente/estado de disponibilidad.
- Transportes emiten informes sin ejecutar acciones. Parser convierte bytes a modelos tipados. Reconocedor emite `GestureEvent`. Mapeador selecciona acción y dispatcher ejecuta.
- La UI depende de interfaces y modelos, nunca de handles HID. Los controles se ocultan/deshabilitan según capabilities y explican el motivo.
- Core (parser, normalización, gestos, perfiles, serialización) permanece portable para pruebas. WinUI y P/Invoke viven en proyectos Windows.
- Reintentos de conexión responden a notificaciones de llegada/remoción; sleep/resume fuerza nueva enumeración. Sin polling frecuente.
- Configuración local, versionada y validada; valores predeterminados seguros; sin telemetría.

## Modelos centrales

`DeviceDescriptor`, `DeviceCapabilities`, `ConnectionState`, `BatteryState?`, `TouchFrame`, `TouchContact`, `GestureCandidate`, `GestureEvent`, `MappedAction`, `AppProfile`, `UserSettings`.

## Riesgo de arquitectura

Un proceso de usuario no puede asumir que controla el flujo de una colección HID enlazada a un controlador del sistema. Si la superficie multitáctil no está accesible, se investigará un filtro firmado como componente opcional separado. Nada en la UI declarará capacidad táctil disponible antes de que el backend lo confirme.

## Estado de implementación

- WinUI 3 contiene shell y construcción de las seis vistas; todavía no hay build verificado.
- `MagicMouseDiscoveryService` enumera top-level mouse collections por Usage Page/Usage y VID/PID conocidos.
- `HidReportCaptureService` abre en lectura la ruta de interfaz HID, obtiene HIDP_CAPS y recibe buffers con lectura overlapped cancelable. El parser de informes Apple aún no está implementado.
- `GestureRecognizer` procesa `TouchFrame` normalizados; todavía no existe la etapa HID-to-TouchFrame ni action dispatcher.
- `ForegroundAppMonitor` escucha eventos de ventana y consulta el path del proceso; `ProfileResolver` busca la regla correspondiente.
