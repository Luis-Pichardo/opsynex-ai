namespace Opsynex.Application.Uploads;

/// <summary>Opciones de la sección <c>Upload</c>.</summary>
public sealed class UploadOptions
{
    public const string SectionName = "Upload";

    public static readonly string[] AllowedExtensions = [".xls", ".xlsx", ".csv"];

    /// <summary>Tamaño máximo del archivo en MB.</summary>
    public int MaxMb { get; set; } = 20;

    /// <summary>Filas máximas que se leen por hoja (el resto se ignora con una advertencia).</summary>
    public int MaxRowsPerSheet { get; set; } = 5_000;

    /// <summary>Hojas máximas que se leen por libro.</summary>
    public int MaxSheets { get; set; } = 60;

    public long MaxBytes => MaxMb * 1024L * 1024L;
}
