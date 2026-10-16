using Dapper;
using NexoApi.Common.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NexoApi.Features.Compras;

public class OrdenCompraPdfService(IDbConnectionFactory db)
{
    private const string Accent   = "#7C5CFF";
    private const string AccentBg = "#F4F1FF";
    private const string Texto    = "#1A1A2E";
    private const string Muted    = "#64748B";
    private const string Border   = "#E2E8F0";
    private const string Verde    = "#10B981";
    private const string Naranja  = "#F59E0B";
    private const string Blanco   = "#FFFFFF";

    public async Task<byte[]> GenerarAsync(int ocId)
    {
        using var conn = db.CreateConnection();

        var oc = await conn.QuerySingleOrDefaultAsync(
            @"SELECT oc.OrdenCompraID, oc.Codigo, oc.EstadoOC,
                     oc.FechaEmision, oc.FechaRecepcion,
                     ISNULL(oc.NroDocVisions,'') AS NroDocVisions,
                     ISNULL(oc.TipDocVisions,'') AS TipDocVisions,
                     ISNULL(p.RazonSocial,'(sin proveedor)') AS ProveedorNombre,
                     ISNULL(p.NIT,'') AS ProveedorNit,
                     ISNULL(cfg.NombreEmpresa,'NEXO ERP') AS NombreEmpresa,
                     ISNULL(cfg.NombrePropietario,'') AS NombrePropietario,
                     cfg.Logo, cfg.LogoContentType
              FROM Compras.OrdenesCompra oc
              JOIN Catalogo.Proveedores p ON p.ProveedorID = oc.ProveedorID
              CROSS JOIN (SELECT TOP 1 NombreEmpresa, NombrePropietario, Logo, LogoContentType
                          FROM Organizacion.ConfiguracionEmpresa) cfg
              WHERE oc.OrdenCompraID = @id",
            new { id = ocId })
            ?? throw new KeyNotFoundException($"Orden de compra {ocId} no encontrada.");

        var lineas = (await conn.QueryAsync(
            @"SELECT ISNULL(t.Referencia, CAST(d.ArticuloID AS VARCHAR)) AS SKU,
                     ISNULL(t.Nombre, '—') AS Articulo,
                     d.CantidadSolicitada, d.CantidadRecibida,
                     d.CostoUnitario,
                     d.CantidadSolicitada * d.CostoUnitario AS Subtotal
              FROM Compras.OrdenesCompraDetalle d
              LEFT JOIN Catalogo.Tarjetas t ON t.ArticuloID = d.ArticuloID
              WHERE d.OrdenCompraID = @id
              ORDER BY d.OrdenCompraDetalleID",
            new { id = ocId })).AsList();

        string empresa     = (string)oc.NombreEmpresa;
        string propietario = (string)oc.NombrePropietario;
        byte[]? logo       = (byte[]?)oc.Logo;
        string proveedor   = (string)oc.ProveedorNombre;
        string provNit     = (string)oc.ProveedorNit;
        string codigo      = (string)oc.Codigo;
        string estado      = (string)oc.EstadoOC;
        DateTime emision   = (DateTime)oc.FechaEmision;
        DateTime? recepcion = oc.FechaRecepcion is DBNull ? null : (DateTime?)oc.FechaRecepcion;
        string nroVisions  = (string)oc.NroDocVisions;
        string tipVisions  = (string)oc.TipDocVisions;
        bool recibida      = estado == "RECIBIDA";

        decimal totalPedido  = lineas.Sum(l => (decimal)l.Subtotal);
        decimal totalRecibido = lineas.Sum(l => (decimal)l.CantidadRecibida * (decimal)l.CostoUnitario);

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
                               .Text("ORDEN DE COMPRA").FontSize(10).Bold().FontColor(Blanco);
                            doc.Item().PaddingTop(3).AlignCenter()
                               .Text(codigo).FontSize(11).Bold().FontColor(Accent);
                            doc.Item().PaddingTop(2).AlignCenter()
                               .Text($"Emisión: {emision:dd/MM/yyyy}").FontSize(8).FontColor(Muted);
                        });
                    });

                    hdr.Item().PaddingTop(6).Height(1.5f).Background(Accent);
                });

                page.Content().PaddingTop(10).Column(body =>
                {
                    // Bloque proveedor + estado
                    body.Item().Row(row =>
                    {
                        row.RelativeItem().Background(AccentBg).Padding(10).Column(prov =>
                        {
                            prov.Item().Text("PROVEEDOR").FontSize(7).Bold().FontColor(Accent);
                            prov.Item().PaddingTop(2).Text(proveedor).FontSize(10).Bold();
                            if (!string.IsNullOrWhiteSpace(provNit))
                                prov.Item().Text($"NIT: {provNit}").FontSize(8).FontColor(Muted);
                            if (!string.IsNullOrWhiteSpace(nroVisions))
                                prov.Item().PaddingTop(2)
                                   .Text($"Doc. Visions: {tipVisions} {nroVisions}").FontSize(8).FontColor(Verde);
                        });

                        row.ConstantItem(8);

                        row.ConstantItem(130).Background(AccentBg).Padding(10).Column(est =>
                        {
                            est.Item().Text("ESTADO").FontSize(7).Bold().FontColor(Accent);
                            est.Item().PaddingTop(4).Text(estado).FontSize(11).Bold()
                               .FontColor(recibida ? Verde : Naranja);
                            if (recepcion.HasValue)
                                est.Item().PaddingTop(3)
                                   .Text($"Recibido: {recepcion:dd/MM/yyyy}").FontSize(8).FontColor(Muted);
                            est.Item().PaddingTop(2)
                               .Text($"OC ID: {ocId}").FontSize(7).FontColor(Muted);
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
                            cols.ConstantColumn(55);
                            cols.ConstantColumn(55);
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
                            h.Cell().Element(ChL).Text("ARTÍCULO").FontSize(7).Bold().FontColor(Blanco);
                            h.Cell().Element(Ch).Text("PEDIDO").FontSize(7).Bold().FontColor(Blanco);
                            h.Cell().Element(Ch).Text("RECIBIDO").FontSize(7).Bold().FontColor(Blanco);
                            h.Cell().Element(Ch).Text("COSTO UNIT.").FontSize(7).Bold().FontColor(Blanco);
                            h.Cell().Element(Ch).Text("SUBTOTAL").FontSize(7).Bold().FontColor(Blanco);
                        });

                        bool alt = false;
                        foreach (var l in lineas)
                        {
                            alt = !alt;
                            string bg = alt ? "#FFFFFF" : "#F8F9FA";

                            string sku    = (string)l.SKU;
                            string art    = (string)l.Articulo;
                            decimal sol   = (decimal)l.CantidadSolicitada;
                            decimal rec   = (decimal)l.CantidadRecibida;
                            decimal costo = (decimal)l.CostoUnitario;
                            decimal sub   = (decimal)l.Subtotal;
                            bool completo = rec >= sol;

                            IContainer CL(IContainer c) => c.Background(bg)
                                .BorderBottom(0.5f).BorderColor(Border)
                                .PaddingVertical(5).PaddingHorizontal(6);

                            table.Cell().Element(CL).Text(sku).FontSize(8).FontColor(Muted);
                            table.Cell().Element(CL).Text(art).FontSize(8);
                            table.Cell().Element(c => CL(c).AlignCenter()).Text($"{sol:N0}").FontSize(8);
                            table.Cell().Element(c => CL(c).AlignCenter())
                                 .Text($"{rec:N0}").FontSize(8)
                                 .FontColor(completo ? Verde : rec > 0 ? Naranja : Texto);
                            table.Cell().Element(c => CL(c).AlignRight()).Text($"{costo:C0}").FontSize(8);
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
                                   r.RelativeItem().Text("TOTAL PEDIDO").FontSize(8).FontColor(Muted);
                                   r.AutoItem().Text($"{totalPedido:C0}").FontSize(8);
                               });
                            tot.Item().Background(recibida ? "#F0FDF4" : AccentBg)
                               .Padding(8).Row(r =>
                               {
                                   r.RelativeItem().Text("TOTAL RECIBIDO").FontSize(10).Bold()
                                    .FontColor(recibida ? Verde : Accent);
                                   r.AutoItem().Text($"{totalRecibido:C0}").FontSize(10).Bold()
                                    .FontColor(recibida ? Verde : Accent);
                               });
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
