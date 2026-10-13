# MiPOS — Gestión offline

MVP de escritorio en español para Windows x64. C# / .NET 10 LTS, WPF con MVVM, SQLite y Dapper. No requiere internet para funcionar, cuentas ni servicios externos.

## Desarrollo

Instalar el SDK especificado en `global.json` (.NET 10.0.401).

```powershell
dotnet restore POS.sln --locked-mode
dotnet build POS.sln -c Release --no-restore
dotnet test POS.Tests/POS.Tests.csproj -c Release --no-restore
dotnet run --project POS.Desktop
```

WPF se ejecuta en Windows. En Linux se puede compilar gracias a `EnableWindowsTargeting` y ejecutar las pruebas de lógica, persistencia y transacciones; no se puede ejecutar la interfaz.

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
- `POS.Desktop`: vistas WPF, ViewModels y comandos; delega las operaciones a servicios.
- `POS.Tests`: pruebas de aceptación que usan bases reales aisladas.

## Uso

1. En **Productos**, crear productos y elegir la unidad de venta y el control de stock.
2. En **Inventario**, reponer existencias o establecer un valor absoluto con motivo. En stock manual, elegir SUFICIENTE, BAJO o REPONER. El mínimo opcional permite señalar reposición en stock medible.
3. En **Nueva venta**, buscar un producto, ingresar cantidad y agregarlo al carrito. Seleccionar una línea para cambiar su cantidad o quitarla. Revisar el total y confirmar el cobro.
4. En **Historial**, filtrar fechas y consultar el detalle. La anulación requiere motivo y confirmación, conserva la venta y devuelve stock una única vez.
5. Exportar respaldos a una unidad externa. Restaurar requiere confirmación y conserva una copia previa de los datos actuales.

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

Genera `artifacts/installer/MiPOS-Setup-x64.exe`. También hay un workflow de GitHub Actions para compilar, probar y empaquetar en Windows. No se ha enviado ni ejecutado ese workflow desde este entorno. El instalador inicial no tiene firma de código.

Validación manual requerida antes de distribuir: [checklist Windows](docs/windows-validation.md).

## Alcance

No incluye control de caja, descuentos, impresión, devoluciones parciales, CSV, facturación fiscal, multiusuario ni sincronización. La anulación completa sí está incluida. Los criterios originales y decisiones aceptadas se encuentran en `docs/specs.md` y `docs/decisions.md`.
