namespace NexoApi.Features.Email;

public static class EmailTemplates
{
    private static string Layout(string nombreEmpresa, string accentColor, string contenido) => $$"""
        <!DOCTYPE html>
        <html lang="es">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <style>
          body{margin:0;padding:0;background:#f4f5f7;font-family:'Segoe UI',Arial,sans-serif;color:#1a1a2e}
          .wrap{max-width:580px;margin:32px auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.08)}
          .header{background:{{accentColor}};padding:28px 32px 20px;color:#fff}
          .header h1{margin:0;font-size:1.4rem;font-weight:700}
          .body{padding:28px 32px}
          .body h2{font-size:1.1rem;margin:0 0 12px;color:#1a1a2e}
          .body p{line-height:1.6;color:#444;font-size:0.92rem;margin:0 0 12px}
          .btn{display:inline-block;margin:16px 0;padding:12px 28px;background:{{accentColor}};color:#fff;text-decoration:none;border-radius:8px;font-weight:600;font-size:0.92rem}
          .divider{border:none;border-top:1px solid #eee;margin:20px 0}
          .detail-row{display:flex;justify-content:space-between;padding:8px 0;border-bottom:1px solid #f0f0f0;font-size:0.88rem}
          .detail-row span:last-child{font-weight:600;color:#1a1a2e}
          .footer{background:#f9f9fb;padding:16px 32px;text-align:center;font-size:0.75rem;color:#999}
        </style>
        </head>
        <body>
        <div class="wrap">
          <div class="header"><h1>{{nombreEmpresa}}</h1></div>
          <div class="body">{{contenido}}</div>
          <div class="footer">Este correo fue generado automáticamente por NEXO ERP &middot; {{nombreEmpresa}}</div>
        </div>
        </body></html>
        """;

    public static string Cotizacion(
        string nombreEmpresa, string nombreCliente, int cotizacionId,
        DateTime fecha, DateTime validoHasta, decimal total, string? notas) =>
        Layout(nombreEmpresa, "#7c5cfc",
            $"<h2>Hola {EscHtml(nombreCliente)},</h2>" +
            $"<p>Te compartimos tu cotización <strong>#{cotizacionId}</strong>. Revisa los detalles a continuación.</p>" +
            $"<div class=\"detail-row\"><span>Fecha</span><span>{fecha:dd/MM/yyyy}</span></div>" +
            $"<div class=\"detail-row\"><span>Válido hasta</span><span>{validoHasta:dd/MM/yyyy}</span></div>" +
            $"<div class=\"detail-row\"><span>Total</span><span>{total:C0}</span></div>" +
            (string.IsNullOrWhiteSpace(notas) ? "" : $"<hr class=\"divider\"><p><strong>Notas:</strong> {EscHtml(notas!)}</p>") +
            "<hr class=\"divider\"><p>Ante cualquier consulta no dudes en responder este correo o contactarnos directamente.</p>" +
            "<p>Gracias por confiar en nosotros.</p>");

    public static string CotizacionPorVencer(
        string nombreEmpresa, string nombreCliente, int cotizacionId,
        DateTime validoHasta, decimal total, int diasRestantes) =>
        Layout(nombreEmpresa, "#ff9800",
            $"<h2>Hola {EscHtml(nombreCliente)},</h2>" +
            $"<p>Te recordamos que tu cotización <strong>#{cotizacionId}</strong> vence " +
            (diasRestantes <= 0 ? "<strong>hoy</strong>" : $"en <strong>{diasRestantes} día(s)</strong>") +
            $" ({validoHasta:dd/MM/yyyy}).</p>" +
            $"<div class=\"detail-row\"><span>Total cotización</span><span>{total:C0}</span></div>" +
            "<hr class=\"divider\"><p>Si necesitas ajustes o tienes alguna pregunta, comunícate con nosotros antes de que venza el plazo.</p>" +
            "<p>Estamos aquí para ayudarte.</p>");

    public static string OrdenProduccionLista(
        string nombreEmpresa, string nombreCliente, int ordenId, string? descripcion) =>
        Layout(nombreEmpresa, "#4caf50",
            $"<h2>Hola {EscHtml(nombreCliente)},</h2>" +
            $"<p>Tu orden de producción <strong>#{ordenId}</strong> está <strong>lista</strong> y disponible para entrega.</p>" +
            (string.IsNullOrWhiteSpace(descripcion) ? "" : $"<div class=\"detail-row\"><span>Descripción</span><span>{EscHtml(descripcion!)}</span></div>") +
            "<hr class=\"divider\"><p>Coordina con nuestro equipo la fecha y forma de entrega. Puedes responder este correo o contactarnos directamente.</p>" +
            "<p>¡Gracias por elegirnos!</p>");

    public static string Bienvenida(string nombreEmpresa, string nombreCliente) =>
        Layout(nombreEmpresa, "#7c5cfc",
            $"<h2>¡Bienvenido/a, {EscHtml(nombreCliente)}!</h2>" +
            $"<p>Es un placer tenerte con nosotros. En <strong>{EscHtml(nombreEmpresa)}</strong> estamos comprometidos a ofrecerte el mejor servicio.</p>" +
            "<p>A partir de ahora podrás recibir cotizaciones, confirmaciones de órdenes y actualizaciones directamente en tu correo.</p>" +
            "<hr class=\"divider\"><p>Si tienes alguna pregunta, no dudes en contactarnos. ¡Estamos para ayudarte!</p>");

    private static string EscHtml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
