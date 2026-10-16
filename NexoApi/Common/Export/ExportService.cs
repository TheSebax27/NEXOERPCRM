using ClosedXML.Excel;
using NexoApi.Features.Catalogo.Dtos;
using NexoApi.Features.Crm.Dtos;
using NexoApi.Features.Rrhh.Dtos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NexoApi.Common.Export;

public static class ExportService
{
    public static byte[] GenerarExcelArticulos(IEnumerable<ArticuloItem> articulos)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Artículos");

        string[] headers =
        {
            "SKU", "Nombre", "Tipo", "Marca", "Grupo Mayor", "Grupo Menor",
            "Presentación", "Costo", "P.Público", "P.Bodega", "P.Crédito",
            "Existencias", "Stock Mín.", "Punto Reorden", "Estado"
        };
        EstilizarEncabezados(ws, headers);

        int row = 2;
        foreach (var a in articulos)
        {
            ws.Cell(row, 1).Value = a.Referencia;
            ws.Cell(row, 2).Value = a.Nombre;
            ws.Cell(row, 3).Value = a.TipoArticulo;
            ws.Cell(row, 4).Value = a.MarcaNombre ?? a.MarcaCodigo ?? "";
            ws.Cell(row, 5).Value = a.GrupoMayorNombre ?? a.GrupoMayorCodigo ?? "";
            ws.Cell(row, 6).Value = a.GrupoMenorNombre ?? a.GrupoMenorCodigo ?? "";
            ws.Cell(row, 7).Value = a.PresentacionNombre ?? a.PresentacionCodigo ?? "";
            ws.Cell(row, 8).Value = (double)(a.Costo ?? 0);
            ws.Cell(row, 9).Value = (double)(a.PPublico ?? 0);
            ws.Cell(row, 10).Value = (double)(a.PBodega ?? 0);
            ws.Cell(row, 11).Value = (double)(a.PCredito ?? 0);
            ws.Cell(row, 12).Value = (double)a.Existencias;
            ws.Cell(row, 13).Value = (double)a.StockMinimo;
            ws.Cell(row, 14).Value = (double)a.PuntoReorden;
            ws.Cell(row, 15).Value = a.Estado ? "Activo" : "Inactivo";

            if (row % 2 == 0)
                ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F7FA");

            row++;
        }

        for (int col = 8; col <= 14; col++)
            ws.Column(col).Style.NumberFormat.Format = "#,##0.00";

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static byte[] GenerarExcelEmpleados(IEnumerable<EmpleadoExportRow> empleados)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Empleados");

        string[] headers =
        {
            "ID", "Nombres", "Apellidos", "Cargo", "Departamento", "Centro de Costo",
            "Fecha Ingreso", "Teléfono", "Email", "Estado", "Tarifa/Hora", "Tipo Sangre"
        };
        EstilizarEncabezados(ws, headers);

        int row = 2;
        foreach (var e in empleados)
        {
            ws.Cell(row, 1).Value  = e.EmpleadoID;
            ws.Cell(row, 2).Value  = e.Nombres;
            ws.Cell(row, 3).Value  = e.Apellidos;
            ws.Cell(row, 4).Value  = e.Cargo ?? "";
            ws.Cell(row, 5).Value  = e.Departamento ?? "";
            ws.Cell(row, 6).Value  = e.CentroCosto ?? "";
            ws.Cell(row, 7).Value  = e.FechaIngreso ?? "";
            ws.Cell(row, 8).Value  = e.Telefono ?? "";
            ws.Cell(row, 9).Value  = e.Email ?? "";
            ws.Cell(row, 10).Value = e.Estado ? "Activo" : "Inactivo";
            ws.Cell(row, 11).Value = e.TarifaHora.HasValue ? (double)e.TarifaHora.Value : 0d;
            ws.Cell(row, 12).Value = e.TipoSangre ?? "";
            if (row % 2 == 0)
                ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F7FA");
            row++;
        }

        ws.Column(11).Style.NumberFormat.Format = "#,##0.00";
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static byte[] GenerarPdfCotizacion(CotizacionPdfData d)
    {
        var total = d.Lineas.Sum(l => l.Subtotal);
        const string azul = "#1E3A5F";

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(1.5f, Unit.Centimetre);
            page.MarginVertical(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(9f));

            page.Content().Column(col =>
            {
                // ── Encabezado ─────────────────────────────
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(d.Empresa).Bold().FontSize(18).FontColor(azul);
                        c.Item().Text("Cotización de Venta").FontSize(11).FontColor(Colors.Grey.Darken1);
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().AlignRight().Text($"COTIZACIÓN #{d.CotizacionID}").Bold().FontSize(14).FontColor(azul);
                        c.Item().AlignRight().Text($"Fecha: {d.Fecha:dd/MM/yyyy}").FontColor(Colors.Grey.Darken2);
                        if (d.ValidoHasta.HasValue)
                        {
                            var color = d.ValidoHasta.Value < NexoApi.Common.ColombiaClock.Today ? Colors.Red.Darken1 : Colors.Grey.Darken2;
                            c.Item().AlignRight().Text($"Válida hasta: {d.ValidoHasta.Value:dd/MM/yyyy}").FontColor(color);
                        }
                        else
                        {
                            c.Item().AlignRight().Text("Sin fecha límite").FontColor(Colors.Grey.Medium);
                        }
                    });
                });

                col.Item().PaddingVertical(8).LineHorizontal(2).LineColor(azul);

                col.Item().PaddingBottom(12).Column(c =>
                {
                    c.Item().Text("Para:").FontSize(8).Bold().FontColor(Colors.Grey.Medium);
                    c.Item().Text(d.Cliente).Bold().FontSize(12);
                });

                // ── Tabla de líneas ────────────────────────
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.ConstantColumn(55);
                        cols.RelativeColumn(3);
                        cols.ConstantColumn(55);
                        cols.ConstantColumn(82);
                        cols.ConstantColumn(82);
                    });

                    table.Header(h =>
                    {
                        h.Cell().Background(azul).Padding(5).Text("SKU").FontColor(Colors.White).Bold().FontSize(8.5f);
                        h.Cell().Background(azul).Padding(5).Text("Descripción").FontColor(Colors.White).Bold().FontSize(8.5f);
                        h.Cell().Background(azul).Padding(5).AlignRight().Text("Cant.").FontColor(Colors.White).Bold().FontSize(8.5f);
                        h.Cell().Background(azul).Padding(5).AlignRight().Text("P. Unitario").FontColor(Colors.White).Bold().FontSize(8.5f);
                        h.Cell().Background(azul).Padding(5).AlignRight().Text("Subtotal").FontColor(Colors.White).Bold().FontSize(8.5f);
                    });

                    int idx = 0;
                    foreach (var l in d.Lineas)
                    {
                        string bg = idx++ % 2 == 0 ? Colors.White : "#F9FAFB";
                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(5).Text(l.SkuArticulo).FontSize(8.5f);
                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(5).Text(l.NombreArticulo).FontSize(8.5f);
                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(5).AlignRight().Text($"{l.Cantidad:N2}").FontSize(8.5f);
                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(5).AlignRight().Text($"{l.PrecioUnitario:N0}").FontSize(8.5f);
                        table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(5).AlignRight().Text($"{l.Subtotal:N0}").Bold().FontSize(8.5f);
                    }
                });

                // ── Total ──────────────────────────────────
                col.Item().AlignRight().PaddingTop(10).Row(row =>
                {
                    row.AutoItem().Padding(6).Text("TOTAL").Bold().FontSize(11);
                    row.ConstantItem(82).Padding(6).AlignRight().Text($"${total:N0}").Bold().FontSize(14).FontColor(azul);
                });

                // ── Notas ──────────────────────────────────
                if (!string.IsNullOrWhiteSpace(d.Notas))
                {
                    col.Item().PaddingTop(20).Column(c =>
                    {
                        c.Item().Text("Notas:").Bold().FontSize(9).FontColor(Colors.Grey.Medium);
                        c.Item().Text(d.Notas).FontSize(9).FontColor("#374141");
                    });
                }
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Medium));
                t.Span($"Generado por NEXO ERP — {NexoApi.Common.ColombiaClock.Now:dd/MM/yyyy HH:mm}");
            });
        })).GeneratePdf();
    }

    public static byte[] GenerarFichaTecnicaPdf(ArticuloItem a, byte[]? imagenBytes, string? imagenContentType, string empresa)
    {
        const string azul = "#1E3A5F";

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(1.5f, Unit.Centimetre);
            page.MarginVertical(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(9f));

            page.Content().Column(col =>
            {
                // Encabezado
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(empresa).Bold().FontSize(16).FontColor(azul);
                        c.Item().Text("Ficha Técnica de Artículo").FontSize(10).FontColor(Colors.Grey.Darken1);
                    });
                    row.AutoItem().AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text(a.Referencia).Bold().FontSize(14).FontColor(azul);
                        c.Item().AlignRight().Text(a.Estado ? "ACTIVO" : "INACTIVO")
                            .FontColor(a.Estado ? Colors.Green.Darken2 : Colors.Red.Darken1).FontSize(9);
                    });
                });

                col.Item().PaddingVertical(6).LineHorizontal(2).LineColor(azul);

                // Imagen + datos básicos
                col.Item().Row(row =>
                {
                    if (imagenBytes != null && imagenBytes.Length > 0)
                    {
                        row.ConstantItem(110).PaddingRight(12)
                            .Image(imagenBytes).FitArea();
                    }

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(a.Nombre).Bold().FontSize(13);
                        if (!string.IsNullOrWhiteSpace(a.Descripcion))
                            c.Item().PaddingTop(4).Text(a.Descripcion).FontSize(9).FontColor(Colors.Grey.Darken2);
                        c.Item().PaddingTop(8).Table(t =>
                        {
                            t.ColumnsDefinition(cols => { cols.RelativeColumn(); cols.RelativeColumn(); });
                            void Fila(string label, string? val)
                            {
                                if (string.IsNullOrWhiteSpace(val)) return;
                                t.Cell().Padding(3).Text(label).FontColor(Colors.Grey.Medium).FontSize(8);
                                t.Cell().Padding(3).Text(val).FontSize(8.5f);
                            }
                            Fila("Tipo", a.TipoArticulo);
                            Fila("Marca", a.MarcaNombre ?? a.MarcaCodigo);
                            Fila("Grupo mayor", a.GrupoMayorNombre ?? a.GrupoMayorCodigo);
                            Fila("Grupo menor", a.GrupoMenorNombre ?? a.GrupoMenorCodigo);
                            Fila("Presentación", a.PresentacionNombre ?? a.PresentacionCodigo);
                            Fila("IVA", a.IvaDescripcion is not null ? $"{a.IvaValor}% – {a.IvaDescripcion}" : null);
                            Fila("Peso", a.Peso.HasValue ? $"{a.Peso:N2} kg" : null);
                        });
                    });
                });

                col.Item().PaddingVertical(10).LineHorizontal(1).LineColor("#E5E7EB");

                // Tabla de precios
                col.Item().PaddingBottom(6).Text("Precios").Bold().FontSize(10).FontColor(azul);
                col.Item().Table(t =>
                {
                    t.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn();
                        cols.ConstantColumn(90);
                        cols.ConstantColumn(90);
                    });

                    t.Header(h =>
                    {
                        h.Cell().Background(azul).Padding(5).Text("Lista de precio").FontColor(Colors.White).Bold().FontSize(8.5f);
                        h.Cell().Background(azul).Padding(5).AlignRight().Text("Precio").FontColor(Colors.White).Bold().FontSize(8.5f);
                        h.Cell().Background(azul).Padding(5).AlignRight().Text("Costo").FontColor(Colors.White).Bold().FontSize(8.5f);
                    });

                    void FilaPrecio(string label, decimal? precio, decimal? costo, string bg)
                    {
                        t.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(5).Text(label).FontSize(8.5f);
                        t.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(5).AlignRight()
                            .Text(precio.HasValue ? $"${precio:N0}" : "–").FontSize(8.5f);
                        t.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(5).AlignRight()
                            .Text(costo.HasValue ? $"${costo:N2}" : "–").FontSize(8.5f);
                    }
                    FilaPrecio("P. Público",  a.PPublico,  a.Costo, Colors.White);
                    FilaPrecio("P. Bodega",   a.PBodega,   a.Costo, "#F9FAFB");
                    FilaPrecio("P. Crédito",  a.PCredito,  a.Costo, Colors.White);
                });

                col.Item().PaddingTop(10).Table(t =>
                {
                    t.ColumnsDefinition(cols => { cols.RelativeColumn(); cols.RelativeColumn(); cols.RelativeColumn(); });
                    t.Cell().Background("#F0F9FF").Padding(8).Column(c2 =>
                    {
                        c2.Item().Text("Stock actual").FontSize(8).FontColor(Colors.Grey.Medium);
                        c2.Item().Text($"{a.Existencias:N0}").Bold().FontSize(14).FontColor(azul);
                    });
                    t.Cell().Background("#F0FDF4").Padding(8).Column(c2 =>
                    {
                        c2.Item().Text("Stock mínimo").FontSize(8).FontColor(Colors.Grey.Medium);
                        c2.Item().Text($"{a.StockMinimo:N0}").Bold().FontSize(12);
                    });
                    t.Cell().Background("#FFF7ED").Padding(8).Column(c2 =>
                    {
                        c2.Item().Text("Punto de reorden").FontSize(8).FontColor(Colors.Grey.Medium);
                        c2.Item().Text($"{a.PuntoReorden:N0}").Bold().FontSize(12);
                    });
                });
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Medium));
                t.Span($"Generado por NEXO ERP — {NexoApi.Common.ColombiaClock.Now:dd/MM/yyyy HH:mm}");
            });
        })).GeneratePdf();
    }

    public static byte[] GenerarExcelClientes(IEnumerable<ClienteItem> clientes)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Clientes");

        string[] headers = { "Nombre", "NIT", "Tipo Cliente", "Tipo Persona", "Teléfono", "Email",
            "Dirección", "Ciudad", "Departamento", "Responsable", "Fuente Contacto",
            "Total Contactos", "Última Interacción", "Estado" };
        EstilizarEncabezados(ws, headers);

        int row = 2;
        foreach (var c in clientes)
        {
            ws.Cell(row, 1).Value  = c.Nombre;
            ws.Cell(row, 2).Value  = c.NIT ?? "";
            ws.Cell(row, 3).Value  = c.TipoCliente ?? "";
            ws.Cell(row, 4).Value  = c.TipoPersona ?? "";
            ws.Cell(row, 5).Value  = c.Telefono ?? "";
            ws.Cell(row, 6).Value  = c.Email ?? "";
            ws.Cell(row, 7).Value  = c.Direccion ?? "";
            ws.Cell(row, 8).Value  = c.Ciudad ?? "";
            ws.Cell(row, 9).Value  = c.Departamento ?? "";
            ws.Cell(row, 10).Value = c.Responsable ?? "";
            ws.Cell(row, 11).Value = c.FuenteContacto ?? "";
            ws.Cell(row, 12).Value = c.TotalContactos;
            ws.Cell(row, 13).Value = c.UltimaInteraccion.HasValue ? c.UltimaInteraccion.Value.ToString("dd/MM/yyyy") : "";
            ws.Cell(row, 14).Value = c.Estado ? "Activo" : "Inactivo";
            if (row % 2 == 0)
                ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F7FA");
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static byte[] GenerarPdfClientes(IEnumerable<ClienteItem> clientes, string empresa)
    {
        const string azul = "#1E3A5F";
        var lista = clientes.ToList();

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.MarginHorizontal(1.5f, Unit.Centimetre);
            page.MarginVertical(1.2f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(8f));

            page.Header().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(empresa).Bold().FontSize(14).FontColor(azul);
                    c.Item().Text("Listado de Clientes").FontSize(10).FontColor(Colors.Grey.Darken1);
                });
                row.AutoItem().AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text($"Fecha: {NexoApi.Common.ColombiaClock.Now:dd/MM/yyyy}").FontColor(Colors.Grey.Darken2);
                    c.Item().AlignRight().Text($"Total: {lista.Count} clientes").FontColor(Colors.Grey.Darken2);
                });
            });

            page.Content().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                });

                table.Header(h =>
                {
                    foreach (var t in new[] { "Nombre", "NIT", "Teléfono", "Email", "Ciudad", "Responsable", "Estado" })
                        h.Cell().Background(azul).Padding(4).Text(t).FontColor(Colors.White).Bold().FontSize(7.5f);
                });

                int idx = 0;
                foreach (var c in lista)
                {
                    string bg = idx++ % 2 == 0 ? Colors.White : "#F9FAFB";
                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(4).Text(c.Nombre).FontSize(7.5f);
                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(4).Text(c.NIT ?? "").FontSize(7.5f);
                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(4).Text(c.Telefono ?? "").FontSize(7.5f);
                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(4).Text(c.Email ?? "").FontSize(7.5f);
                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(4).Text(c.Ciudad ?? "").FontSize(7.5f);
                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(4).Text(c.Responsable ?? "").FontSize(7.5f);
                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(4).Text(c.Estado ? "Activo" : "Inactivo").FontSize(7.5f);
                }
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7).FontColor(Colors.Grey.Medium));
                t.Span($"Generado por NEXO ERP — {NexoApi.Common.ColombiaClock.Now:dd/MM/yyyy HH:mm}");
            });
        })).GeneratePdf();
    }

    public static byte[] GenerarExcelProveedores(IEnumerable<ProveedorItem> proveedores)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Proveedores");

        string[] headers = { "Razón Social", "NIT", "Tipo Persona", "Contacto", "Teléfono", "Email",
            "Dirección", "Ciudad", "Departamento", "País", "Estado" };
        EstilizarEncabezados(ws, headers);

        int row = 2;
        foreach (var p in proveedores)
        {
            ws.Cell(row, 1).Value  = p.RazonSocial;
            ws.Cell(row, 2).Value  = p.NIT ?? "";
            ws.Cell(row, 3).Value  = p.TipoPersona ?? "";
            ws.Cell(row, 4).Value  = p.Contacto ?? "";
            ws.Cell(row, 5).Value  = p.Telefono ?? "";
            ws.Cell(row, 6).Value  = p.Email ?? "";
            ws.Cell(row, 7).Value  = p.Direccion ?? "";
            ws.Cell(row, 8).Value  = p.Ciudad ?? "";
            ws.Cell(row, 9).Value  = p.Departamento ?? "";
            ws.Cell(row, 10).Value = p.Pais ?? "";
            ws.Cell(row, 11).Value = p.Estado ? "Activo" : "Inactivo";
            if (row % 2 == 0)
                ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F7FA");
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static byte[] GenerarPdfProveedores(IEnumerable<ProveedorItem> proveedores, string empresa)
    {
        const string azul = "#1E3A5F";
        var lista = proveedores.ToList();

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.MarginHorizontal(1.5f, Unit.Centimetre);
            page.MarginVertical(1.2f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(8f));

            page.Header().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(empresa).Bold().FontSize(14).FontColor(azul);
                    c.Item().Text("Listado de Proveedores").FontSize(10).FontColor(Colors.Grey.Darken1);
                });
                row.AutoItem().AlignRight().Column(c =>
                {
                    c.Item().AlignRight().Text($"Fecha: {NexoApi.Common.ColombiaClock.Now:dd/MM/yyyy}").FontColor(Colors.Grey.Darken2);
                    c.Item().AlignRight().Text($"Total: {lista.Count} proveedores").FontColor(Colors.Grey.Darken2);
                });
            });

            page.Content().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                });

                table.Header(h =>
                {
                    foreach (var t in new[] { "Razón Social", "NIT", "Teléfono", "Email", "Ciudad", "Estado" })
                        h.Cell().Background(azul).Padding(4).Text(t).FontColor(Colors.White).Bold().FontSize(7.5f);
                });

                int idx = 0;
                foreach (var p in lista)
                {
                    string bg = idx++ % 2 == 0 ? Colors.White : "#F9FAFB";
                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(4).Text(p.RazonSocial).FontSize(7.5f);
                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(4).Text(p.NIT ?? "").FontSize(7.5f);
                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(4).Text(p.Telefono ?? "").FontSize(7.5f);
                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(4).Text(p.Email ?? "").FontSize(7.5f);
                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(4).Text(p.Ciudad ?? "").FontSize(7.5f);
                    table.Cell().Background(bg).BorderBottom(0.5f).BorderColor("#E5E7EB").Padding(4).Text(p.Estado ? "Activo" : "Inactivo").FontSize(7.5f);
                }
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7).FontColor(Colors.Grey.Medium));
                t.Span($"Generado por NEXO ERP — {NexoApi.Common.ColombiaClock.Now:dd/MM/yyyy HH:mm}");
            });
        })).GeneratePdf();
    }

    private static void EstilizarEncabezados(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
        }
    }

    // ── Reportes dinámicos ──────────────────────────────────────────────────

    private static readonly Dictionary<string, string> _rptColLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SKU"] = "SKU", ["Articulo"] = "Artículo", ["NombreArticulo"] = "Artículo",
        ["Stock"] = "Stock", ["CostoPromedio"] = "Costo Prom.", ["ValorTotal"] = "Valor Total",
        ["PPublico"] = "Precio Público", ["PBodega"] = "P. Bodega", ["PCredito"] = "P. Crédito",
        ["MargenPct"] = "Margen %", ["Margen"] = "Margen",
        ["TipoMovimiento"] = "Tipo Movimiento", ["Entrada"] = "Entrada", ["Salida"] = "Salida",
        ["Saldo"] = "Saldo", ["Documento"] = "Documento", ["Observaciones"] = "Observaciones",
        ["Insumo"] = "Insumo", ["CantidadTeorica"] = "Cant. Teórica", ["CantidadReal"] = "Cant. Real",
        ["DesviacionPct"] = "Desviación %", ["Desviacion"] = "Desviación", ["Unidad"] = "Unidad",
        ["Total"] = "Total", ["Cantidad"] = "Cantidad", ["Subtotal"] = "Subtotal",
        ["TotalVentas"] = "Total Ventas", ["TotalFacturas"] = "Total Facturas",
        ["Promedio"] = "Promedio", ["Canal"] = "Canal",
        ["Dia"] = "Día", ["Semana"] = "Semana", ["Mes"] = "Mes", ["Hora"] = "Hora",
        ["Fecha"] = "Fecha", ["FechaMovimiento"] = "Fecha", ["FechaCreacion"] = "Fecha Creación",
        ["Cliente"] = "Cliente", ["NombreCliente"] = "Cliente",
        ["Vendedor"] = "Vendedor", ["Nombre"] = "Nombre", ["Referencia"] = "Referencia",
        ["StockMinimo"] = "Stock Mín.", ["PuntoReorden"] = "Punto Reorden", ["Estado"] = "Estado",
        ["OrdenProduccion"] = "OP", ["Producto"] = "Producto", ["CantidadProducida"] = "Cant. Producida",
    };

    private static string RptLabel(string col) =>
        _rptColLabels.TryGetValue(col, out var lbl) ? lbl : col;

    private static bool RptEsCurrency(string col) =>
        col.Contains("Valor", StringComparison.OrdinalIgnoreCase) ||
        col.Contains("Precio", StringComparison.OrdinalIgnoreCase) ||
        col.Contains("Costo", StringComparison.OrdinalIgnoreCase) ||
        col.Contains("Venta", StringComparison.OrdinalIgnoreCase) ||
        col.Contains("Subtotal", StringComparison.OrdinalIgnoreCase) ||
        col == "Total" || col == "Promedio";

    private static bool RptEsPct(string col) =>
        col.EndsWith("Pct", StringComparison.OrdinalIgnoreCase) ||
        col.Contains("Margen", StringComparison.OrdinalIgnoreCase) ||
        col.Contains("Desviacion", StringComparison.OrdinalIgnoreCase);

    public static byte[] GenerarExcelReporte(
        string nombre,
        DateTime? desde,
        DateTime? hasta,
        IList<Dictionary<string, object?>> filas)
    {
        const string navy    = "#1E3A5F";
        const string navyBg  = "#E8EDF5";

        using var wb = new XLWorkbook();
        var sheetName = nombre.Length > 31 ? nombre[..31] : nombre;
        var ws = wb.Worksheets.Add(sheetName);

        if (filas.Count == 0)
        {
            ws.Cell(1, 1).Value = "Sin datos para el período seleccionado.";
            using var ms0 = new MemoryStream();
            wb.SaveAs(ms0);
            return ms0.ToArray();
        }

        var headers   = filas[0].Keys.ToList();
        int colCount  = headers.Count;

        // Fila 1 — título
        ws.Cell(1, 1).Value = nombre;
        ws.Cell(1, 1).Style.Font.Bold              = true;
        ws.Cell(1, 1).Style.Font.FontSize          = 14;
        ws.Cell(1, 1).Style.Font.FontColor         = XLColor.White;
        ws.Cell(1, 1).Style.Fill.BackgroundColor   = XLColor.FromHtml(navy);
        ws.Cell(1, 1).Style.Alignment.Vertical     = XLAlignmentVerticalValues.Center;
        ws.Range(1, 1, 1, colCount).Merge();
        ws.Row(1).Height = 28;

        // Fila 2 — metadatos
        var rango = desde.HasValue || hasta.HasValue
            ? $"Período: {desde?.ToString("dd/MM/yyyy") ?? "inicio"} — {hasta?.ToString("dd/MM/yyyy") ?? "hoy"}   |   {filas.Count:N0} registros   |   Generado: {NexoApi.Common.ColombiaClock.Now:dd/MM/yyyy HH:mm}"
            : $"{filas.Count:N0} registros   |   Generado: {NexoApi.Common.ColombiaClock.Now:dd/MM/yyyy HH:mm}";
        ws.Cell(2, 1).Value = rango;
        ws.Cell(2, 1).Style.Font.FontSize          = 9;
        ws.Cell(2, 1).Style.Font.FontColor         = XLColor.FromHtml("#374151");
        ws.Cell(2, 1).Style.Fill.BackgroundColor   = XLColor.FromHtml(navyBg);
        ws.Range(2, 1, 2, colCount).Merge();
        ws.Row(2).Height = 16;

        // Fila 3 — encabezados de columnas
        for (int c = 0; c < colCount; c++)
        {
            var cell = ws.Cell(3, c + 1);
            cell.Value                              = RptLabel(headers[c]);
            cell.Style.Font.Bold                    = true;
            cell.Style.Font.FontSize                = 9;
            cell.Style.Font.FontColor               = XLColor.White;
            cell.Style.Fill.BackgroundColor         = XLColor.FromHtml(navy);
            cell.Style.Alignment.Horizontal         = XLAlignmentHorizontalValues.Center;
            cell.Style.Border.BottomBorder          = XLBorderStyleValues.Medium;
            cell.Style.Border.BottomBorderColor     = XLColor.FromHtml("#FFFFFF");
        }
        ws.Row(3).Height = 18;

        // Filas de datos
        for (int r = 0; r < filas.Count; r++)
        {
            var fila  = filas[r];
            string bg = r % 2 == 0 ? "#FFFFFF" : "#F8FAFC";

            for (int c = 0; c < colCount; c++)
            {
                var col  = headers[c];
                var val  = fila[col];
                var cell = ws.Cell(r + 4, c + 1);

                cell.Style.Border.OutsideBorder        = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor   = XLColor.FromHtml("#E2E8F0");
                cell.Style.Fill.BackgroundColor        = XLColor.FromHtml(bg);
                cell.Style.Font.FontSize               = 9;

                switch (val)
                {
                    case decimal d:
                        cell.Value = d;
                        cell.Style.NumberFormat.Format   = RptEsCurrency(col) ? "$#,##0" : (RptEsPct(col) ? "0.0" : "#,##0.00");
                        cell.Style.Alignment.Horizontal  = XLAlignmentHorizontalValues.Right;
                        if (d < 0) cell.Style.Font.FontColor = XLColor.FromHtml("#DC2626");
                        break;
                    case double dbl:
                        cell.Value = dbl;
                        cell.Style.NumberFormat.Format   = RptEsCurrency(col) ? "$#,##0" : (RptEsPct(col) ? "0.0" : "#,##0.00");
                        cell.Style.Alignment.Horizontal  = XLAlignmentHorizontalValues.Right;
                        if (dbl < 0) cell.Style.Font.FontColor = XLColor.FromHtml("#DC2626");
                        break;
                    case int i:
                        cell.Value = i;
                        cell.Style.NumberFormat.Format   = "#,##0";
                        cell.Style.Alignment.Horizontal  = XLAlignmentHorizontalValues.Right;
                        if (i < 0) cell.Style.Font.FontColor = XLColor.FromHtml("#DC2626");
                        break;
                    case long l:
                        cell.Value = l;
                        cell.Style.NumberFormat.Format   = "#,##0";
                        cell.Style.Alignment.Horizontal  = XLAlignmentHorizontalValues.Right;
                        if (l < 0) cell.Style.Font.FontColor = XLColor.FromHtml("#DC2626");
                        break;
                    case DateTime dt:
                        cell.Value = dt;
                        cell.Style.NumberFormat.Format = "dd/MM/yyyy";
                        break;
                    case null:
                        cell.Value = "";
                        break;
                    default:
                        cell.Value = val.ToString();
                        break;
                }
            }
        }

        ws.Columns().AdjustToContents();
        for (int c = 1; c <= colCount; c++)
            if (ws.Column(c).Width < 10) ws.Column(c).Width = 10;
        ws.SheetView.FreezeRows(3);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public static byte[] GenerarPdfReporte(
        string empresa,
        string nombre,
        string? descripcion,
        DateTime? desde,
        DateTime? hasta,
        IList<Dictionary<string, object?>> filas)
    {
        const string navy = "#1E3A5F";
        var headers = filas.Count > 0 ? filas[0].Keys.ToList() : new List<string>();
        bool landscape = headers.Count > 6;

        return Document.Create(container => container.Page(page =>
        {
            page.Size(landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
            page.MarginHorizontal(1.5f, Unit.Centimetre);
            page.MarginVertical(1.2f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(8.5f));

            page.Header().Column(hdr =>
            {
                hdr.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text(empresa).Bold().FontSize(15).FontColor(navy);
                        c.Item().Text(nombre).FontSize(11).FontColor(Colors.Grey.Darken2);
                        if (!string.IsNullOrWhiteSpace(descripcion))
                            c.Item().Text(descripcion).FontSize(8).FontColor(Colors.Grey.Medium);
                    });
                    row.AutoItem().AlignRight().Column(c =>
                    {
                        if (desde.HasValue || hasta.HasValue)
                        {
                            var r2 = $"{desde?.ToString("dd/MM/yyyy") ?? "inicio"} — {hasta?.ToString("dd/MM/yyyy") ?? "hoy"}";
                            c.Item().AlignRight().Text(r2).FontColor(Colors.Grey.Darken2).FontSize(9);
                        }
                        c.Item().AlignRight().Text($"{filas.Count:N0} registros").Bold().FontColor(navy).FontSize(9);
                    });
                });
                hdr.Item().PaddingTop(6).LineHorizontal(2).LineColor(navy);
            });

            page.Content().PaddingTop(10).Column(col =>
            {
                if (filas.Count == 0)
                {
                    col.Item().PaddingTop(20).AlignCenter()
                       .Text("Sin datos para el período seleccionado.")
                       .FontColor(Colors.Grey.Medium);
                    return;
                }

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        foreach (var _ in headers)
                            cols.RelativeColumn();
                    });

                    table.Header(h =>
                    {
                        foreach (var hdr in headers)
                        {
                            h.Cell().Background(navy).Padding(5).AlignCenter()
                             .Text(RptLabel(hdr))
                             .FontColor(Colors.White).Bold().FontSize(7.5f);
                        }
                    });

                    int idx = 0;
                    foreach (var fila in filas)
                    {
                        string bg = idx++ % 2 == 0 ? Colors.White : "#F8FAFC";
                        foreach (var hdr in headers)
                        {
                            var val    = fila[hdr];
                            bool isNum = val is decimal or double or int or long;
                            bool isNeg = isNum && Convert.ToDouble(val) < 0;

                            string text = val switch
                            {
                                null      => "",
                                decimal d => RptEsCurrency(hdr) ? $"${d:N0}" : (RptEsPct(hdr) ? $"{d:N1}%" : $"{d:N2}"),
                                double db => RptEsCurrency(hdr) ? $"${db:N0}" : (RptEsPct(hdr) ? $"{db:N1}%" : $"{db:N2}"),
                                int i     => $"{i:N0}",
                                long l    => $"{l:N0}",
                                DateTime dt => dt.ToString("dd/MM/yyyy"),
                                _         => val.ToString() ?? "",
                            };

                            var cellBase = table.Cell()
                                .Background(bg)
                                .BorderBottom(0.5f).BorderColor("#E2E8F0")
                                .Padding(4);

                            (isNum ? cellBase.AlignRight() : cellBase)
                                .Text(text).FontSize(7.5f)
                                .FontColor(isNeg ? Colors.Red.Darken1 : Colors.Black);
                        }
                    }
                });
            });

            page.Footer().AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(7.5f).FontColor(Colors.Grey.Medium));
                t.Span($"Generado por NEXO ERP — {NexoApi.Common.ColombiaClock.Now:dd/MM/yyyy HH:mm}   ·   Pág. ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        })).GeneratePdf();
    }
}
