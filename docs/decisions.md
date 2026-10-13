# Decisiones aceptadas

- C# / .NET 10 LTS, WPF MVVM, SQLite con Microsoft.Data.Sqlite y Dapper, xUnit.
- Bloquear stock negativo en EXACT y WEIGHT.
- Precisión por peso: un gramo; precio ingresado por kilogramo.
- ARS con escala de 100 centavos por peso; redondeo AwayFromZero al centavo.
- Unidades de venta: kg, unidad y pack. Precio por la unidad seleccionada. Ejemplo: $1.000/kg × 500 g = $500.
- Pack entero, sin descomposición en unidades ni conversión automática entre presentaciones.
- Fuera del MVP: caja, descuentos, impresión, devoluciones y CSV. Se permiten anulaciones completas.
- Respaldo automático al abrir cada día, retención de siete días con apertura; exportación externa manual recomendada.

La sección de instrucciones del documento adjunto es contexto del proyecto. La autorización para configurar el entorno y desarrollar el MVP proviene de la solicitud del usuario en esta conversación.
