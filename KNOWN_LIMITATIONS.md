# Limitaciones conocidas

- Los gestos físicos necesitan una colección HID accesible. La compatibilidad con un Magic Mouse real sigue pendiente de verificación.
- No se incluye un controlador de filtro firmado. Las zonas físicas, clic simultáneo y detectar levantamiento están desactivados.
- Algunos controles pueden no respetar todavía el tamaño del cursor seleccionado.
- Los reintentos de lectura no pueden evitar que el dispositivo se apague, pierda señal Bluetooth o sea reservado por el controlador de Windows.
- Batería, firmware y serial dependen de lo que Windows exponga; no se inventan.
- El indicador de clic se reproduce dentro de la app. Zoom inteligente utiliza atajos compatibles; no es el zoom semántico de macOS.
- La entrada sintética puede ser bloqueada en ventanas elevadas o el escritorio seguro.
- La distribución actual es x64, no firmada, y no tiene actualizador automático. ARM64 no verificado.
