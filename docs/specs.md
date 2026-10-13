# Especificación funcional y técnica — POS Offline

**Estado:** Borrador para implementación del MVP  
**Plataforma:** Windows, una sola computadora  
**Idioma de interfaz:** Español  
**Fecha:** 2026-10-13

## 1. Objetivo

Desarrollar una aplicación de escritorio sencilla, moderna y completamente offline para un comercio minorista. Debe permitir administrar un catálogo y sus precios, controlar inventario (exacto, fraccionable o aproximado/manual), registrar ventas con múltiples productos y descontar automáticamente el stock cuando corresponda. Está dirigida a una persona con escasos conocimientos técnicos.

## 2. Alcance del MVP

### Incluido

- Catálogo de productos: alta, edición, desactivación, búsqueda por nombre y código opcional, categoría, unidad de venta, precio vigente y costo opcional.
- Edición rápida de precios individuales y masivos: aumento/reducción por porcentaje o importe fijo, filtrado por categoría y vista previa antes de aplicar; redondeo configurable.
- Inventario con tres modalidades:
  - **EXACT:** artículos discretos; cantidad entera.
  - **WEIGHT:** productos vendidos por peso, como alimento suelto; cantidades fraccionables. Internamente almacenar en unidad mínima entera (por ejemplo gramos) y mostrar kilogramos.
  - **MANUAL:** stock no confiable o no medible; registrar ventas sin descuento automático de cantidad, con estado de reposición `SUFICIENTE`, `BAJO` o `REPONER` ajustable manualmente.
- Reposición, ajustes de inventario y consulta del historial de movimientos.
- Nueva venta: búsqueda rápida, carrito de múltiples productos, modificación de cantidades, eliminación de ítems, total y confirmación de cobro.
- Registrar fecha/hora, ítems, cantidad, precio unitario histórico, total y medio de pago básico (efectivo, transferencia, tarjeta, otro). No implica conciliación de pagos.
- Descontar stock automáticamente en ventas de productos `EXACT` y `WEIGHT` mediante una transacción atómica.
- Historial de ventas por fecha, detalle y anulación con reversión de movimientos de stock; nunca borrar ventas históricas.
- Copias de seguridad y restauración verificable, incluyendo exportación a una ubicación externa.
- Instalador Windows x64 y ejecución sin internet ni servicios externos.

### Fuera de alcance

- Facturación electrónica, comprobantes fiscales o integración con ARCA.
- Multiusuario, múltiples cajas o sucursales y sincronización.
- Proveedores, órdenes de compra, cuentas corrientes y fidelización.
- Integración con impresoras fiscales, balanzas, terminales de pago o lectores especiales (la entrada de un lector tipo teclado puede evaluarse después).
- Dashboard avanzado, reportes contables, impuestos complejos y cálculo de rentabilidad.
- Control formal de caja (apertura, cierre, arqueo): **pendiente de decisión**.

## 3. Stack técnico

- **Lenguaje:** C#.
- **Framework:** .NET 10 LTS.
- **UI:** WPF con patrón MVVM, estilos modernos y controles accesibles.
- **Base de datos:** SQLite, archivo local.
- **Acceso a datos:** `Microsoft.Data.Sqlite` + Dapper, consultas SQL parametrizadas.
- **Pruebas:** xUnit para reglas de negocio, persistencia y transacciones.
- **Distribución:** instalador Windows x64 con runtime incluido (self-contained).

No se necesita servidor HTTP, backend remoto, navegador embebido ni infraestructura cloud.

## 4. Arquitectura

Monolito modular con tres proyectos de aplicación y uno de pruebas:

```text
POS/
├── POS.Desktop/          # WPF, Views, ViewModels, comandos
├── POS.Core/             # Entidades, reglas de negocio, servicios, interfaces
├── POS.Infrastructure/   # SQLite, repositorios, migraciones, respaldos
└── POS.Tests/            # Pruebas automatizadas
```

Reglas de dependencia:
- `Desktop` usa los servicios de `Core` y configura las implementaciones de `Infrastructure`.
- `Infrastructure` implementa interfaces de `Core`.
- `Core` no depende de WPF ni de SQLite.
- La lógica de ventas y stock nunca se implementa dentro de los ViewModels.

## 5. Modelo de datos inicial

### `products`
- `id` (PK)
- `code` (texto, único cuando no sea nulo)
- `name` (obligatorio)
- `category` (texto; normalizable más adelante)
- `sale_unit` (`UNIT`, `KG`)
- `stock_mode` (`EXACT`, `WEIGHT`, `MANUAL`)
- `price_minor` (entero, importe en unidad monetaria mínima)
- `cost_minor` (entero, opcional)
- `is_active` (booleano)
- `created_at`, `updated_at`

### `inventory`
- `product_id` (PK/FK)
- `quantity_base` (entero, nulo para modo `MANUAL`; unidades para `EXACT`, gramos para `WEIGHT`)
- `minimum_base` (entero, opcional)
- `replenishment_status` (`SUFICIENTE`, `BAJO`, `REPONER`; usado en `MANUAL`)
- `updated_at`

### `stock_movements`
- `id` (PK)
- `product_id` (FK)
- `sale_id` (FK, opcional)
- `movement_type` (`SALE`, `RESTOCK`, `ADJUSTMENT`, `SALE_REVERSAL`)
- `delta_base` (entero con signo)
- `created_at`
- `note` (opcional)

### `sales`
- `id` (PK)
- `created_at`
- `status` (`COMPLETED`, `CANCELLED`)
- `payment_method` (`CASH`, `TRANSFER`, `CARD`, `OTHER`)
- `total_minor` (entero)
- `cancelled_at`, `cancellation_reason` (opcionales)

### `sale_items`
- `id` (PK)
- `sale_id` (FK)
- `product_id` (FK)
- `product_name_snapshot` (texto)
- `quantity_base` (entero positivo; unidades o gramos según producto)
- `unit_price_minor` (entero; precio por unidad de venta, no por gramo)
- `line_total_minor` (entero)

### `price_changes`
- `id` (PK)
- `product_id` (FK)
- `old_price_minor`, `new_price_minor` (enteros)
- `changed_at`
- `reason` (opcional)

**Notas de precisión:** no usar `float`/`double` para importes ni cantidades persistidas. Definir una política única de redondeo para calcular ventas por peso, incluyendo casos como 1,5 kg × precio por kg. Mantener importes históricos inmutables. La unidad monetaria inicial es ARS; documentar la escala monetaria elegida.

## 6. Reglas de negocio

1. No se puede vender un producto inactivo ni una cantidad igual o inferior a cero.
2. En `EXACT`, la cantidad vendida debe ser entera. En `WEIGHT`, la UI admite decimales de kilogramos y los convierte a gramos.
3. Para `EXACT` y `WEIGHT`, la venta registra movimientos negativos y actualiza inventario en la misma transacción de base de datos.
4. En `MANUAL`, la venta se registra, pero no descuenta existencias; el comerciante actualiza manualmente el estado de reposición.
5. **Política de stock insuficiente: pendiente de confirmar.** Por defecto propuesto: bloquear ventas que dejen stock negativo en modos exacto y peso; permitir corregir stock antes de confirmar.
6. Confirmar una venta es una operación atómica: insertar cabecera, ítems, movimientos y actualizar cantidades, o revertir todo.
7. La anulación no elimina registros: cambia el estado de la venta y genera movimientos compensatorios una sola vez, en una transacción.
8. Los precios vigentes pueden cambiar, pero cada venta conserva el precio realmente aplicado y el nombre del producto al momento de vender.
9. Los productos con ventas históricas no se eliminan físicamente; se desactivan.
10. Las modificaciones masivas de precios deben mostrar cantidad de productos afectados, precio anterior y nuevo antes de confirmar, y guardar auditoría.
11. Validar todos los datos en la capa de negocio y agregar restricciones de integridad en SQLite (`FOREIGN KEY`, `CHECK`, índices).
12. Habilitar `PRAGMA foreign_keys=ON` en cada conexión SQLite.

## 7. Interfaz y experiencia de usuario

Navegación principal con cuatro secciones:

1. **Nueva venta (pantalla inicial):** foco en buscador, resultados rápidos, carrito visible, cantidades editables, total destacado y confirmación clara.
2. **Productos:** listado filtrable, formulario simple y edición masiva de precios con vista previa.
3. **Inventario:** existencias, estado de reposición, ajuste manual, reposición e historial de movimientos.
4. **Historial:** filtro por fechas, ventas y detalle; opción de anular con confirmación y motivo.

Criterios UX: lenguaje cotidiano en español, controles suficientemente grandes, mensajes de error comprensibles, pocos pasos, navegación por teclado, confirmación de acciones destructivas y estados vacíos explicativos. Evitar paneles complejos y configuración técnica expuesta al usuario.

## 8. Persistencia, respaldo e instalación

- Ruta de base de datos sugerida: `%LOCALAPPDATA%\MiPOS\data\pos.db`.
- No guardar archivos modificables dentro de `Program Files`.
- Crear estructura y aplicar migraciones versionadas al primer inicio o al actualizar.
- Hacer copias consistentes mediante la API de backup de SQLite, nunca copiando a ciegas un archivo en uso.
- Permitir exportar a pendrive u otra unidad y restaurar con validación y confirmación explícita.
- Propuesta inicial: copia automática diaria si la aplicación se abre ese día, con retención acotada; definir ubicación externa recomendada y política de recuperación.
- El instalador debe incluir dependencias necesarias; la aplicación debe abrir sin internet.

## 9. Pruebas mínimas de aceptación

- Crear un producto por unidad, reponer 10 unidades y vender 3: quedan 7; historial refleja 3 vendidas al precio original.
- Vender 1,5 kg de alimento con 10 kg disponibles: quedan 8,5 kg y el total respeta el precio por kg.
- Vender un producto `MANUAL`: se registra la venta sin descontar stock; su estado de reposición no cambia automáticamente.
- Registrar una venta con varios productos: todas las líneas y movimientos se confirman juntos.
- Simular un fallo durante la confirmación: no queda una venta parcial ni inventario parcialmente descontado.
- Cambiar un precio después de vender: la venta anterior mantiene su precio histórico.
- Anular una venta: el stock se revierte exactamente una vez y el historial conserva el registro.
- Aplicar un aumento masivo: la vista previa coincide con los precios finales y quedan registros de auditoría.
- Exportar un backup y restaurarlo en una instalación de prueba: los datos coinciden.
- Ejecutar sin conexión a internet.

## 10. Orden de implementación sugerido

1. Crear solución, esquema SQLite, migraciones y pruebas de persistencia.
2. Implementar catálogo y precios.
3. Implementar inventario y movimientos.
4. Implementar carrito, confirmación transaccional y anulación de ventas.
5. Implementar historial y filtros.
6. Implementar backups, restauración e instalador.
7. Validar flujos completos con un usuario no técnico y pulir la interfaz.

## 11. Decisiones abiertas antes de cerrar el alcance

- ¿Hace falta control de caja con apertura, cierre y arqueo, o basta con registrar medio de pago?
- ¿Se permite vender con stock insuficiente o se bloquea?
- ¿Hay descuentos manuales por venta o por producto?
- ¿Se necesita imprimir un comprobante no fiscal?
- ¿Qué precisión de venta por peso se necesita: gramos, decenas de gramos u otra?
- ¿Qué política exacta de redondeo de precios se utiliza?
- ¿Se requiere editar o devolver ventas, además de anularlas?
- ¿Se necesita importar/exportar catálogo mediante CSV?

## 12. Instrucciones para Codex

1. Leer este documento completo antes de generar código.
2. Señalar ambigüedades y proponer valores predeterminados sin convertirlos silenciosamente en requisitos confirmados.
3. Presentar un plan breve por etapas y comenzar por el esquema, las reglas de negocio y las pruebas.
4. Mantener el MVP pequeño; no introducir servidores, autenticación, microservicios, sincronización ni integraciones fiscales.
5. Priorizar transacciones, trazabilidad, copias de seguridad y una interfaz accesible sobre funcionalidades adicionales.
6. Documentar cómo compilar, ejecutar pruebas y generar el instalador en Windows.
