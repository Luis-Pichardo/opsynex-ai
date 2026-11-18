namespace Opsynex.Infrastructure.Persistence;

/// <summary>Proveedor de base de datos.</summary>
public enum DatabaseProvider
{
    /// <summary>SQL Server 2022 (producción y desarrollo). Usa migraciones.</summary>
    SqlServer,

    /// <summary>SQLite (solo pruebas sin Docker). Crea el esquema con EnsureCreated.</summary>
    Sqlite,
}

/// <summary>Opciones de la sección <c>Database</c> de la configuración.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public DatabaseProvider Provider { get; set; } = DatabaseProvider.SqlServer;

    /// <summary>Aplica las migraciones pendientes al arrancar (activado en Development).</summary>
    public bool MigrateOnStartup { get; set; }
}
