using System.Globalization;
using System.Text;

namespace burroQueue.Infrastructure.Web;

public sealed class QrCodeSvgGenerator : IQrCodeSvgGenerator
{
    private static readonly byte[] _logTable = new byte[256];
    private static readonly byte[] _expTable = new byte[256];

    static QrCodeSvgGenerator()
    {
        int value = 1;
        for (int i = 0; i < 255; i++)
        {
            _expTable[i] = (byte)value;
            _logTable[value] = (byte)i;
            value <<= 1;
            if ((value & 0x100) != 0)
            {
                value ^= 0x11D; // Galois Field GF(256) Generator Polynomial
            }
        }
        _expTable[255] = _expTable[0];
    }

    public string GenerateSvg(string content, int moduleSize = 4)
    {
        if (moduleSize < 1)
        {
            moduleSize = 4;
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            int fallbackSize = moduleSize * 25;
            return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{fallbackSize.ToString(CultureInfo.InvariantCulture)}\" height=\"{fallbackSize.ToString(CultureInfo.InvariantCulture)}\" viewBox=\"0 0 {fallbackSize.ToString(CultureInfo.InvariantCulture)} {fallbackSize.ToString(CultureInfo.InvariantCulture)}\"><rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/><text x=\"50%\" y=\"50%\" dominant-baseline=\"middle\" text-anchor=\"middle\" fill=\"#000000\" font-size=\"10\">No URL</text></svg>";
        }

        bool[,] matrix = GenerateMatrix(content.Trim());
        int size = matrix.GetLength(0);
        int quietZone = 2;
        int totalModules = size + (quietZone * 2);
        int pixelSize = totalModules * moduleSize;

        StringBuilder sb = new();
        sb.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{pixelSize}\" height=\"{pixelSize}\" viewBox=\"0 0 {pixelSize} {pixelSize}\">");
        sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#ffffff\"/>");

        for (int r = 0; r < size; r++)
        {
            for (int c = 0; c < size; c++)
            {
                if (matrix[r, c])
                {
                    int x = (c + quietZone) * moduleSize;
                    int y = (r + quietZone) * moduleSize;
                    sb.Append(CultureInfo.InvariantCulture, $"<rect x=\"{x}\" y=\"{y}\" width=\"{moduleSize}\" height=\"{moduleSize}\" fill=\"#000000\"/>");
                }
            }
        }

        sb.Append("</svg>");
        return sb.ToString();
    }

    public bool[,] GenerateMatrix(string content)
    {
        byte[] textBytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
        int version = textBytes.Length <= 14 ? 1 : (textBytes.Length <= 28 ? 2 : 3);
        int size = 17 + (version * 4);
        bool[,] matrix = new bool[size, size];
        bool[,] reserved = new bool[size, size];

        // 1. Finder Patterns (7x7) + Separators
        PlaceFinderPattern(matrix, reserved, 0, 0);
        PlaceFinderPattern(matrix, reserved, size - 7, 0);
        PlaceFinderPattern(matrix, reserved, 0, size - 7);

        // 2. Timing Patterns
        for (int i = 8; i < size - 8; i++)
        {
            bool val = i % 2 == 0;
            matrix[6, i] = val;
            reserved[6, i] = true;
            matrix[i, 6] = val;
            reserved[i, 6] = true;
        }

        // 3. Alignment Pattern
        if (version >= 2)
        {
            int alignPos = version == 2 ? 18 : 22;
            PlaceAlignmentPattern(matrix, reserved, alignPos, alignPos);
        }

        // 4. Reserve Format Info Area
        for (int i = 0; i < 9; i++)
        {
            reserved[8, i] = true;
            reserved[i, 8] = true;
        }
        for (int i = size - 8; i < size; i++)
        {
            reserved[8, i] = true;
            reserved[i, 8] = true;
        }
        reserved[8, 8] = true;

        // 5. Build Data & Error Correction Codewords
        byte[] dataBits = EncodeData(textBytes, version);
        byte[] ecWords = CalculateErrorCorrection(dataBits, version);
        List<bool> allBits = [];

        foreach (byte b in dataBits)
        {
            for (int i = 7; i >= 0; i--)
            {
                allBits.Add(((b >> i) & 1) == 1);
            }
        }
        foreach (byte b in ecWords)
        {
            for (int i = 7; i >= 0; i--)
            {
                allBits.Add(((b >> i) & 1) == 1);
            }
        }

        // 6. Placement & Masking (Mask 0: (row + col) % 2 == 0)
        int bitIdx = 0;
        int row = size - 1;
        int col = size - 1;
        bool goingUp = true;

        while (col > 0)
        {
            if (col == 6)
            {
                col--;
            }

            int c = col;
            int r = row;

            for (int subCol = 0; subCol < 2; subCol++)
            {
                int currentC = c - subCol;
                if (!reserved[r, currentC])
                {
                    bool bit = bitIdx < allBits.Count && allBits[bitIdx++];
                    bool mask = (r + currentC) % 2 == 0;
                    matrix[r, currentC] = bit ^ mask;
                }
            }

            if (goingUp)
            {
                row--;
                if (row < 0)
                {
                    row = 0;
                    col -= 2;
                    goingUp = false;
                }
            }
            else
            {
                row++;
                if (row >= size)
                {
                    row = size - 1;
                    col -= 2;
                    goingUp = true;
                }
            }
        }

        // 7. Format Information
        ushort formatBits = 0x77C4;
        PlaceFormatInfo(matrix, formatBits, size);

        return matrix;
    }

    private static void PlaceFinderPattern(bool[,] matrix, bool[,] reserved, int startR, int startC)
    {
        for (int r = -1; r <= 7; r++)
        {
            for (int c = -1; c <= 7; c++)
            {
                int curR = startR + r;
                int curC = startC + c;
                if (curR >= 0 && curR < matrix.GetLength(0) && curC >= 0 && curC < matrix.GetLength(1))
                {
                    reserved[curR, curC] = true;
                    bool isBorder = r == 0 || r == 6 || c == 0 || c == 6;
                    bool isCenter = r >= 2 && r <= 4 && c >= 2 && c <= 4;
                    matrix[curR, curC] = (r >= 0 && r <= 6 && c >= 0 && c <= 6) && (isBorder || isCenter);
                }
            }
        }
    }

    private static void PlaceAlignmentPattern(bool[,] matrix, bool[,] reserved, int centerR, int centerC)
    {
        for (int r = -2; r <= 2; r++)
        {
            for (int c = -2; c <= 2; c++)
            {
                int curR = centerR + r;
                int curC = centerC + c;
                reserved[curR, curC] = true;
                bool isBorder = Math.Abs(r) == 2 || Math.Abs(c) == 2;
                bool isCenter = r == 0 && c == 0;
                matrix[curR, curC] = isBorder || isCenter;
            }
        }
    }

    private static void PlaceFormatInfo(bool[,] matrix, ushort formatBits, int size)
    {
        for (int i = 0; i < 15; i++)
        {
            bool bit = ((formatBits >> i) & 1) == 1;
            int r1;
            int c1;
            if (i < 6)
            {
                r1 = i;
                c1 = 8;
            }
            else if (i == 6)
            {
                r1 = 7;
                c1 = 8;
            }
            else if (i == 7)
            {
                r1 = 8;
                c1 = 8;
            }
            else if (i == 8)
            {
                r1 = 8;
                c1 = 7;
            }
            else
            {
                r1 = 8;
                c1 = 14 - i;
            }
            matrix[r1, c1] = bit;

            int r2 = i < 7 ? size - 1 - i : 8;
            int c2 = i < 7 ? 8 : size - 15 + i;
            matrix[r2, c2] = bit;
        }
        matrix[size - 8, 8] = true;
    }

    private static byte[] EncodeData(byte[] textBytes, int version)
    {
        int totalDataBytes = version == 1 ? 19 : (version == 2 ? 34 : 55);
        List<bool> bits = [];

        bits.Add(false);
        bits.Add(true);
        bits.Add(false);
        bits.Add(false);

        byte len = (byte)textBytes.Length;
        for (int i = 7; i >= 0; i--)
        {
            bits.Add(((len >> i) & 1) == 1);
        }

        foreach (byte b in textBytes)
        {
            for (int i = 7; i >= 0; i--)
            {
                bits.Add(((b >> i) & 1) == 1);
            }
        }

        for (int i = 0; i < 4 && bits.Count < totalDataBytes * 8; i++)
        {
            bits.Add(false);
        }

        while (bits.Count % 8 != 0)
        {
            bits.Add(false);
        }

        byte[] result = new byte[totalDataBytes];
        int byteIdx = 0;
        for (int i = 0; i < bits.Count; i += 8)
        {
            byte b = 0;
            for (int j = 0; j < 8; j++)
            {
                if (bits[i + j])
                {
                    b |= (byte)(1 << (7 - j));
                }
            }
            result[byteIdx++] = b;
        }

        byte pad = 0xEC;
        while (byteIdx < totalDataBytes)
        {
            result[byteIdx++] = pad;
            pad = (pad == 0xEC) ? (byte)0x11 : (byte)0xEC;
        }

        return result;
    }

    private static byte[] CalculateErrorCorrection(byte[] data, int version)
    {
        int ecCount = version == 1 ? 7 : (version == 2 ? 10 : 15);
        byte[] generator = GetGeneratorPolynomial(ecCount);
        byte[] result = new byte[ecCount];
        byte[] msg = new byte[data.Length + ecCount];
        Array.Copy(data, msg, data.Length);

        for (int i = 0; i < data.Length; i++)
        {
            byte factor = msg[i];
            if (factor != 0)
            {
                int factorLog = _logTable[factor];
                for (int j = 0; j < generator.Length; j++)
                {
                    msg[i + j] ^= _expTable[(generator[j] + factorLog) % 255];
                }
            }
        }

        Array.Copy(msg, data.Length, result, 0, ecCount);
        return result;
    }

    private static byte[] GetGeneratorPolynomial(int degree)
    {
        byte[] poly = [1];
        for (int i = 0; i < degree; i++)
        {
            byte[] next = new byte[poly.Length + 1];
            for (int j = 0; j < poly.Length; j++)
            {
                next[j] ^= poly[j];
                next[j + 1] ^= _expTable[(_logTable[poly[j]] + i) % 255];
            }
            poly = next;
        }
        return poly;
    }
}
