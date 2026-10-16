using Dapper;
using NexoApi.Common.Data;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace NexoApi.Features.Rrhh;

public class CarnetEmpleadoService(IDbConnectionFactory db)
{
    private const string Verde       = "#4a9a2f";
    private const string VerdeOscuro = "#2f7a1e";
    private const string GrisTexto   = "#1b1b1b";
    private const string GrisMuted   = "#666666";

    public async Task<byte[]> GenerarAsync(int empleadoId)
    {
        using var conn = db.CreateConnection();

        var emp = await conn.QuerySingleOrDefaultAsync(
            @"SELECT e.EmpleadoID,
                     e.Nombres, e.Apellidos,
                     ca.Nombre  AS Cargo,
                     dep.Nombre AS Departamento,
                     e.Telefono, e.Email,
                     e.FechaIngreso,
                     e.TipoSangre,
                     e.Foto, e.FotoContentType,
                     cfg.NombreEmpresa,
                     cfg.Logo        AS LogoEmpresa,
                     cfg.LogoContentType AS LogoContentType
              FROM Rrhh.Empleados e
              LEFT JOIN Rrhh.Cargos        ca  ON ca.CargoID        = e.CargoID
              LEFT JOIN Rrhh.Departamentos dep ON dep.DepartamentoID = ca.DepartamentoID
              LEFT JOIN (SELECT NombreEmpresa, Logo, LogoContentType
                         FROM Organizacion.ConfiguracionEmpresa
                         WHERE ConfiguracionID = 1) cfg ON 1 = 1
              WHERE e.EmpleadoID = @EmpleadoID",
            new { EmpleadoID = empleadoId })
        ?? throw new KeyNotFoundException($"Empleado {empleadoId} no encontrado.");

        string nombre       = $"{emp.Nombres} {emp.Apellidos}";
        string cargo        = (string?)emp.Cargo        ?? "—";
        string empresa      = (string?)emp.NombreEmpresa ?? "Mi Empresa";
        byte[]? foto        = (byte[]?)emp.Foto;
        byte[]? logoEmpresa = (byte[]?)emp.LogoEmpresa;
        string? tipoSangre  = (string?)emp.TipoSangre;
        DateTime? fechaIngreso = (DateTime?)emp.FechaIngreso;

        byte[] qrBytes = GenerarQr(empleadoId, nombre, cargo, empresa);

        return Document.Create(c => c.Page(page =>
        {
            // CR80: 85.6 × 54 mm
            page.Size(85.6f, 54f, Unit.Millimetre);
            page.Margin(0);
            page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(7).FontColor(GrisTexto));

            page.Content().Column(col =>
            {
                // ── Banner verde ─────────────────────────────────
                col.Item()
                   .Height(16, Unit.Millimetre)
                   .Background(Verde)
                   .Row(banner =>
                   {
                       // Izquierda: relleno
                       banner.RelativeItem();

                       // Derecha: logo o nombre empresa
                       banner.ConstantItem(38, Unit.Millimetre)
                             .AlignRight()
                             .PaddingRight(5).PaddingTop(3)
                             .Column(logo =>
                             {
                                 if (logoEmpresa is not null)
                                 {
                                     logo.Item()
                                         .Height(10, Unit.Millimetre)
                                         .AlignRight()
                                         .Image(logoEmpresa).FitHeight();
                                 }
                                 else
                                 {
                                     logo.Item()
                                         .PaddingTop(1)
                                         .Text(t =>
                                         {
                                             t.AlignRight();
                                             t.Span(empresa.ToUpperInvariant())
                                              .Bold().FontSize(8.5f).FontColor(Colors.White)
                                              .LetterSpacing(0.12f);
                                         });
                                 }
                             });
                   });

                // ── Cuerpo ──────────────────────────────────────
                col.Item()
                   .Padding(4)
                   .Row(body =>
                   {
                       // Foto
                       var fotoBox = body.ConstantItem(18, Unit.Millimetre)
                           .Height(19, Unit.Millimetre)
                           .Border(2).BorderColor(VerdeOscuro)
                           .Background(foto is null ? "#cfd2d4" : Colors.White);

                       if (foto is not null)
                           fotoBox.Image(foto).FitArea();
                       else
                           fotoBox.AlignCenter().AlignMiddle()
                                  .Text(nombre.Length > 0 ? nombre[0].ToString().ToUpper() : "?")
                                  .FontSize(20).Bold().FontColor(Colors.White);

                       // Nombre + cargo + datos
                       body.RelativeItem().PaddingLeft(4).Column(info =>
                       {
                           // Nombre
                           info.Item()
                               .Text(nombre)
                               .Bold().FontSize(9.5f).FontColor(GrisTexto);

                           // Cargo
                           info.Item().PaddingTop(1)
                               .Text(cargo)
                               .FontSize(7.5f).FontColor(VerdeOscuro);

                           info.Item().PaddingTop(4)
                               .LineHorizontal(0.4f).LineColor("#dddddd");

                           // Grid 2×2 de datos
                           info.Item().PaddingTop(4).Row(grid =>
                           {
                               grid.RelativeItem().Column(c1 =>
                               {
                                   Dato(c1, "ID", $"{empleadoId:D5}");
                                   Dato(c1, "Ingreso", fechaIngreso.HasValue
                                       ? fechaIngreso.Value.ToString("dd/MM/yyyy") : "—");
                               });

                               grid.RelativeItem().Column(c2 =>
                               {
                                   Dato(c2, "Tipo Sangre", tipoSangre ?? "—");
                                   Dato(c2, "Cargo", cargo.Length > 14 ? cargo[..14] + "…" : cargo);
                               });

                               // QR
                               grid.ConstantItem(14, Unit.Millimetre)
                                   .Height(14, Unit.Millimetre)
                                   .AlignBottom()
                                   .Image(qrBytes).FitArea();
                           });
                       });
                   });
            });
        })).GeneratePdf();
    }

    private static void Dato(ColumnDescriptor col, string label, string valor)
    {
        col.Item().PaddingBottom(3).Column(d =>
        {
            d.Item().Text(label).Bold().FontSize(5.5f).FontColor(GrisMuted);
            d.Item().Text(valor).FontSize(6.5f).FontColor(GrisTexto);
        });
    }

    private static byte[] GenerarQr(int empleadoId, string nombre, string cargo, string empresa)
    {
        var vCard = $"BEGIN:VCARD\r\nVERSION:3.0\r\nFN:{nombre}\r\nORG:{empresa}\r\nTITLE:{cargo}\r\nUID:EMP-{empleadoId:D5}\r\nEND:VCARD";
        using var gen = new QRCodeGenerator();
        var data = gen.CreateQrCode(vCard, QRCodeGenerator.ECCLevel.M);
        var png  = new PngByteQRCode(data);
        return png.GetGraphic(8, [0, 0, 0], [255, 255, 255]);
    }
}
