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

Registrar versión de Windows, artefacto, fecha, resultados y cualquier defecto encontrado. La compilación desde Linux no sustituye estos pasos.
