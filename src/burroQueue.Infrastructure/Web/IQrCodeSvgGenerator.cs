namespace burroQueue.Infrastructure.Web;

public interface IQrCodeSvgGenerator
{
    string GenerateSvg(string content, int moduleSize = 4);

    bool[,] GenerateMatrix(string content);
}
