# Validación en Windows antes de distribuir

Estas comprobaciones requieren Windows y aún no se ejecutaron en el entorno cloud Linux.

1. Ejecutar `packaging/build.ps1`; comprobar que finalizan pruebas y compilación de Inno Setup.
2. Instalar en Windows x64 limpio, sin .NET instalado, con internet desactivado. Abrir desde el acceso directo.
3. Crear producto por unidad, reponer 10, vender 3 y verificar stock 7, total y detalle histórico.
4. Crear producto por kg a $1.000/kg, reponer 10 kg, vender 0,5 kg y comprobar $500 y 9,5 kg restantes. Vender 1,5 kg y comprobar $1.500.
5. Crear pack a $1.000, reponer cinco packs y vender dos. Comprobar $2.000 y tres packs restantes.
6. Crear producto MANUAL, establecer BAJO, venderlo y comprobar que su estado no cambia.
7. Confirmar una venta mixta y verificar todas las líneas. Intentar una venta con stock insuficiente y comprobar que ninguna existencia cambia.
8. Cambiar un precio y verificar que la venta anterior conserva el nombre y precio históricos. Generar un cambio masivo por categoría y comprobar vista previa y precios aplicados.
9. Anular una venta con motivo; comprobar stock revertido, historial conservado y rechazo de segunda anulación.
10. Exportar un backup a una unidad externa; restaurarlo en una instalación de prueba y comparar catálogo, stock e historial. Comprobar respaldo previo y copia diaria.
11. Recorrer con teclado, verificar foco inicial, tabulación, lector de pantalla y legibilidad con escala de pantalla de 125% y 150%.
12. Cerrar y abrir sin internet; confirmar datos persistidos. Desinstalar y verificar que los datos permanecen en LocalAppData.

## Interfaz rediseñada (WPF UI)

Las pantallas se revisaron con Wine en Linux, que no reproduce fielmente fuentes, sombras, foco de ventanas ni impresión. Verificar en Windows 10 y Windows 11:

13. Aspecto general: menú lateral con gradiente, barra de título propia (minimizar, maximizar, cerrar y arrastre), fuentes Segoe UI, sin cuadrados en lugar de caracteres. Menús y tooltips sin bordes negros.
14. Atajos: F1 a F6 cambian de sección; F2 enfoca la búsqueda; F12 cobra; en el carrito, +, − y Suprimir; Ctrl+N en Productos.
15. Lector de códigos de barras tipo teclado: escanear seguido varios productos sin tocar el mouse; cada lectura agrega el producto y el foco vuelve a la búsqueda.
16. Producto por kilo: tocar la tarjeta, ingresar 0,5 y confirmar con Enter; el foco vuelve a la búsqueda y un Enter vacío no agrega nada.
17. Cobro en efectivo: con "Paga con" menor al total el botón queda deshabilitado; con monto mayor se muestra el vuelto y el mensaje final lo repite. Con transferencia o tarjeta no se pide monto.
18. Diálogos (confirmación, cantidad, cobro, producto): se abren centrados, con el alto justo, sin cortar botones al aparecer errores, y en acciones destructivas el foco inicial está en Cancelar.
19. Productos: el panel "Ajustar precios" no tapa información necesaria a 1040×680; al aplicar se cierra y no vuelve a aplicar el mismo ajuste.
20. Inventario e Historial: la selección se mantiene después de reponer, fijar existencias o anular.
21. Cierre del día: los totales coinciden con el Historial del mismo día; imprimir en una impresora real y en "Microsoft Print to PDF", y revisar que el documento entre en una página A4 o carta con los datos completos.
22. Escala de pantalla 100%, 125% y 150% y ventana mínima (1040×680): sin textos cortados ni barras de desplazamiento horizontales en las tablas.

## Actualizador

23. Instalar una versión con actualizador (0.3.0 o posterior), publicar una release más nueva con un tag `vX.Y.Z` (por ejemplo `v0.3.1`) y abrir MiPOS: aparece el aviso, las novedades coinciden con la release, "Descargar e instalar" respalda los datos en `backups`, instala sin pedir permisos de administrador y vuelve a abrir MiPOS en la versión nueva con los datos intactos.
24. Con una venta en curso, el diálogo no permite instalar. Sin conexión no aparece ningún aviso y "Buscar actualizaciones" informa el problema. Al volver a abrir el mismo día no se repite la búsqueda automática.

Registrar versión de Windows, artefacto, fecha, resultados y cualquier defecto encontrado. La compilación desde Linux no sustituye estos pasos.
