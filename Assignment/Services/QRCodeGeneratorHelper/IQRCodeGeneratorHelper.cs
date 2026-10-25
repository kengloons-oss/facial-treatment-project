namespace Assignment.Services.QRCodeGeneratorHelper;

public interface IQRCodeGeneratorHelper
{
    byte[] GenerateQRCode(string text);
}
