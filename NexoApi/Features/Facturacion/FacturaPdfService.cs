using Dapper;
using NexoApi.Common.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NexoApi.Features.Facturacion;

public class FacturaPdfService(IDbConnectionFactory db)
{
    private const string Accent   = "#7C5CFF";
    private const string AccentBg = "#F4F1FF";
    private const string Texto    = "#1A1A2E";
    private const string Muted    = "#64748B";
    private const string Border   = "#E2E8F0";
    private const string Verde    = "#10B981";
    private const string Naranja  = "#F59E0B";
    private const string Rojo     = "#EF4444";
    private const string Blanco   = "#FFFFFF";

    public async Task<byte[]> GenerarAsync(int facturaId)
    {
        using var conn = db.CreateConnection();

        var factura = await conn.QuerySingleOrDefaultAsync(
            @"SELECT f.FacturaID, f.Fecha, f.TipDoc, f.NroDoc,
                     ISNULL(f.Notas,'') AS Notas,
                     f.VisionsConfirmado, f.StockDescontado,
                     c.Nombre AS ClienteNombre,
                     ISNULL(c.NIT,'') AS ClienteNit,
                     ISNULL(cc.Nombre,'') AS CentroCosto,
                     ISNULL(tot.Total,0) AS Total,
                     ISNULL(pag.TotalPagado,0) AS TotalPagado,
                     ISNULL(cfg.NombreEmpresa,'NEXO ERP') AS NombreEmpresa,
                     ISNULL(cfg.NombrePropietario,'') AS NombrePropietario,
                     cfg.Logo, cfg.LogoContentType
              FROM Facturacion.Facturas f
              JOIN Crm.Clientes c ON c.ClienteID = f.ClienteID
              LEFT JOIN Organizacion.CentrosCosto cc ON cc.CentroCostoID = f.CentroCostoID
              LEFT JOIN (SELECT FacturaID, SUM(Cantidad * PrecioUnitario) AS Total
                         FROM Facturacion.FacturaLineas GROUP BY FacturaID) tot ON tot.FacturaID = f.FacturaID
              LEFT JOIN (SELECT FacturaID, SUM(Monto) AS TotalPagado
                         FROM Facturacion.Pagos GROUP BY FacturaID) pag ON pag.FacturaID = f.FacturaID
              CROSS JOIN (SELECT TOP 1 NombreEmpresa, NombrePropietario, Logo, LogoContentType
                          FROM Organizacion.ConfiguracionEmpresa) cfg
              WHERE f.FacturaID = @id",
            new { id = facturaId })
            ?? throw new KeyNotFoundException($"Factura {facturaId} no encontrada.");

        var lineas = (await conn.QueryAsync(
            @"SELECT ISNULL(t.Referencia, CAST(fl.LineaID AS VARCHAR)) AS SKU,
                     COALESCE(t.Nombre, cb.Nombre, fl.DescripcionLinea, '—') AS Descripcion,
                     fl.Cantidad, fl.PrecioUnitario,
                     fl.Cantidad * fl.PrecioUnitario AS Subtotal,
                     ISNULL(fl.Nota,'') AS Nota
              FROM Facturacion.FacturaLineas fl
              LEFT JOIN Catalogo.Tarjetas t  ON t.ArticuloID = fl.ArticuloID
              LEFT JOIN Produccion.Combos cb ON cb.ComboID   = fl.ComboID
              WHERE fl.FacturaID = @id
              ORDER BY fl.LineaID",
            new { id = facturaId })).AsList();

        var pagos = (await conn.QueryAsync(
            @"SELECT Monto, FechaPago, MetodoPago, ISNULL(Notas,'') AS Notas
              FROM Facturacion.Pagos
              WHERE FacturaID = @id
              ORDER BY FechaPago",
            new { id = facturaId })).AsList();

        string empresa      = (string)factura.NombreEmpresa;
        string propietario  = (string)factura.NombrePropietario;
        byte[]? logo        = (byte[]?)factura.Logo;
        string cliente      = (string)factura.ClienteNombre;
        string nit          = (string)factura.ClienteNit;
        DateTime fecha      = (DateTime)factura.Fecha;
        string tipDoc       = (string)factura.TipDoc;
        string nroDoc       = (string?)factura.NroDoc ?? "";
        string notas        = (string)factura.Notas;
        string centroCosto  = (string)factura.CentroCosto;
        decimal total       = (decimal)factura.Total;
        decimal pagado      = (decimal)factura.TotalPagado;
        decimal saldo       = total - pagado;

        string estadoLabel = pagado <= 0 ? "PENDIENTE" : saldo > 0.01m ? "PAGO PARCIAL" : "PAGADA";
        string estadoColor = pagado <= 0 ? Rojo         : saldo > 0.01m ? Naranja         : Verde;

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
                    // Bloque cliente + estado
                    body.Item().Row(row =>
                    {
                        row.RelativeItem().Background(AccentBg).Padding(10).Column(cli =>
                        {
                            cli.Item().Text("CLIENTE").FontSize(7).Bold().FontColor(Accent);
                            cli.Item().PaddingTop(2).Text(cliente).FontSize(10).Bold();
                            if (!string.IsNullOrWhiteSpace(nit))
                                cli.Item().Text($"NIT / CC: {nit}").FontSize(8).FontColor(Muted);
                            if (!string.IsNullOrWhiteSpace(centroCosto))
                                cli.Item().Text($"Punto de venta: {centroCosto}").FontSize(8).FontColor(Muted);
                            cli.Item().PaddingTop(2).Text($"Factura ID: {facturaId}").FontSize(7).FontColor(Muted);
                        });

                        row.ConstantItem(8);

                        row.ConstantItem(130).Background(AccentBg).Padding(10).Column(est =>
                        {
                            est.Item().Text("ESTADO").FontSize(7).Bold().FontColor(Accent);
                            est.Item().PaddingTop(4).Text(estadoLabel).FontSize(11).Bold().FontColor(estadoColor);
                            est.Item().PaddingTop(3).Text($"Total: {total:C0}").FontSize(8).FontColor(Muted);
                            est.Item().Text($"Pagado: {pagado:C0}").FontSize(8).FontColor(Verde);
                        });
                    });

                    body.Item().Height(10);

                    // Tabla de líneas
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

                            string sku  = (string)l.SKU;
                            string desc = (string)l.Descripcion;
                            decimal qty = (decimal)l.Cantidad;
                            decimal pu  = (decimal)l.PrecioUnitario;
                            decimal sub = (decimal)l.Subtotal;
                            string nota = (string)l.Nota;

                            IContainer CL(IContainer c) => c.Background(bg)
                                .BorderBottom(0.5f).BorderColor(Border)
                                .PaddingVertical(5).PaddingHorizontal(6);

                            table.Cell().Element(CL).Text(sku).FontSize(8).FontColor(Muted);
                            table.Cell().Element(CL).Column(dc =>
                            {
                                dc.Item().Text(desc).FontSize(8);
                                if (!string.IsNullOrWhiteSpace(nota))
                                    dc.Item().Text(nota).FontSize(7).Italic().FontColor(Muted);
                            });
                            table.Cell().Element(c => CL(c).AlignCenter()).Text($"{qty:N0}").FontSize(8);
                            table.Cell().Element(c => CL(c).AlignRight()).Text($"{pu:C0}").FontSize(8);
                            table.Cell().Element(c => CL(c).AlignRight()).Text($"{sub:C0}").FontSize(8).Bold();
                        }
                    });

                    body.Item().Height(8);

                    // Totales
                    body.Item().Row(row =>
                    {
                        row.RelativeItem();
                        row.ConstantItem(220).Column(tot =>
                        {
                            tot.Item().BorderBottom(0.5f).BorderColor(Border)
                               .PaddingVertical(4).PaddingHorizontal(8).Row(r =>
                               {
                                   r.RelativeItem().Text("SUBTOTAL").FontSize(8).FontColor(Muted);
                                   r.AutoItem().Text($"{total:C0}").FontSize(8);
                               });
                            tot.Item().BorderBottom(0.5f).BorderColor(Border)
                               .PaddingVertical(4).PaddingHorizontal(8).Row(r =>
                               {
                                   r.RelativeItem().Text("PAGADO").FontSize(8).FontColor(Verde);
                                   r.AutoItem().Text($"{pagado:C0}").FontSize(8).FontColor(Verde);
                               });
                            tot.Item()
                               .Background(saldo > 0.01m ? "#FEF2F2" : "#F0FDF4")
                               .Padding(8).Row(r =>
                               {
                                   r.RelativeItem().Text("SALDO").FontSize(10).Bold().FontColor(saldo > 0.01m ? Rojo : Verde);
                                   r.AutoItem().Text($"{saldo:C0}").FontSize(10).Bold().FontColor(saldo > 0.01m ? Rojo : Verde);
                               });
                        });
                    });

                    if (pagos.Count > 0)
                    {
                        body.Item().Height(12);
                        body.Item().Text("DETALLE DE PAGOS").FontSize(7).Bold().FontColor(Accent);
                        body.Item().Height(4);
                        foreach (var p in pagos)
                        {
                            body.Item().BorderBottom(0.3f).BorderColor(Border).PaddingVertical(3).Row(r =>
                            {
                                r.ConstantItem(70).Text(((DateTime)p.FechaPago).ToString("dd/MM/yyyy")).FontSize(7).FontColor(Muted);
                                r.ConstantItem(90).Text((string)p.MetodoPago).FontSize(7).FontColor(Muted);
                                r.RelativeItem().Text((string)p.Notas).FontSize(7).FontColor(Muted);
                                r.ConstantItem(60).AlignRight().Text($"{(decimal)p.Monto:C0}").FontSize(7).Bold();
                            });
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(notas))
                    {
                        body.Item().Height(10);
                        body.Item().Background("#FFFBEB").BorderLeft(3).BorderColor(Naranja)
                            .Padding(8).Column(n =>
                            {
                                n.Item().Text("NOTAS").FontSize(7).Bold().FontColor(Naranja);
                                n.Item().PaddingTop(2).Text(notas).FontSize(8);
                            });
                    }
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
