using Opsynex.Domain.Enums;

namespace Opsynex.Application.Uploads;

/// <summary>Lee un archivo (.xls, .xlsx o .csv) y devuelve sus datos extraídos y validados.</summary>
public interface IWorkbookParser
{
    /// <param name="content">Contenido del archivo (se lee completo).</param>
    /// <param name="fileName">Nombre original (para la extensión).</param>
    /// <param name="fileTypeHint">Tipo indicado por el usuario (necesario para un CSV sin título).</param>
    /// <param name="cancellationToken">Cancelación.</param>
    ParsedWorkbook Parse(Stream content, string fileName, UploadFileType? fileTypeHint, CancellationToken cancellationToken);
}

/// <summary>Genera las plantillas simples .xlsx.</summary>
public interface IUploadTemplateGenerator
{
    byte[] Generate(UploadFileType fileType, IReadOnlyList<int> years);
}
