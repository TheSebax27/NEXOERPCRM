using QRCoder;

namespace NexoApi.Common;

public static class QrHelper
{
    public static byte[] GenerarPng(string contenido, int pixelsPorModulo = 10)
    {
        using var gen  = new QRCodeGenerator();
        var data = gen.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.M);
        var png  = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPorModulo, [0, 0, 0, 255], [255, 255, 255, 255]);
    }
}
