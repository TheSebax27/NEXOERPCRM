namespace NexoWeb.Common.Dtos;

public record ReglaAutomacionItem(
    int ReglaID, string Nombre, string? Descripcion, string Evento,
    string ParametrosJSON, bool Activa, DateTime FechaCreacion
);
public record ActualizarReglaRequest(bool Activa, string ParametrosJSON);
public record EjecutarReglaResponse(int ActividadesCreadas);
