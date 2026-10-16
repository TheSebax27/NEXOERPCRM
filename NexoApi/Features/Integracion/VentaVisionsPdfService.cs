using Dapper;
using NexoApi.Common.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NexoApi.Features.Integracion;

public class VentaVisionsPdfService(IDbConnectionFactory db)
{
    private const string Accent   = "#7C5CFF";
    private const string AccentBg = "#F4F1FF";
    private const string Texto    = "#1A1A2E";
    private const string Muted    = "#64748B";
    private const string Border   = "#E2E8F0";
    private const string Blanco   = "#FFFFFF";

    public async Task<byte[]> GenerarAsync(int centroCostoId, string tipDoc, string nroDoc)
    {
        using var conn = db.CreateConnection();

        var cabecera = await conn.QuerySingleOrDefaultAsync(
            @"SELECT TOP 1
                     MAX(ISNULL(ee.NitCliente,''))    AS NitCliente,
                     MAX(ISNULL(ee.NombreCliente,'')) AS NombreCliente,
                     MIN(ee.FechaEventoOrigen)        AS Fecha,
                     ISNULL(cc.Nombre,'')             AS CentroCosto,
                     ISNULL(cfg.NombreEmpresa,'NEXO ERP') AS NombreEmpresa,
                     ISNULL(cfg.NombrePropietario,'') AS NombrePropietario,
                     cfg.Logo, cfg.LogoContentType
              FROM Integracion.EventosEntrantes ee
              LEFT JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = ee.CentroCostoID
              CROSS JOIN (SELECT TOP 1 NombreEmpresa, NombrePropietario, Logo, LogoContentType
                          FROM Organizacion.ConfiguracionEmpresa) cfg
              WHERE ee.CentroCostoID = @CentroCostoId
                AND ee.TipDoc = @TipDoc
                AND ee.NroDoc = @NroDoc
              GROUP BY cc.Nombre, cfg.NombreEmpresa, cfg.NombrePropietario, cfg.Logo, cfg.LogoContentType",
            new { CentroCostoId = centroCostoId, TipDoc = tipDoc, NroDoc = nroDoc })
            ?? throw new KeyNotFoundException($"Documento {tipDoc} {nroDoc} no encontrado.");

        var lineas = (await conn.QueryAsync(
            @"SELECT ISNULL(CodigoArticuloVisions,'') AS Codigo,
                     ISNULL(NombreArticuloVisions,'—') AS Descripcion,
                     Cantidad,
                     CAST(ISNULL(PrecioArticuloVisions,0) AS DECIMAL(18,4)) AS PrecioUnitario,
                     Cantidad * CAST(ISNULL(PrecioArticuloVisions,0) AS DECIMAL(18,4)) AS Subtotal
              FROM Integracion.EventosEntrantes
              WHERE CentroCostoID = @CentroCostoId AND TipDoc = @TipDoc AND NroDoc = @NroDoc
              ORDER BY EventoEntranteID",
            new { CentroCostoId = centroCostoId, TipDoc = tipDoc, NroDoc = nroDoc })).AsList();

        string empresa     = (string)cabecera.NombreEmpresa;
        string propietario = (string)cabecera.NombrePropietario;
        byte[]? logo       = (byte[]?)cabecera.Logo;
        string cliente     = (string)cabecera.NombreCliente;
        string nit         = (string)cabecera.NitCliente;
        DateTime fecha     = (DateTime)cabecera.Fecha;
        string cc          = (string)cabecera.CentroCosto;
        decimal total      = lineas.Sum(l => (decimal)l.Subtotal);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(9).FontColor(Texto));

                page.Header().Column(hdr =>
                {
                    hdr.Item().Row(row =>
                    {
                        if (logo?.Length > 0)
                            row.ConstantItem(60).Height(50).Image(logo).FitArea();

                        row.RelativeItem().PaddingLeft(10).Column(emp =>
                        {
                            emp.Item().Text(empresa).FontSize(13).Bold();
                            if (!string.IsNullOrWhiteSpace(propietario))
                                emp.Item().PaddingTop(2).Text(propietario).FontSize(8).FontColor(Muted);
                        });

                        row.ConstantItem(155).Column(doc =>
                        {
                            doc.Item().Background(Accent).PaddingVertical(4).PaddingHorizontal(8)
                               .AlignCenter()
                               .Text(tipDoc).FontSize(12).Bold().FontColor(Blanco);
                            if (!string.IsNullOrWhiteSpace(nroDoc))
                                doc.Item().PaddingTop(3).AlignCenter()
                                   .Text($"N° {nroDoc}").FontSize(10).Bold().FontColor(Accent);
                            doc.Item().PaddingTop(2).AlignCenter()
                               .Text($"Fecha: {fecha:dd/MM/yyyy}").FontSize(8).FontColor(Muted);
                        });
                    });

                    hdr.Item().PaddingTop(6).Height(1.5f).Background(Accent);
                });

                page.Content().PaddingTop(10).Column(body =>
                {
                    body.Item().Background(AccentBg).Padding(10).Column(cli =>
                    {
                        cli.Item().Text("CLIENTE / DOCUMENTO").FontSize(7).Bold().FontColor(Accent);
                        cli.Item().PaddingTop(2).Text(string.IsNullOrWhiteSpace(cliente) ? "CONSUMIDOR FINAL" : cliente)
                           .FontSize(10).Bold();
                        if (!string.IsNullOrWhiteSpace(nit))
                            cli.Item().Text($"NIT / CC: {nit}").FontSize(8).FontColor(Muted);
                        if (!string.IsNullOrWhiteSpace(cc))
                            cli.Item().Text($"Punto de venta: {cc}").FontSize(8).FontColor(Muted);
                        cli.Item().PaddingTop(2).Text("Origen: VISIONS POS").FontSize(7).FontColor(Muted);
                    });

                    body.Item().Height(10);

                    body.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(65);
                            cols.RelativeColumn(3);
                            cols.ConstantColumn(38);
                            cols.ConstantColumn(80);
                            cols.ConstantColumn(80);
                        });

                        table.Header(h =>
                        {
                            static IContainer Ch(IContainer c) =>
                                c.Background(Accent).Padding(5).AlignCenter();
                            static IContainer ChL(IContainer c) =>
                                c.Background(Accent).Padding(5);

                            h.Cell().Element(Ch).Text("CÓDIGO").FontSize(7).Bold().FontColor(Blanco);
                            h.Cell().Element(ChL).Text("DESCRIPCIÓN").FontSize(7).Bold().FontColor(Blanco);
                            h.Cell().Element(Ch).Text("CANT.").FontSize(7).Bold().FontColor(Blanco);
                            h.Cell().Element(Ch).Text("P. UNIT.").FontSize(7).Bold().FontColor(Blanco);
                            h.Cell().Element(Ch).Text("SUBTOTAL").FontSize(7).Bold().FontColor(Blanco);
                        });

                        bool alt = false;
                        foreach (var l in lineas)
                        {
                            alt = !alt;
                            string bg = alt ? "#FFFFFF" : "#F8F9FA";

                            string cod  = (string)l.Codigo;
                            string desc = (string)l.Descripcion;
                            decimal qty = (decimal)l.Cantidad;
                            decimal pu  = (decimal)l.PrecioUnitario;
                            decimal sub = (decimal)l.Subtotal;

                            IContainer CL(IContainer c) => c.Background(bg)
                                .BorderBottom(0.5f).BorderColor(Border)
                                .PaddingVertical(5).PaddingHorizontal(6);

                            table.Cell().Element(CL).Text(cod).FontSize(8).FontColor(Muted);
                            table.Cell().Element(CL).Text(desc).FontSize(8);
                            table.Cell().Element(c => CL(c).AlignCenter()).Text($"{qty:N0}").FontSize(8);
                            table.Cell().Element(c => CL(c).AlignRight()).Text($"{pu:C0}").FontSize(8);
                            table.Cell().Element(c => CL(c).AlignRight()).Text($"{sub:C0}").FontSize(8).Bold();
                        }
                    });

                    body.Item().Height(8);

                    body.Item().Row(row =>
                    {
                        row.RelativeItem();
                        row.ConstantItem(220).Background(AccentBg).Padding(10).Row(r =>
                        {
                            r.RelativeItem().Text("TOTAL").FontSize(11).Bold();
                            r.AutoItem().Text($"{total:C0}").FontSize(11).Bold().FontColor(Accent);
                        });
                    });
                });

                page.Footer().Column(footer =>
                {
                    footer.Item().Height(0.5f).Background(Border);
                    footer.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem()
                         .Text($"Generado por NEXO ERP · {NexoApi.Common.ColombiaClock.Now:dd/MM/yyyy HH:mm}")
                         .FontSize(7).FontColor(Muted);
                        r.AutoItem().Text(t =>
                        {
                            t.Span("Página ").FontSize(7).FontColor(Muted);
                            t.CurrentPageNumber().FontSize(7).FontColor(Muted);
                            t.Span(" de ").FontSize(7).FontColor(Muted);
                            t.TotalPages().FontSize(7).FontColor(Muted);
                        });
                    });
                });
            });
        }).GeneratePdf();
    }
}
