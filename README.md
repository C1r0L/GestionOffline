# MiPOS — Gestión offline

MVP de escritorio en español para Windows x64. C# / .NET 10 LTS, WPF con MVVM, [WPF UI](https://github.com/lepoco/wpfui) (estilo Fluent de Windows 11), SQLite y Dapper. No requiere internet para funcionar, cuentas ni servicios externos; si hay conexión, avisa cuando se publica una versión nueva.

## Desarrollo

Instalar el SDK especificado en `global.json` (.NET 10.0.401).

```powershell
dotnet restore POS.sln --locked-mode
dotnet build POS.sln -c Release --no-restore
dotnet test POS.Tests/POS.Tests.csproj -c Release --no-restore
dotnet run --project POS.Desktop
```

WPF se ejecuta en Windows. En Linux se puede compilar gracias a `EnableWindowsTargeting` y ejecutar las pruebas de lógica, persistencia y transacciones. Para revisar la interfaz sin Windows se puede publicar para `win-x64` con `--self-contained` y abrir `POS.Desktop.exe` con Wine; sirve para ver pantallas, pero las fuentes difieren y no reemplaza la [validación en Windows](docs/windows-validation.md).

En el entorno cloud preparado:

```bash
source /workspace/tools/dotnet-env.sh
cd /workspace/GestionOffline
dotnet restore POS.sln --locked-mode
dotnet test POS.Tests/POS.Tests.csproj -c Release --no-restore
dotnet build POS.sln -c Release --no-restore
```

Cada tarea cloud está aislada: usar el checkout existente, sin crear worktrees. La aplicación no necesita procesos de servidor.

## Organización

- `POS.Core`: modelos, interfaces, precisión monetaria y cantidades.
- `POS.Infrastructure`: SQLite, migración versionada, operaciones atómicas, auditoría y backups.
- `POS.Desktop`: interfaz WPF; delega las operaciones a servicios.
  - `Theme/MiPos.xaml`: paleta, gradientes y estilos propios sobre WPF UI.
  - `Views/`: una vista por sección (venta, productos, inventario, historial, cierre).
  - `ViewModels*.cs`: `MainViewModel` dividido por sección (clase parcial).
  - Diálogos: producto, cantidad, cobro, confirmación y vista previa de impresión.
- `POS.Tests`: pruebas de aceptación que usan bases reales aisladas.

## Uso

La navegación es un menú lateral. Debajo están el estado del respaldo diario y el acceso a exportar o restaurar respaldos.

1. **Nueva venta** (pantalla inicial). Buscar por nombre o escanear el código: Enter agrega el código exacto o el primer resultado. También se puede tocar la tarjeta del producto; los productos por kilo piden el peso. En el carrito, −/+ cambian la cantidad (una unidad o 100 g), la cantidad se toca para editarla y el tacho quita la línea. Elegir el medio de pago y cobrar: en efectivo se calcula el vuelto, que no se guarda.
2. **Productos**. Crear y editar con un diálogo que solo permite combinaciones válidas de unidad y control de stock, muestra el margen sobre el costo y marca errores por campo. "Ajustar precios" abre un panel con vista previa automática por categoría, porcentaje o monto fijo y redondeo.
3. **Inventario**. Resumen por estado, filtros y un panel por producto para reponer o fijar existencias con motivo, definir el mínimo y ver movimientos. Los productos manuales se marcan como Suficiente, Bajo o Reponer.
4. **Historial**. Períodos rápidos o fechas a elección, totales del período y detalle con precios históricos. La anulación exige motivo y confirmación, conserva la venta y devuelve el stock una única vez.
5. **Cierre del día**. Totales del día, cobros por medio de pago, productos vendidos y control de efectivo (el conteo no se guarda). "Imprimir resumen" muestra una vista previa y usa el diálogo de impresión de Windows; con "Microsoft Print to PDF" se obtiene un PDF.
6. **Respaldos**. Exportar a una unidad externa. Restaurar requiere confirmación y conserva una copia previa de los datos actuales.
7. **Actualizaciones**. Al abrir, como máximo una vez por día, MiPOS consulta si hay una versión nueva publicada en GitHub. Si la hay, muestra un aviso en el menú lateral; el usuario ve las novedades y confirma. Antes de instalar se hace un respaldo en `backups`, se descarga el instalador, se verifica su SHA-256 y se instala en modo silencioso; MiPOS se cierra y se vuelve a abrir sola. No se puede actualizar con una venta en curso. Sin conexión no aparece ningún aviso. "Versión X.Y.Z", debajo de Respaldos, busca en el momento.

### Atajos de teclado

| Tecla | Acción |
| --- | --- |
| F1 / F2 | Nueva venta / ir a la búsqueda |
| F3, F4, F5, F6 | Productos, Inventario, Historial, Cierre del día |
| Enter (en la búsqueda) | Agregar el producto |
| Esc (en la búsqueda) | Limpiar la búsqueda |
| ↓ (en la búsqueda) | Pasar al carrito |
| + / − / Supr (en el carrito) | Cambiar cantidad o quitar la línea |
| F12 | Cobrar |
| Ctrl+N (en Productos) | Nuevo producto |

### Precio y cantidades

ARS se guarda en centavos (`long`); cantidades discretas como enteros y peso como gramos. No se usa `float` ni `double`. Los totales por peso se redondean al centavo con mitades hacia arriba (`AwayFromZero`).

| Unidad de venta | Precio ingresado | Cantidad de ejemplo | Total |
| --- | --- | --- | --- |
| kg | $1.000 por kg | 0,5 kg | $500 |
| unidad | $1.000 por unidad | 2 unidades | $2.000 |
| pack | $1.000 por pack | 2 packs | $2.000 |

Un pack es un artículo completo: no se desarma ni descuenta unidades integrantes. EXACT permite unidades y packs enteros; WEIGHT requiere kg y precisión de un gramo; MANUAL permite cualquiera de esas unidades sin descuento automático. La unidad y el modo de stock no cambian después de crear un producto para conservar la interpretación del historial: desactivar y crear otro si cambia su presentación.

Stock insuficiente bloquea la venta completa. Precios y nombres históricos permanecen intactos al editar productos. Si cambió un precio con un carrito abierto, se actualiza el carrito y se pide revisar antes de confirmar. Los cambios masivos se calculan sobre productos activos, pueden filtrarse por categoría y requieren una vista previa; si quedó desactualizada, se rechazan sin cambios parciales.

## Datos y recuperación

Datos en `%LOCALAPPDATA%\MiPOS\data\pos.db`. Al iniciar se aplica la migración 1 y se hace una copia diaria (si se abre la aplicación), reteniendo las últimas siete en `%LOCALAPPDATA%\MiPOS\backups`. Estas copias en el mismo equipo no protegen contra pérdida del disco: exportar regularmente a una unidad externa.

Los backups utilizan la API SQLite y validan integridad, claves foráneas, versión y esquema. Restaurar mantiene una copia `pos.db.before-restore-<id>.db` junto a la base; estas copias de seguridad previas a restauración se conservan sin borrado automático. Para recuperar, usar Restaurar respaldo y seleccionar la copia. No copiar una base en uso manualmente.

El programa no escribe datos en Program Files. El instalador se instala para el usuario actual, incluye el runtime y conserva la carpeta de datos al desinstalar.

## Instalador Windows x64

En Windows, instalar [Inno Setup 6](https://jrsoftware.org/isinfo.php) en su ubicación predeterminada y ejecutar:

```powershell
./packaging/build.ps1
```

Genera `artifacts/installer/MiPOS-Setup-x64.exe`. También hay un workflow de GitHub Actions para compilar, probar y empaquetar en Windows. No se ha enviado ni ejecutado ese workflow desde este entorno. El instalador no tiene firma de código. Incluye `THIRD-PARTY-NOTICES.txt` con la licencia de WPF UI.

Validación manual requerida antes de distribuir: [checklist Windows](docs/windows-validation.md).

### Publicar una versión

1. Actualizar `<Version>` en `Directory.Build.props` (por ejemplo `0.4.0`) y hacer merge a `main`.
2. Crear y subir el tag con la misma versión: `git tag v0.4.0 && git push origin v0.4.0`.
3. El workflow arma el instalador con esa versión y crea la Release `v0.4.0` con `MiPOS-Setup-x64.exe` y `MiPOS-Setup-x64.exe.sha256`. Las notas se generan a partir de los PR y se pueden editar en GitHub; son las "Novedades" que ve el usuario.

Las instalaciones existentes la detectan al abrirse (como máximo una vez por día). La 0.3.0 es la primera versión con actualizador: las instalaciones anteriores se actualizan a mano una vez. El actualizador solo acepta releases publicadas (no borradores ni pre-releases) con tag `vX.Y.Z` y ambos archivos.

## Alcance

No incluye control formal de caja (apertura o arqueo guardado), descuentos, impresión de tickets, devoluciones parciales, CSV, facturación fiscal, multiusuario ni sincronización. Sí incluye la anulación completa, el cierre del día informativo y la impresión de su resumen en una impresora común. Los criterios originales y decisiones aceptadas se encuentran en `docs/specs.md` y `docs/decisions.md`.
