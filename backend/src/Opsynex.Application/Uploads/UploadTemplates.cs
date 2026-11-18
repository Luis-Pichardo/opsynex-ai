using Opsynex.Domain.Enums;

namespace Opsynex.Application.Uploads;

/// <summary>
/// Plantillas simples (alternativa al libro contable completo). Cada una tiene un título que el
/// clasificador reconoce y las filas con las cuentas que se deben llenar por año.
/// </summary>
public static class UploadTemplates
{
    public sealed record Template(UploadFileType FileType, SheetType SheetType, string Title, string FileName, IReadOnlyList<string> Rows);

    public static readonly IReadOnlyList<Template> All =
    [
        new(UploadFileType.IncomeStatement, SheetType.IncomeStatement, "ESTADO DE RESULTADOS", "plantilla_estado_resultados.xlsx",
        [
            "Ventas netas", "Descuentos sobre ventas", "Costo de ventas", "Compensaciones al personal",
            "Gastos administrativos", "Depreciación & Amortización", "Gastos financieros",
            "Impuesto sobre la renta", "Ingresos netos",
        ]),
        new(UploadFileType.BalanceSheet, SheetType.BalanceSheet, "BALANCE GENERAL", "plantilla_balance_general.xlsx",
        [
            "Efectivo en cajas y bancos", "Cuentas por cobrar", "Inventarios", "Total activos corrientes",
            "Propiedad, planta y equipo, neto", "Total activos", "Cuentas por pagar", "Préstamos por pagar",
            "Total pasivos corrientes", "Préstamos a largo plazo", "Total pasivos", "Total capital",
            "Total pasivos y capital",
        ]),
        new(UploadFileType.CashFlow, SheetType.CashFlow, "ESTADO DE FLUJO DE EFECTIVO", "plantilla_flujo_efectivo.xlsx",
        [
            "Utilidad neta", "Depreciación y amortización", "Variación de capital de trabajo",
            "Flujo de efectivo de las operaciones", "Flujo de efectivo de inversión",
            "Flujo de efectivo de financiamiento",
        ]),
        new(UploadFileType.CostStructure, SheetType.CostStructure, "ESTRUCTURA DE COSTOS", "plantilla_estructura_costos.xlsx",
        [
            "Materias primas", "Labor directa", "Energía eléctrica", "Mantenimiento", "Depreciación de maquinarias",
            "Gastos generales", "Pérdida de materiales (merma)",
        ]),
        new(UploadFileType.Operational, SheetType.Operational, "DATOS OPERACIONALES", "plantilla_datos_operacionales.xlsx",
        [
            "Unidades producidas", "Cantidad de empleados", "Consumo energético (kWh)", "Scrap estimado",
            "Costos operacionales", "Costo de energía",
        ]),
    ];

    public static Template? Find(UploadFileType fileType) => All.FirstOrDefault(t => t.FileType == fileType);

    public static SheetType? SheetTypeFor(UploadFileType fileType) => Find(fileType)?.SheetType;
}
