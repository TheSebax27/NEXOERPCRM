using Dapper;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HtmlAgilityPack;
using NexoApi.Common.Data;
using NexoApi.Features.Documentos.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace NexoApi.Features.Documentos;

public interface IDocumentosService
{
    Task<IEnumerable<PlantillaResumen>> ListarPlantillasAsync();
    Task<PlantillaDetalle?> ObtenerPlantillaAsync(int id);
    Task<int> CrearPlantillaAsync(CrearPlantillaRequest request, int usuarioId);
    Task<bool> ActualizarPlantillaAsync(int id, ActualizarPlantillaRequest request, int usuarioId);
    Task<bool> EliminarPlantillaAsync(int id);

    Task<IEnumerable<DocumentoResumen>> ListarDocumentosAsync(string? estado, int? clienteId, int? proveedorId, int? empleadoId);
    Task<DocumentoDetalle?> ObtenerDocumentoAsync(int id);
    Task<int> GenerarDocumentoAsync(GenerarDocumentoRequest request, int usuarioId);
    Task<bool> AgregarFirmaAsync(int id, AgregarFirmaRequest request);
    Task<bool> ArchivarDocumentoAsync(int id);
    Task<byte[]> ExportarPdfAsync(int id);
    ImportarPlantillaResult ImportarDesdeArchivo(ImportarArchivoRequest request);
}

public class DocumentosService : IDocumentosService
{
    private readonly IDbConnectionFactory _db;

    public DocumentosService(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<PlantillaResumen>> ListarPlantillasAsync()
    {
        using var conn = _db.CreateConnection();
        const string sql = @"
            SELECT PlantillaID, Nombre, Descripcion, TipoEntidad, Activa, FechaCreacion
            FROM Documentos.PlantillasDocumento
            ORDER BY Activa DESC, Nombre";
        return await conn.QueryAsync<PlantillaResumen>(sql);
    }

    public async Task<PlantillaDetalle?> ObtenerPlantillaAsync(int id)
    {
        using var conn = _db.CreateConnection();
        const string sql = @"
            SELECT PlantillaID, Nombre, Descripcion, TipoEntidad, Cuerpo, Variables, Activa, FechaCreacion
            FROM Documentos.PlantillasDocumento
            WHERE PlantillaID = @id";
        return await conn.QuerySingleOrDefaultAsync<PlantillaDetalle>(sql, new { id });
    }

    public async Task<int> CrearPlantillaAsync(CrearPlantillaRequest r, int usuarioId)
    {
        using var conn = _db.CreateConnection();
        const string sql = @"
            INSERT INTO Documentos.PlantillasDocumento
                (Nombre, Descripcion, TipoEntidad, Cuerpo, Variables, UsuarioCreacionID)
            OUTPUT INSERTED.PlantillaID
            VALUES (@Nombre, @Descripcion, @TipoEntidad, @Cuerpo, @Variables, @UsuarioCreacionID)";
        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            r.Nombre, r.Descripcion, r.TipoEntidad, r.Cuerpo, r.Variables,
            UsuarioCreacionID = usuarioId > 0 ? (int?)usuarioId : null
        });
    }

    public async Task<bool> ActualizarPlantillaAsync(int id, ActualizarPlantillaRequest r, int usuarioId)
    {
        using var conn = _db.CreateConnection();
        const string sql = @"
            UPDATE Documentos.PlantillasDocumento
            SET Nombre = @Nombre, Descripcion = @Descripcion, TipoEntidad = @TipoEntidad,
                Cuerpo = @Cuerpo, Variables = @Variables, Activa = @Activa,
                FechaModificacion = GETDATE(), UsuarioModificacionID = @UsuarioModificacionID
            WHERE PlantillaID = @id";
        var rows = await conn.ExecuteAsync(sql, new
        {
            id, r.Nombre, r.Descripcion, r.TipoEntidad, r.Cuerpo, r.Variables, r.Activa,
            UsuarioModificacionID = usuarioId > 0 ? (int?)usuarioId : null
        });
        return rows > 0;
    }

    public async Task<bool> EliminarPlantillaAsync(int id)
    {
        using var conn = _db.CreateConnection();
        // Solo eliminar si no tiene documentos generados
        const string sql = @"
            DELETE FROM Documentos.PlantillasDocumento
            WHERE PlantillaID = @id
              AND NOT EXISTS (SELECT 1 FROM Documentos.DocumentosGenerados WHERE PlantillaID = @id)";
        var rows = await conn.ExecuteAsync(sql, new { id });
        return rows > 0;
    }

    public async Task<IEnumerable<DocumentoResumen>> ListarDocumentosAsync(
        string? estado, int? clienteId, int? proveedorId, int? empleadoId)
    {
        using var conn = _db.CreateConnection();
        const string sql = @"
            SELECT
                d.DocumentoID,
                d.PlantillaID,
                p.Nombre        AS PlantillaNombre,
                d.Titulo,
                d.Estado,
                d.FechaGeneracion,
                COALESCE(c.Nombre, pv.RazonSocial,
                    RTRIM(LTRIM(ISNULL(e.Nombres,'') + ' ' + ISNULL(e.Apellidos,''))))
                                AS EntidadNombre,
                d.ClienteID,
                d.ProveedorID,
                d.EmpleadoID,
                CAST(CASE WHEN d.FirmaBase64 IS NOT NULL THEN 1 ELSE 0 END AS bit) AS TieneFirma
            FROM Documentos.DocumentosGenerados d
            JOIN Documentos.PlantillasDocumento  p  ON p.PlantillaID  = d.PlantillaID
            LEFT JOIN Crm.Clientes               c  ON c.ClienteID   = d.ClienteID
            LEFT JOIN catalogo.Proveedores       pv ON pv.ProveedorID = d.ProveedorID
            LEFT JOIN Rrhh.Empleados             e  ON e.EmpleadoID  = d.EmpleadoID
            WHERE (@estado    IS NULL OR d.Estado     = @estado)
              AND (@clienteId IS NULL OR d.ClienteID  = @clienteId)
              AND (@proveedorId IS NULL OR d.ProveedorID = @proveedorId)
              AND (@empleadoId IS NULL OR d.EmpleadoID = @empleadoId)
            ORDER BY d.FechaGeneracion DESC";
        return await conn.QueryAsync<DocumentoResumen>(sql,
            new { estado, clienteId, proveedorId, empleadoId });
    }

    public async Task<DocumentoDetalle?> ObtenerDocumentoAsync(int id)
    {
        using var conn = _db.CreateConnection();
        const string sql = @"
            SELECT
                d.DocumentoID, d.PlantillaID, p.Nombre AS PlantillaNombre,
                d.Titulo, d.Contenido, d.Estado, d.FechaGeneracion,
                COALESCE(c.Nombre, pv.RazonSocial,
                    RTRIM(LTRIM(ISNULL(e.Nombres,'') + ' ' + ISNULL(e.Apellidos,''))))
                    AS EntidadNombre,
                d.ClienteID, d.ProveedorID, d.EmpleadoID,
                d.FirmaBase64, d.FirmaNombre, d.FirmaFecha
            FROM Documentos.DocumentosGenerados d
            JOIN Documentos.PlantillasDocumento  p  ON p.PlantillaID  = d.PlantillaID
            LEFT JOIN Crm.Clientes               c  ON c.ClienteID   = d.ClienteID
            LEFT JOIN catalogo.Proveedores       pv ON pv.ProveedorID = d.ProveedorID
            LEFT JOIN Rrhh.Empleados             e  ON e.EmpleadoID  = d.EmpleadoID
            WHERE d.DocumentoID = @id";
        return await conn.QuerySingleOrDefaultAsync<DocumentoDetalle>(sql, new { id });
    }

    public async Task<int> GenerarDocumentoAsync(GenerarDocumentoRequest r, int usuarioId)
    {
        using var conn = _db.CreateConnection();
        const string sql = @"
            INSERT INTO Documentos.DocumentosGenerados
                (PlantillaID, Titulo, Contenido, ClienteID, ProveedorID, EmpleadoID, UsuarioGeneracionID)
            OUTPUT INSERTED.DocumentoID
            VALUES (@PlantillaID, @Titulo, @Contenido, @ClienteID, @ProveedorID, @EmpleadoID, @UsuarioGeneracionID)";
        return await conn.ExecuteScalarAsync<int>(sql, new
        {
            r.PlantillaID, r.Titulo, r.Contenido,
            r.ClienteID, r.ProveedorID, r.EmpleadoID,
            UsuarioGeneracionID = usuarioId > 0 ? (int?)usuarioId : null
        });
    }

    public async Task<bool> AgregarFirmaAsync(int id, AgregarFirmaRequest r)
    {
        using var conn = _db.CreateConnection();
        const string sql = @"
            UPDATE Documentos.DocumentosGenerados
            SET FirmaBase64 = @FirmaBase64, FirmaNombre = @FirmaNombre,
                FirmaFecha = GETDATE(), Estado = 'FIRMADO'
            WHERE DocumentoID = @id AND Estado <> 'ARCHIVADO'";
        var rows = await conn.ExecuteAsync(sql, new { id, r.FirmaBase64, r.FirmaNombre });
        return rows > 0;
    }

    public async Task<bool> ArchivarDocumentoAsync(int id)
    {
        using var conn = _db.CreateConnection();
        const string sql = @"
            UPDATE Documentos.DocumentosGenerados
            SET Estado = 'ARCHIVADO'
            WHERE DocumentoID = @id AND Estado <> 'ARCHIVADO'";
        var rows = await conn.ExecuteAsync(sql, new { id });
        return rows > 0;
    }

    public async Task<byte[]> ExportarPdfAsync(int id)
    {
        var doc = await ObtenerDocumentoAsync(id);
        if (doc is null)
            throw new KeyNotFoundException($"Documento {id} no encontrado.");

        byte[]? firmaBytes = null;
        if (!string.IsNullOrEmpty(doc.FirmaBase64))
        {
            var base64 = doc.FirmaBase64.Contains(',')
                ? doc.FirmaBase64.Split(',', 2)[1]
                : doc.FirmaBase64;
            try { firmaBytes = Convert.FromBase64String(base64); }
            catch { /* firma inválida, se ignora */ }
        }

        var documento = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(s => s.FontSize(10).FontColor("#1A1A2E"));

                page.Header().BorderBottom(1).BorderColor("#6C5CE7").PaddingBottom(8).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text(doc.Titulo).FontSize(16).Bold().FontColor("#2D2D7F");
                        if (!string.IsNullOrEmpty(doc.EntidadNombre))
                            col.Item().Text(doc.EntidadNombre).FontSize(9).FontColor("#6B7280");
                    });
                    row.ConstantItem(120).AlignRight().Column(col =>
                    {
                        col.Item().Text($"Estado: {doc.Estado}").FontSize(8).FontColor("#6B7280");
                        col.Item().Text($"Fecha: {doc.FechaGeneracion:dd/MM/yyyy}").FontSize(8).FontColor("#6B7280");
                        col.Item().Text($"Doc. #{doc.DocumentoID}").FontSize(8).FontColor("#6B7280");
                    });
                });

                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Spacing(0);
                    RenderHtmlEnPdf(col, doc.Contenido);

                    if (firmaBytes is not null)
                    {
                        col.Item().PaddingTop(24).BorderTop(1).BorderColor("#E5E7EB").PaddingTop(16).Column(firma =>
                        {
                            firma.Item().Text("Firma:").FontSize(9).FontColor("#6B7280");
                            try { firma.Item().Width(180).Height(60).Image(firmaBytes!).FitWidth(); }
                            catch { /* imagen inválida */ }
                            if (!string.IsNullOrEmpty(doc.FirmaNombre))
                                firma.Item().Text(doc.FirmaNombre).FontSize(9).Bold();
                            if (doc.FirmaFecha.HasValue)
                                firma.Item().Text($"Firmado el {doc.FirmaFecha.Value:dd/MM/yyyy HH:mm}").FontSize(8).FontColor("#6B7280");
                        });
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.CurrentPageNumber().FontSize(8).FontColor("#9CA3AF");
                    t.Span(" de ").FontSize(8).FontColor("#9CA3AF");
                    t.TotalPages().FontSize(8).FontColor("#9CA3AF");
                });
            });
        });

        return documento.GeneratePdf();
    }

    public ImportarPlantillaResult ImportarDesdeArchivo(ImportarArchivoRequest request)
    {
        var bytes = Convert.FromBase64String(request.Base64);
        var ext   = Path.GetExtension(request.NombreArchivo).ToLowerInvariant();

        var cuerpo = ext switch
        {
            ".pdf"  => ExtraerTextoPdf(bytes),
            ".docx" => ExtraerTextoDocx(bytes),
            _       => throw new InvalidOperationException($"Formato no soportado: {ext}. Use .pdf o .docx")
        };

        var variables = Regex.Matches(cuerpo, @"\{\{(\w+)\}\}")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();

        return new ImportarPlantillaResult(cuerpo, variables);
    }

    // ── HTML → QuestPDF renderer ─────────────────────────────────────────

    private record EstiloInline(
        bool Bold = false, bool Italic = false, bool Underline = false,
        float FontSize = 10f, string FontColor = "#1A1A2E");

    private static void RenderHtmlEnPdf(ColumnDescriptor col, string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return;

        // Plain text fallback (legacy templates)
        var trimmed = html.TrimStart();
        if (!trimmed.StartsWith("<p") && !trimmed.StartsWith("<h") &&
            !trimmed.StartsWith("<ul") && !trimmed.StartsWith("<ol") &&
            !trimmed.StartsWith("<div"))
        {
            foreach (var raw in html.Split('\n'))
            {
                var line = System.Net.WebUtility.HtmlDecode(raw.TrimEnd('\r'));
                if (string.IsNullOrWhiteSpace(line))
                    col.Item().Height(6);
                else
                    col.Item().PaddingBottom(4).Text(line).FontSize(10).LineHeight(1.5f);
            }
            return;
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        foreach (var node in doc.DocumentNode.ChildNodes)
            RenderBloqueHtml(col, node);
    }

    private static void RenderBloqueHtml(ColumnDescriptor col, HtmlNode node)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            var t = node.InnerText.Trim();
            if (!string.IsNullOrEmpty(t))
                col.Item().PaddingBottom(4).Text(t).FontSize(10);
            return;
        }

        switch (node.Name.ToLower())
        {
            case "h1":
                col.Item().PaddingTop(10).PaddingBottom(2).Text(txt =>
                {
                    txt.DefaultTextStyle(s => s.FontSize(22).Bold());
                    RenderSpansHtml(txt, node.ChildNodes, new EstiloInline(Bold: true, FontSize: 22));
                });
                col.Item().PaddingBottom(8).BorderBottom(1).BorderColor("#6C5CE7");
                break;
            case "h2":
                col.Item().PaddingTop(8).PaddingBottom(4).Text(txt =>
                {
                    txt.DefaultTextStyle(s => s.FontSize(17).Bold());
                    RenderSpansHtml(txt, node.ChildNodes, new EstiloInline(Bold: true, FontSize: 17));
                });
                break;
            case "h3":
                col.Item().PaddingTop(6).PaddingBottom(3).Text(txt =>
                {
                    txt.DefaultTextStyle(s => s.FontSize(14).Bold());
                    RenderSpansHtml(txt, node.ChildNodes, new EstiloInline(Bold: true, FontSize: 14));
                });
                break;
            case "p":
                RenderParrafoHtml(col, node);
                break;
            case "ul":
                RenderListaHtml(col, node, false);
                break;
            case "ol":
                RenderListaHtml(col, node, true);
                break;
            case "br":
                col.Item().Height(6);
                break;
            case "div":
                foreach (var child in node.ChildNodes)
                    RenderBloqueHtml(col, child);
                break;
        }
    }

    private static void RenderParrafoHtml(ColumnDescriptor col, HtmlNode node)
    {
        var innerText = node.InnerText;
        var isBr = node.ChildNodes.Count == 1 && node.FirstChild?.Name == "br";
        if (string.IsNullOrWhiteSpace(innerText) || isBr)
        {
            col.Item().Height(8);
            return;
        }

        var cssClass = node.GetAttributeValue("class", "");
        col.Item().PaddingBottom(5).Text(txt =>
        {
            if (cssClass.Contains("ql-align-center")) txt.AlignCenter();
            else if (cssClass.Contains("ql-align-right")) txt.AlignRight();
            else if (cssClass.Contains("ql-align-justify")) txt.Justify();

            RenderSpansHtml(txt, node.ChildNodes, new EstiloInline());
        });
    }

    private static void RenderListaHtml(ColumnDescriptor col, HtmlNode node, bool numerada)
    {
        int num = 1;
        foreach (var li in node.ChildNodes.Where(n => n.Name.ToLower() == "li"))
        {
            var prefijo = numerada ? $"{num++}. " : "• ";
            var indent = li.GetAttributeValue("class", "").Contains("ql-indent") ? 20 : 12;
            col.Item().PaddingLeft(indent).PaddingBottom(3).Text(txt =>
            {
                txt.Span(prefijo);
                RenderSpansHtml(txt, li.ChildNodes, new EstiloInline());
            });
        }
    }

    private static void RenderSpansHtml(TextDescriptor txt, HtmlNodeCollection nodes, EstiloInline estilo)
    {
        foreach (var node in nodes)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                var t = System.Net.WebUtility.HtmlDecode(node.InnerText);
                if (string.IsNullOrEmpty(t)) continue;
                var span = txt.Span(t).FontSize(estilo.FontSize).FontColor(estilo.FontColor);
                if (estilo.Bold) span.Bold();
                if (estilo.Italic) span.Italic();
                if (estilo.Underline) span.Underline();
                continue;
            }

            var childEstilo = estilo;
            switch (node.Name.ToLower())
            {
                case "strong": case "b":
                    childEstilo = childEstilo with { Bold = true };
                    break;
                case "em": case "i":
                    childEstilo = childEstilo with { Italic = true };
                    break;
                case "u":
                    childEstilo = childEstilo with { Underline = true };
                    break;
                case "s": case "strike": case "del":
                    // QuestPDF strikethrough not supported on span; render as normal
                    break;
                case "span":
                    childEstilo = AplicarEstiloSpan(childEstilo, node);
                    break;
                case "br":
                    txt.Span("\n");
                    break;
            }

            if (node.Name.ToLower() != "br")
                RenderSpansHtml(txt, node.ChildNodes, childEstilo);
        }
    }

    private static EstiloInline AplicarEstiloSpan(EstiloInline estilo, HtmlNode node)
    {
        var style = node.GetAttributeValue("style", "");
        var cssClass = node.GetAttributeValue("class", "");

        // Color
        var rgbMatch = Regex.Match(style, @"color:\s*rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)");
        if (rgbMatch.Success)
        {
            int r = int.Parse(rgbMatch.Groups[1].Value);
            int g = int.Parse(rgbMatch.Groups[2].Value);
            int b = int.Parse(rgbMatch.Groups[3].Value);
            estilo = estilo with { FontColor = $"#{r:X2}{g:X2}{b:X2}" };
        }
        else
        {
            var hexMatch = Regex.Match(style, @"color:\s*(#[0-9a-fA-F]{3,6})");
            if (hexMatch.Success)
                estilo = estilo with { FontColor = hexMatch.Groups[1].Value };
        }

        // Font size from style
        var sizeMatch = Regex.Match(style, @"font-size:\s*([\d.]+)px");
        if (sizeMatch.Success && float.TryParse(sizeMatch.Groups[1].Value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var px))
            estilo = estilo with { FontSize = px };

        // Font size from Quill class
        if (cssClass.Contains("ql-size-small")) estilo = estilo with { FontSize = 8f };
        else if (cssClass.Contains("ql-size-large")) estilo = estilo with { FontSize = 14f };
        else if (cssClass.Contains("ql-size-huge")) estilo = estilo with { FontSize = 20f };

        return estilo;
    }

    // ─────────────────────────────────────────────────────────────────────
    private static string ExtraerTextoPdf(byte[] bytes)
    {
        var sb = new StringBuilder();
        using var doc = PdfDocument.Open(bytes);
        foreach (var page in doc.GetPages())
        {
            foreach (var word in page.GetWords())
                sb.Append(word.Text).Append(' ');
            sb.AppendLine();
        }
        return sb.ToString().Trim();
    }

    private static string ExtraerTextoDocx(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var wordDoc = WordprocessingDocument.Open(stream, false);
        var body = wordDoc.MainDocumentPart?.Document?.Body;
        if (body is null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var para in body.Elements<Paragraph>())
        {
            var linea = para.InnerText;
            sb.AppendLine(linea);
        }
        return sb.ToString().Trim();
    }
}
