namespace NexoApi.Features.Email;

public record EmailMessage(string To, string Subject, string Html, string? ToName = null);

public record EmailContadorItem(int EmailsHoy, int LimiteGmail, DateOnly Fecha);

public interface IEmailService
{
    /// <summary>Envía un email. Devuelve false si el email no está configurado o activo.</summary>
    Task<bool> SendAsync(EmailMessage message);
    Task<EmailConfigItem> ObtenerConfigAsync();
    Task GuardarConfigAsync(EmailConfigItem config);
    Task<EmailContadorItem> ObtenerContadorAsync()
        => Task.FromResult(new EmailContadorItem(0, 500, DateOnly.FromDateTime(DateTime.UtcNow)));
    Task IncrementarContadorAsync() => Task.CompletedTask;
}

public record EmailConfigItem(
    string Proveedor, string? ApiKey, string? EmailFrom, string? NombreFrom, bool Activo
);
