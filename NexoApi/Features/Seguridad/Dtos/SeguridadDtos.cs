namespace NexoApi.Features.Seguridad.Dtos;

// ── Modulos ──────────────────────────────────────────────────────────────────

public record ModuloItem(
    int ModuloID, string Codigo, string Nombre, string? Ruta,
    int? ModuloPadreID, string? Icono, int Orden, bool EsGrupo);

public class ModuloVisibilidadItem
{
    public int ModuloID { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public bool? Visible { get; set; }
    public bool EsOverride { get; set; }
}

public record ActualizarVisibilidadRolRequest(List<ItemVisibilidad> Items);
public record ActualizarVisibilidadUsuarioRequest(List<ItemVisibilidad> Items);
public record ItemVisibilidad(int ModuloID, bool? Visible); // null = eliminar override

// ── Roles ────────────────────────────────────────────────────────────────────

public class RolDetalleItem
{
    public int RolID { get; set; }
    public string Nombre { get; set; } = "";
    public string? Descripcion { get; set; }
    public bool Estado { get; set; }
    public int CantidadUsuarios { get; set; }
    public DateTime FechaCreacion { get; set; }
    public List<CargoVinculadoItem> Cargos { get; set; } = [];
}

public record CargoVinculadoItem(int CargoID, string Nombre, string? Departamento);

public record CrearRolRequest(string Nombre, string? Descripcion);
public record ActualizarRolRequest(string Nombre, string? Descripcion, bool Estado);
