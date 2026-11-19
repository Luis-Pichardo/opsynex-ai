## Guía de usuario

### Roles

| Rol | Permisos |
|---|---|
| Admin | Crea empresas y usuarios; puede hacer todo lo del Analyst en cualquier empresa |
| Analyst | Sube archivos, revisa la homologación y vuelve a analizar |
| Client | Ve los resultados de su empresa |

### Qué archivos acepta el sistema

**Opción 1 — el libro contable completo (recomendado).** El mismo archivo de Excel que prepara
el contador (`.xls` o `.xlsx`), tal como está: con el balance general, el estado de ganancias y
pérdidas, las notas, la nómina, el cuadro de ventas mensuales, el balance de comprobación y las
hojas fiscales. No hace falta borrar hojas, filas ni fórmulas: el sistema reconoce cada hoja por
su contenido aunque su nombre cambie, y descarta las hojas fiscales o vacías.

**Opción 2 — una plantilla simple.** Para empresas sin estados formales, descarga la plantilla
(estado de resultados, balance general, flujo de efectivo, estructura de costos o datos
operacionales), escribe los montos de cada año y súbela como `.xlsx` o `.csv`. Si subes un CSV,
indica de qué tipo es. Deja vacío lo que no tengas: una celda vacía no es cero.

Límites: hasta 20 MB por archivo. El archivo original **no se guarda**: solo los montos
extraídos y el reporte de la carga.

### Privacidad

De la hoja de nómina solo se guarda la **cantidad de empleados** (total y por departamento) y la
suma de sueldos; nunca nombres, cédulas ni sueldos individuales. Los números de cuenta bancaria,
cédulas y RNC que aparezcan en las notas se reemplazan por `***`. Nada de esto se envía al
servicio de inteligencia artificial.

### Cómo leer el resultado de una carga

Después de subir el archivo verás:

- **Años detectados** (por ejemplo 2023 y 2024).
- **Hojas:** cada hoja con el tipo que se reconoció y cuántas filas se leyeron, o "ignorada" y el
  motivo (por ejemplo "hoja fiscal" o "hoja vacía").
- **Errores** (rojo): impiden usar el archivo. Ocurren si el archivo está dañado, vacío, no es
  de Excel, o no tiene ni balance ni estado de resultados. Corrige el archivo y vuelve a subirlo.
- **Advertencias** (ámbar): el archivo se usa igual, pero conviene revisarlo. Cada una indica la
  hoja, la fila y la columna:

| Advertencia | Qué significa y qué hacer |
|---|---|
| El balance no cuadra | El total de activos no es igual al de pasivos y capital (más allá del redondeo). Revisa los totales en la hoja indicada. |
| La nota no cuadra con el estado | El detalle de una nota suma distinto que la línea del estado que desglosa. Revisa ambas cifras. |
| Patrimonio | El estado de cambios en el patrimonio no refleja la ganancia del año o su saldo no coincide con el balance. |
| Balance de comprobación no confiable | No cuadra, tiene bancos o inventarios negativos o sus ventas no coinciden. Se usan los estados financieros, que son la fuente principal. |
| Nombre de empresa distinto | Una hoja tiene otro nombre de empresa en su encabezado. Verifica que todas las hojas sean de la misma empresa. |
| Subtotal no cuadra | Un total no es la suma de sus líneas. |
| Año faltante | Un estado no trae alguno de los años que sí traen los demás. |

> Pendiente (Etapas 04 a 11): homologar cuentas y leer el dashboard, el score y los problemas.
