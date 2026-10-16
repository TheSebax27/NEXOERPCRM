namespace NexoWeb.Common.Dtos;

public record WhatsAppConfigItem(string? AccountSid, string? AuthToken, string FromNumber, bool Activo);

public record DestinatarioRequest(int ClienteID, string Nombre, string Telefono);
public record EnvioMasivoRequest(List<DestinatarioRequest> Destinatarios, string Mensaje);
public record ResultadoDestinatario(int ClienteID, string Nombre, string Telefono, bool Enviado, string? Error);
public record EnvioMasivoResponse(int Total, int Enviados, int Fallidos, List<ResultadoDestinatario> Resultados);
