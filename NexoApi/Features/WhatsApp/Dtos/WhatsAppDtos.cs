namespace NexoApi.Features.WhatsApp.Dtos;

public record EnviarWhatsAppRequest(string Telefono, string Mensaje);
public record WhatsAppResponse(bool Enviado, string? MensajeSid, string? Error);
public record WhatsAppConfigItem(string? AccountSid, string? AuthToken, string FromNumber, bool Activo);

// Envío masivo
public record EnvioMasivoRequest(List<DestinatarioRequest> Destinatarios, string Mensaje);
public record DestinatarioRequest(int ClienteID, string Nombre, string Telefono);
public record ResultadoDestinatario(int ClienteID, string Nombre, string Telefono, bool Enviado, string? Error);
