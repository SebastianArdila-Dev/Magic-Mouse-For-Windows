<p align="center"><img src="docs/images/magic-mouse.png" width="96" alt="Magic Mouse for Windows"></p>
<h1 align="center">Magic Mouse for Windows</h1>
<p align="center">Tu Magic Mouse, con una experiencia cuidada en Windows.</p>
<p align="center">Creado por <a href="https://github.com/SebastianArdila-Dev">Sebastian Ardila</a> · <a href="https://x.com/SebastianardSWE">X / Twitter</a></p>

Soy Sebastian. Creé este proyecto para que más personas puedan aprovechar su Magic Mouse en Windows sin pagar por una aplicación de configuración. Me importa tanto lo que hace como lo que se siente al usarlo: una interfaz limpia, ajustes fáciles de encontrar y una experiencia que no te interrumpa.

Esta es una **versión de desarrollo para Windows x64**. La interfaz, los perfiles y los ajustes del sistema se pueden probar ahora. La compatibilidad de los gestos físicos depende del acceso HID que permita Windows y aún necesita pruebas con un Magic Mouse real. Prefiero dejar eso claro antes de prometer algo que todavía no he podido comprobar.

![Aspecto de la aplicación](docs/images/appearance-dark.png)

## Qué puedes hacer

- Elegir apariencia clara, oscura o del sistema.
- Configurar estilo, tamaño y color del puntero, con aplicación explícita a Windows y restauración del esquema original.
- Ajustar la velocidad del puntero y el intercambio de botones de Windows.
- Preparar navegación con un dedo, escritorios con dos, taps, doble taps, scroll natural e inercia.
- Guardar perfiles por aplicación y seleccionarlos al cambiar de ventana activa.
- Ver la batería en una cabecera fija, cuando Windows expone su nivel y estado de carga.
- Cerrar la ventana y seguir en la bandeja del sistema. Desde la bandeja puedes abrir la app o salir por completo.
- Volver a conectar la lectura táctil automáticamente cuando falla, con reintentos espaciados.
- Probar gestos con el simulador local, revisar controladores y exportar ajustes o diagnóstico HID.

Los ajustes se guardan en tu usuario de Windows. No hay cuenta obligatoria, telemetría ni un servidor que reciba tus preferencias.

## Descargar e instalar

Descarga la [versión para Windows x64](https://github.com/SebastianArdila-Dev/magic-mouse-for-windows/releases/latest) y extrae el ZIP completo. No ejecutes el archivo desde dentro del ZIP: el ejecutable necesita los archivos que lo acompañan.

1. Abre `MagicMouse.Windows.App.exe` o `Instalar.cmd`.
2. En el instalador visual, arrastra el logo hacia **Aplicaciones** o pulsa **Instalar y continuar**. Se copia al directorio de programas de tu usuario y se crea un acceso en el menú Inicio, sin privilegios de administrador.
3. Si prefieres usar la carpeta directamente, elige **Usar sin instalar**.
4. Completa la bienvenida, empareja el mouse en Bluetooth cuando lo tengas y pulsa **Finalizar**. La aplicación completa aparece con una transición suave.

![Bienvenida](docs/images/welcome-dark.png)

La instalación por arrastre es una experiencia propia de esta app para Windows. No utiliza un archivo DMG ni instala componentes de macOS. Incluye el runtime de la app; no incluye un controlador multitáctil de Apple ni de terceros.

Para desinstalar la copia de usuario, sal desde la bandeja y elimina `%LOCALAPPDATA%\Programs\MagicMouseForWindows` y su acceso del menú Inicio. Los ajustes se conservan aparte en `%LOCALAPPDATA%\MagicMouseForWindows`; elimínalos solo si también quieres borrar tu configuración. Desactiva **Iniciar con Windows** antes de desinstalar.

## Compatibilidad y estado

Requiere Windows 10 versión 2004 / compilación 19041 o posterior, x64. Windows 11 también es compatible. ARM64 tiene configuración de proyecto, pero todavía no está verificado.

| Función | Estado |
| --- | --- |
| Interfaz, temas, perfiles y persistencia | Implementados y comprobados localmente |
| Velocidad e intercambio de botones de Windows | Aplicación y restauración comprobadas |
| Cursor | Archivos y asignación implementados; la comprobación del tamaño visible sobre controles aún tiene un caso pendiente |
| Gestos y scroll | Motor y simulador comprobados; falta validar hardware físico |
| Batería y carga | Solo datos reales que Windows exponga; sin hardware no se afirma compatibilidad |
| Reconexión | Observador y reintentos implementados; pendiente de prueba de desconexión Bluetooth real |
| Zonas físicas, clic simultáneo y detección de levantamiento | Desactivados; requieren un controlador de filtro compatible |

No se ofrece paridad completa con macOS ni se anuncia como controlador plug and play. Consulta [limitaciones](KNOWN_LIMITATIONS.md) y [paridad con macOS](PARIDAD-MAC.md) para distinguir funciones disponibles de las que necesitan hardware o controlador.

## Compilar y colaborar

El proyecto usa C#, .NET 8 y WinUI 3. Consulta [BUILDING.md](BUILDING.md) para compilar y crear el ZIP; [TESTING.md](TESTING.md) describe las pruebas y qué queda fuera de ellas.

Si encuentras un problema, abre un issue con tu versión de Windows, modelo del Magic Mouse, pasos para reproducirlo y qué esperabas que pasara. Revisa los archivos de diagnóstico antes de compartirlos: pueden incluir rutas de ejecutables e identificadores del dispositivo.

Si este proyecto te sirve, una estrella, una recomendación o una prueba con tu Magic Mouse ayudan mucho. También puedes seguirme en [X](https://x.com/SebastianardSWE) y [GitHub](https://github.com/SebastianArdila-Dev) para conocer lo que estoy construyendo.

Gracias por usarlo y ayudarme a mejorarlo.

— Sebastian Ardila

## Licencia

El código del proyecto se comparte bajo [MIT](LICENSE). Las dependencias y la fuente Inter mantienen sus propias licencias, detalladas en [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md). Este es un proyecto independiente, sin afiliación con Apple. Magic Mouse y Apple son marcas de Apple Inc.
