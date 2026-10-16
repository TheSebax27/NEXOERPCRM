namespace NexoWeb.Common.Dtos;

public record ModuloItem(
    int ModuloID, string Codigo, string Nombre, string? Ruta,
    int? ModuloPadreID, string? Icono, int Orden, bool EsGrupo);

public record ModuloVisibilidadItem(int ModuloID, string Codigo, string Nombre, bool? Visible, bool EsOverride = false);

public record ActualizarVisibilidadRolRequest(List<ItemVisibilidad> Items);
public record ActualizarVisibilidadUsuarioRequest(List<ItemVisibilidad> Items);
public record ItemVisibilidad(int ModuloID, bool? Visible);

public record RolDetalleItem(
    int RolID, string Nombre, string? Descripcion, bool Estado,
    int CantidadUsuarios, DateTime FechaCreacion);

public record CargoVinculadoItem(int CargoID, string Nombre, string? Departamento);

public record CrearRolRequest(string Nombre, string? Descripcion);
public record ActualizarRolRequest(string Nombre, string? Descripcion, bool Estado);
