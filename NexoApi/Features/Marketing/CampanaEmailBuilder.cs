using System.Text;
using System.Text.Json;

namespace NexoApi.Features.Marketing;

/// <summary>
/// Convierte el BloqueJSON de una campaña a HTML listo para enviar.
/// Soporta bloques: titulo, parrafo, imagen, boton, separador.
/// Variables de personalización: {{nombre}}, {{empresa}}.
/// </summary>
public static class CampanaEmailBuilder
{
    public static string Build(
        string bloqueJson,
        string nombreEmpresa,
        string accentColor,
        string nombreCliente,
        string? empresaCliente,
        string? trackingPixelUrl,        // null = no tracking
        string? unsubscribeUrl)
    {
        var bloques = ParseBloques(bloqueJson);
        var body    = new StringBuilder();

        foreach (var b in bloques)
        {
            var tipo = b.TryGetProperty("tipo", out var t) ? t.GetString() : null;
            body.Append(tipo switch
            {
                "titulo"    => RenderTitulo(b, nombreCliente, empresaCliente),
                "parrafo"   => RenderParrafo(b, nombreCliente, empresaCliente),
                "imagen"    => RenderImagen(b),
                "boton"     => RenderBoton(b),
                "separador" => "<hr style=\"border:none;border-top:1px solid #eee;margin:20px 0\">",
                _           => ""
            });
        }

        // Pie con link de desuscripción
        if (!string.IsNullOrWhiteSpace(unsubscribeUrl))
            body.Append($"<p style=\"text-align:center;font-size:0.72rem;color:#aaa;margin-top:24px;\">" +
                        $"<a href=\"{unsubscribeUrl}\" style=\"color:#aaa;\">No quiero recibir más correos</a></p>");

        // Pixel de tracking (invisible)
        if (!string.IsNullOrWhiteSpace(trackingPixelUrl))
            body.Append($"<img src=\"{trackingPixelUrl}\" width=\"1\" height=\"1\" style=\"display:none\" alt=\"\">");

        return Layout(nombreEmpresa, accentColor, body.ToString());
    }

    // ── Preview (sin tracking, nombre de ejemplo) ─────────────────────────────

    public static string Preview(string bloqueJson, string nombreEmpresa, string nombreCliente = "Juan Ejemplo")
        => Build(bloqueJson, nombreEmpresa, "#7c5cfc", nombreCliente, null, null, null);

    // ── Renderers de bloques ──────────────────────────────────────────────────

    private static string RenderTitulo(JsonElement b, string nombre, string? empresa)
    {
        var texto = Personalizar(Str(b, "texto") ?? "", nombre, empresa);
        return $"<h2 style=\"font-size:1.25rem;font-weight:700;color:#1a1a2e;margin:0 0 12px\">{Esc(texto)}</h2>";
    }

    private static string RenderParrafo(JsonElement b, string nombre, string? empresa)
    {
        var texto = Personalizar(Str(b, "texto") ?? "", nombre, empresa);
        // Preservar saltos de línea
        var html = Esc(texto).Replace("\n", "<br>");
        return $"<p style=\"line-height:1.65;color:#444;font-size:0.92rem;margin:0 0 14px\">{html}</p>";
    }

    private static string RenderImagen(JsonElement b)
    {
        var src = Str(b, "src") ?? "";      // base64 data URI guardado en el bloque
        var alt = Str(b, "alt") ?? "";
        if (string.IsNullOrWhiteSpace(src)) return "";
        return $"<div style=\"text-align:center;margin:16px 0\">" +
               $"<img src=\"{src}\" alt=\"{Esc(alt)}\" style=\"max-width:100%;border-radius:8px\">" +
               $"</div>";
    }

    private static string RenderBoton(JsonElement b)
    {
        var texto = Str(b, "texto") ?? "Ver más";
        var url   = Str(b, "url") ?? "#";
        return $"<div style=\"text-align:center;margin:20px 0\">" +
               $"<a href=\"{url}\" style=\"display:inline-block;padding:12px 32px;background:#7c5cfc;" +
               $"color:#fff;text-decoration:none;border-radius:8px;font-weight:600;font-size:0.92rem\">" +
               $"{Esc(texto)}</a></div>";
    }

    // ── Layout base (mismo estilo que EmailTemplates) ─────────────────────────

    private static string Layout(string nombreEmpresa, string accentColor, string contenido) => $$"""
        <!DOCTYPE html>
        <html lang="es">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
        <style>
          body{margin:0;padding:0;background:#f4f5f7;font-family:'Segoe UI',Arial,sans-serif;color:#1a1a2e}
          .wrap{max-width:600px;margin:32px auto;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 12px rgba(0,0,0,.08)}
          .header{background:{{accentColor}};padding:28px 32px 20px;color:#fff}
          .header h1{margin:0;font-size:1.4rem;font-weight:700}
          .body{padding:28px 32px}
          .footer{background:#f9f9fb;padding:16px 32px;text-align:center;font-size:0.75rem;color:#999}
        </style>
        </head>
        <body>
        <div class="wrap">
          <div class="header"><h1>{{nombreEmpresa}}</h1></div>
          <div class="body">{{contenido}}</div>
          <div class="footer">{{nombreEmpresa}} &middot; Este correo fue enviado desde NEXO ERP</div>
        </div>
        </body></html>
        """;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static List<JsonElement> ParseBloques(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray().ToList();
        }
        catch { return []; }
    }

    private static string Personalizar(string texto, string nombre, string? empresa) =>
        texto.Replace("{{nombre}}", nombre)
             .Replace("{{empresa}}", empresa ?? "");

    private static string? Str(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) ? v.GetString() : null;

    private static string Esc(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
