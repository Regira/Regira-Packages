/////////////////////////////////////////////////////////////////////
//
//	QR Code Encoder Library
//
//	QR Code encoder.
//
//	Author: Uzi Granot
//	Original Version: 1.0
//	Date: June 30, 2018
//	Copyright (C) 2018-2019 Uzi Granot. All Rights Reserved
//	For full version history please look at QREncoder.cs
//
//	QR Code Library C# class library and the attached test/demo
//  applications are free software.
//	Software developed by this author is licensed under CPOL 1.02.
//	Some portions of the QRCodeVideoDecoder are licensed under GNU Lesser
//	General Public License v3.0.
//
//	The solution is made of 3 projects:
//	1. QRCodeEncoderLibrary: QR code encoding.
//	2. QRCodeEncoderDemo: Create QR Code images.
//	3. QRCodeConsoleDemo: Demo app for net standard
//
//	The main points of CPOL 1.02 subject to the terms of the License are:
//
//	Source Code and Executable Files can be used in commercial applications;
//	Source Code and Executable Files can be redistributed; and
//	Source Code can be modified to create derivative works.
//	No claim of suitability, guarantee, or any warranty whatsoever is
//	provided. The software is provided "as-is".
//	The Article accompanying the Work may not be distributed or republished
//	without the Author's consent
//
/////////////////////////////////////////////////////////////////////
//
//	Version History:
//
//	Version 1.0 2018/06/30
//		Original revision
//
//	Version 1.1 2018/07/20
//		Consolidate DirectShowLib into one module removing unused code
//
//	Version 2.0 2019/05/15
//		Split the combined QRCode encoder and decoder to two solutions.
//		Add support for .net standard.
//		Add save image to png file without Bitmap class.
//	Version 2.1 2019/07/22
//		Add support for ECI Assignment Value
/////////////////////////////////////////////////////////////////////



// ReSharper disable once CheckNamespace
namespace QRCodeEncoderLibrary;

public class QRCodeEncoder : QREncoder
{
    private static readonly byte[] PngFileSignature = [137, (byte)'P', (byte)'N', (byte)'G', (byte)'\r', (byte)'\n', 26, (byte)'\n'
    ];

    private static readonly byte[] PngIendChunk = [0, 0, 0, 0, (byte)'I', (byte)'E', (byte)'N', (byte)'D', 0xae, 0x42, 0x60, 0x82
    ];

    /// <summary>
    /// Save QRCode image to PNG file
    /// </summary>
    /// <param name="fileName">PNG file name</param>
    public void SaveQRCodeToPngFile(string fileName)
    {
        // exceptions
        if (fileName == null)
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        if (!fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("SaveQRCodeToPngFile: fileName extension must be .png");
        }

        if (QRCodeMatrix == null)
        {
            throw new ApplicationException("QRCode must be encoded first");
        }

        // file name to stream
        using Stream outputStream = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.None);
        // save file
        SaveQRCodeToPngFile(outputStream);
    }

    /// <summary>
    /// Save QRCode image to PNG stream
    /// </summary>
    /// <param name="outputStream">PNG output stream</param>
    public void SaveQRCodeToPngFile(Stream outputStream)
    {
        if (QRCodeMatrix == null)
        {
            throw new ApplicationException("QRCode must be encoded first");
        }

        // header
        var header = BuildPngHeader();

        // barcode data
        var inputBuf = QRCodeMatrixToPng();

        // compress barcode data
        var outputBuf = PngImageData(inputBuf);

        // stream to binary writer
        var bw = new BinaryWriter(outputStream);

        // write signature
        bw.Write(PngFileSignature, 0, PngFileSignature.Length);

        // write header
        bw.Write(header, 0, header.Length);

        // write image data
        bw.Write(outputBuf, 0, outputBuf.Length);

        // write end of file
        bw.Write(PngIendChunk, 0, PngIendChunk.Length);

        // flush all buffers
        bw.Flush();
    }

    internal byte[] BuildPngHeader()
    {
        // header
        var header = new byte[25];

        // header length
        header[0] = 0;
        header[1] = 0;
        header[2] = 0;
        header[3] = 13;

        // header label
        header[4] = (byte)'I';
        header[5] = (byte)'H';
        header[6] = (byte)'D';
        header[7] = (byte)'R';

        // image width
        var imageDimension = QRCodeImageDimension;
        header[8] = (byte)(imageDimension >> 24);
        header[9] = (byte)(imageDimension >> 16);
        header[10] = (byte)(imageDimension >> 8);
        header[11] = (byte)imageDimension;

        // image height
        header[12] = (byte)(imageDimension >> 24);
        header[13] = (byte)(imageDimension >> 16);
        header[14] = (byte)(imageDimension >> 8);
        header[15] = (byte)imageDimension;

        // bit depth (1)
        header[16] = 1;

        // color type (grey)
        header[17] = 0;

        // Compression (deflate)
        header[18] = 0;

        // filtering (up)
        header[19] = 0; // 2;

        // interlace (none)
        header[20] = 0;

        // crc
        var crc = Crc32.Checksum(header, 4, 17);
        header[21] = (byte)(crc >> 24);
        header[22] = (byte)(crc >> 16);
        header[23] = (byte)(crc >> 8);
        header[24] = (byte)crc;

        // return header
        return header;
    }

    internal static byte[] PngImageData(byte[] inputBuf)
    {
        // output buffer is:
        // Png IDAT length 4 bytes
        // Png chunk type IDAT 4 bytes
        // Png chunk data made of:
        //		header 2 bytes
        //		compressed data DataLen bytes
        //		adler32 input buffer checksum 4 bytes
        // Png CRC 4 bytes
        // Total output buffer length is 18 + DataLen

        // compress image
        var outputBuf = ZLibCompression.Compress(inputBuf);

        // png chunk data length
        var pngDataLen = outputBuf.Length - 12;
        outputBuf[0] = (byte)(pngDataLen >> 24);
        outputBuf[1] = (byte)(pngDataLen >> 16);
        outputBuf[2] = (byte)(pngDataLen >> 8);
        outputBuf[3] = (byte)pngDataLen;

        // add IDAT
        outputBuf[4] = (byte)'I';
        outputBuf[5] = (byte)'D';
        outputBuf[6] = (byte)'A';
        outputBuf[7] = (byte)'T';

        // adler32 checksum
        var readAdler32 = Adler32.Checksum(inputBuf, 0, inputBuf.Length);

        // ZLib checksum is Adler32 write it big endian order, high byte first
        var adlerPtr = outputBuf.Length - 8;
        outputBuf[adlerPtr++] = (byte)(readAdler32 >> 24);
        outputBuf[adlerPtr++] = (byte)(readAdler32 >> 16);
        outputBuf[adlerPtr++] = (byte)(readAdler32 >> 8);
        outputBuf[adlerPtr] = (byte)readAdler32;

        // crc
        var crc = Crc32.Checksum(outputBuf, 4, outputBuf.Length - 8);
        var crcPtr = outputBuf.Length - 4;
        outputBuf[crcPtr++] = (byte)(crc >> 24);
        outputBuf[crcPtr++] = (byte)(crc >> 16);
        outputBuf[crcPtr++] = (byte)(crc >> 8);
        outputBuf[crcPtr] = (byte)crc;

        // successful exit
        return outputBuf;
    }

    // convert barcode matrix to PNG image format
    internal byte[] QRCodeMatrixToPng()
    {
        // image width and height
        var imageDimension = QRCodeImageDimension;

        // width in bytes including filter leading byte
        var pngWidth = (imageDimension + 7) / 8 + 1;

        // PNG image array
        // array is all zeros in other words it is black image
        var pngLength = pngWidth * imageDimension;
        var pngImage = new byte[pngLength];

        // first row is a quiet zone and it is all white (filter is 0 none)
        int pngPtr;
        for (pngPtr = 1; pngPtr < pngWidth; pngPtr++) pngImage[pngPtr] = 255;

        // additional quiet zone rows are the same as first line (filter is 2 up)
        var pngEnd = QuietZone * pngWidth;
        for (; pngPtr < pngEnd; pngPtr += pngWidth) pngImage[pngPtr] = 2;

        // convert result matrix to output matrix
        for (var matrixRow = 0; matrixRow < QRCodeDimension; matrixRow++)
        {
            // make next row all white (filter is 0 none)
            pngEnd = pngPtr + pngWidth;
            for (var pngCol = pngPtr + 1; pngCol < pngEnd; pngCol++) pngImage[pngCol] = 255;

            // add black to next row
            for (var matrixCol = 0; matrixCol < QRCodeDimension; matrixCol++)
            {
                // bar is white
                if (!QRCodeMatrix[matrixRow, matrixCol]) continue;

                var pixelCol = ModuleSize * matrixCol + QuietZone;
                var pixelEnd = pixelCol + ModuleSize;
                for (; pixelCol < pixelEnd; pixelCol++)
                {
                    pngImage[pngPtr + 1 + pixelCol / 8] &= (byte)~(1 << (7 - (pixelCol & 7)));
                }
            }

            // additional rows are the same as the one above (filter is 2 up)
            pngEnd = pngPtr + ModuleSize * pngWidth;
            for (pngPtr += pngWidth; pngPtr < pngEnd; pngPtr += pngWidth) pngImage[pngPtr] = 2;
        }

        // bottom quiet zone and it is all white (filter is 0 none)
        pngEnd = pngPtr + pngWidth;
        for (pngPtr++; pngPtr < pngEnd; pngPtr++) pngImage[pngPtr] = 255;

        // additional quiet zone rows are the same as first line (filter is 2 up)
        for (; pngPtr < pngLength; pngPtr += pngWidth) pngImage[pngPtr] = 2;

        return pngImage;
    }
}