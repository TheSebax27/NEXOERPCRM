namespace NexoWeb.Common.Dtos;

public record EmailConfigItem(
    string Proveedor, string? ApiKey, string? EmailFrom, string? NombreFrom, bool Activo
);

public record EmailContadorItem(int EmailsHoy, int LimiteGmail, DateOnly Fecha);
