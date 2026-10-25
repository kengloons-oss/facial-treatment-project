using QRCoder;
namespace Assignment.Services.QRCodeGeneratorHelper;

public class QRCodeGeneratorHelper : IQRCodeGeneratorHelper
{
    public byte[] GenerateQRCode(string text)
    {
        byte[] QRCode = new byte[0];

        if (!string.IsNullOrEmpty(text))
        {
            QRCodeGenerator qrCodeGenerator = new QRCodeGenerator();
            QRCodeData data = qrCodeGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            BitmapByteQRCode bitmap = new BitmapByteQRCode(data);
            QRCode = bitmap.GetGraphic(20);
        }
        return QRCode;
    }
}
