// Helpers/QRCodeHelper.cs
using QRCoder;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public static class QRCodeHelper
{
    public static string GenerateQRCodeFromLink(string link, int pixelsPerModule = 20)
    {
        if (string.IsNullOrEmpty(link)) return null;

        using (var qrGenerator = new QRCodeGenerator())
        {
            var qrCodeData = qrGenerator.CreateQrCode(link, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new QRCode(qrCodeData);

            using (var qrCodeImage = qrCode.GetGraphic(pixelsPerModule, Color.Black, Color.White, true))
            using (var ms = new MemoryStream())
            {
                qrCodeImage.Save(ms, ImageFormat.Png);
                return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
            }
        }
    }
}