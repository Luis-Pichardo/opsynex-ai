using Opsynex.Application.Abstractions.Persistence;
using Opsynex.Application.Uploads;
using Opsynex.Domain.Entities;

namespace Opsynex.Application.Industrial;

/// <summary>
/// Guarda los registros industriales de una carga. Es un upsert: un registro con la misma
/// fecha y máquina (o la misma máquina, en los estándares) reemplaza al anterior; el resto se conserva.
/// No confirma los cambios (lo hace la carga con su <c>SaveChanges</c>).
/// </summary>
public sealed class IndustrialIngestionService(
    IRepository<ProductionData> production,
    IRepository<CostData> costs,
    IRepository<StandardData> standards)
{
    public async Task StoreAsync(Upload upload, IndustrialTables tables, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(tables);
        var companyId = upload.CompanyId;

        if (tables.Production.Count > 0)
        {
            var from = tables.Production.Min(p => p.Date);
            var to = tables.Production.Max(p => p.Date);
            var existing = (await production.ListAsync(p => p.CompanyId == companyId && p.Date >= from && p.Date <= to, cancellationToken))
                .ToDictionary(p => Key(p.Date, p.MachineId), StringComparer.OrdinalIgnoreCase);
            foreach (var record in tables.Production)
            {
                if (!existing.TryGetValue(Key(record.Date, record.MachineId), out var row))
                {
                    row = new ProductionData { CompanyId = companyId, Date = record.Date, MachineId = record.MachineId };
                    await production.AddAsync(row, cancellationToken);
                }

                row.LineId = record.LineId;
                row.UnitsProduced = record.UnitsProduced;
                row.DowntimeMinutes = record.DowntimeMinutes;
                row.PlannedMinutes = record.PlannedMinutes;
                row.CycleTime = record.CycleTime;
                row.GoodUnits = record.GoodUnits;
                row.TotalUnits = record.TotalUnits;
                row.UploadId = upload.Id;
            }
        }

        if (tables.Costs.Count > 0)
        {
            var from = tables.Costs.Min(c => c.Date);
            var to = tables.Costs.Max(c => c.Date);
            var existing = (await costs.ListAsync(c => c.CompanyId == companyId && c.Date >= from && c.Date <= to, cancellationToken))
                .ToDictionary(c => Key(c.Date, c.MachineId), StringComparer.OrdinalIgnoreCase);
            foreach (var record in tables.Costs)
            {
                if (!existing.TryGetValue(Key(record.Date, record.MachineId), out var row))
                {
                    row = new CostData { CompanyId = companyId, Date = record.Date, MachineId = record.MachineId };
                    await costs.AddAsync(row, cancellationToken);
                }

                row.LaborCost = record.LaborCost;
                row.MaterialCost = record.MaterialCost;
                row.EnergyCost = record.EnergyCost;
                row.OverheadCost = record.OverheadCost;
                row.UploadId = upload.Id;
            }
        }

        if (tables.Standards.Count > 0)
        {
            var existing = (await standards.ListAsync(s => s.CompanyId == companyId, cancellationToken))
                .ToDictionary(s => s.MachineId, StringComparer.OrdinalIgnoreCase);
            foreach (var record in tables.Standards)
            {
                if (!existing.TryGetValue(record.MachineId, out var row))
                {
                    row = new StandardData { CompanyId = companyId, MachineId = record.MachineId };
                    await standards.AddAsync(row, cancellationToken);
                }

                row.ProcessName = record.ProcessName;
                row.StandardCycleTime = record.StandardCycleTime;
                row.StandardCost = record.StandardCost;
                row.CostPerMinute = record.CostPerMinute;
                row.UploadId = upload.Id;
            }
        }
    }

    private static string Key(DateOnly date, string machine) => $"{date:yyyy-MM-dd}|{machine}";
}
