# Decisiones aceptadas

- C# / .NET 10 LTS, WPF MVVM, SQLite con Microsoft.Data.Sqlite y Dapper, xUnit.
- Bloquear stock negativo en EXACT y WEIGHT.
- Precisión por peso: un gramo; precio ingresado por kilogramo.
- ARS con escala de 100 centavos por peso; redondeo AwayFromZero al centavo.
- Unidades de venta: kg, unidad y pack. Precio por la unidad seleccionada. Ejemplo: $1.000/kg × 500 g = $500.
- Pack entero, sin descomposición en unidades ni conversión automática entre presentaciones.
- Fuera del MVP: control formal de caja (apertura, arqueo guardado), descuentos, impresión de tickets, devoluciones parciales y CSV. Se permiten anulaciones completas.
- Interfaz: WPF UI 4.3.0 (licencia MIT) con tema claro Fluent y paleta propia de celestes y verdes; menú lateral en lugar de pestañas.
- Vuelto en efectivo: se calcula y muestra al cobrar; el monto recibido no se guarda.
- Cierre del día: resumen calculado a partir de las ventas guardadas (sin tablas nuevas). Las ventas anuladas no suman y se informan aparte. El efectivo contado solo se usa para mostrar la diferencia y para el resumen impreso; no se guarda.
- Actualizaciones: MiPOS consulta la última Release pública de GitHub al abrir, como máximo una vez por día, y solo avisa; instala únicamente con confirmación del usuario y sin venta en curso. Verifica el SHA-256 publicado, respalda los datos y ejecuta el instalador de Inno Setup en modo silencioso. Sin conexión no hace nada visible: la aplicación sigue funcionando sin internet. Las releases se publican desde GitHub Actions al subir un tag `vX.Y.Z`.
- Impresión: solo el resumen del cierre, en impresora común mediante el diálogo de Windows (incluye "Microsoft Print to PDF"). Sin ticketeras térmicas ni impresoras fiscales.
- Respaldo automático al abrir cada día, retención de siete días con apertura; exportación externa manual recomendada.

La sección de instrucciones del documento adjunto es contexto del proyecto. La autorización para configurar el entorno y desarrollar el MVP proviene de la solicitud del usuario en esta conversación.
