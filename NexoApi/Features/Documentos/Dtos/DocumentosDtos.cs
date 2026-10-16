namespace NexoApi.Features.Documentos.Dtos;

public record PlantillaResumen(
    int PlantillaID, string Nombre, string? Descripcion,
    string TipoEntidad, bool Activa, DateTime FechaCreacion);

public record PlantillaDetalle(
    int PlantillaID, string Nombre, string? Descripcion,
    string TipoEntidad, string Cuerpo, string? Variables, bool Activa, DateTime FechaCreacion);

public record CrearPlantillaRequest(
    string Nombre, string? Descripcion, string TipoEntidad, string Cuerpo, string? Variables);

public record ActualizarPlantillaRequest(
    string Nombre, string? Descripcion, string TipoEntidad,
    string Cuerpo, string? Variables, bool Activa);

public record DocumentoResumen(
    int DocumentoID, int PlantillaID, string PlantillaNombre,
    string Titulo, string Estado, DateTime FechaGeneracion,
    string? EntidadNombre, int? ClienteID, int? ProveedorID, int? EmpleadoID, bool TieneFirma);

public record DocumentoDetalle(
    int DocumentoID, int PlantillaID, string PlantillaNombre,
    string Titulo, string Contenido, string Estado, DateTime FechaGeneracion,
    string? EntidadNombre, int? ClienteID, int? ProveedorID, int? EmpleadoID,
    string? FirmaBase64, string? FirmaNombre, DateTime? FirmaFecha);

public record GenerarDocumentoRequest(
    int PlantillaID, string Titulo, string Contenido,
    int? ClienteID, int? ProveedorID, int? EmpleadoID);

public record AgregarFirmaRequest(string FirmaBase64, string FirmaNombre);

public record ImportarArchivoRequest(string Base64, string NombreArchivo);
public record ImportarPlantillaResult(string Cuerpo, List<string> Variables);
