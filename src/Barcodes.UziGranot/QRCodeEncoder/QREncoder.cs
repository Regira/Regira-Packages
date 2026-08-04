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

using System.Text;

// ReSharper disable once CheckNamespace
namespace QRCodeEncoderLibrary;

/// <summary>
/// QR Code error correction code enumeration
/// </summary>
public enum ErrorCorrection
{
    /// <summary>
    /// Low (01)
    /// </summary>
    L,

    /// <summary>
    /// Medium (00)
    /// </summary>
    M,

    /// <summary>
    /// Medium-high (11)
    /// </summary>
    Q,

    /// <summary>
    /// High (10)
    /// </summary>
    H,
}

/// <summary>
/// QR Code encoding modes
/// </summary>
public enum EncodingMode
{
    /// <summary>
    /// Terminator
    /// </summary>
    Terminator,

    /// <summary>
    /// Numeric
    /// </summary>
    Numeric,

    /// <summary>
    /// Alpha numeric
    /// </summary>
    AlphaNumeric,

    /// <summary>
    /// Append
    /// </summary>
    Append,

    /// <summary>
    /// byte encoding
    /// </summary>
    Byte,

    /// <summary>
    /// FNC1 first
    /// </summary>
    Fnc1First,

    /// <summary>
    /// Unknown encoding constant
    /// </summary>
    Unknown6,

    /// <summary>
    /// ECI Assignment Value
    /// </summary>
    Eci,

    /// <summary>
    /// Kanji encoding (not implemented by this software)
    /// </summary>
    Kanji,

    /// <summary>
    /// FNC1 second
    /// </summary>
    Fnc1Second,

    /// <summary>
    /// Unknown encoding constant
    /// </summary>
    Unknown10,

    /// <summary>
    /// Unknown encoding constant
    /// </summary>
    Unknown11,

    /// <summary>
    /// Unknown encoding constant
    /// </summary>
    Unknown12,

    /// <summary>
    /// Unknown encoding constant
    /// </summary>
    Unknown13,

    /// <summary>
    /// Unknown encoding constant
    /// </summary>
    Unknown14,

    /// <summary>
    /// Unknown encoding constant
    /// </summary>
    Unknown15,
}

/// <summary>
/// QR Code Encoder class
/// </summary>
public class QREncoder : QREncoderTables
{
    /// <summary>
    /// Version number
    /// </summary>
    public const string VersionNumber = "Ver 2.1.0 - 2019-05-26";

    /// <summary>
    /// QR code matrix.
    /// </summary>
    public bool[,] QRCodeMatrix { get; internal set; }

    /// <summary>
    /// Gets QR Code matrix version
    /// </summary>
    public int QRCodeVersion { get; internal set; }

    /// <summary>
    /// Gets QR Code matrix dimension in bits
    /// </summary>
    public int QRCodeDimension { get; internal set; }

    /// <summary>
    /// Gets QR Code image dimension
    /// </summary>
    public int QRCodeImageDimension { get; internal set; }

    // internal variables
    internal byte[][] DataSegArray;
    internal int EncodedDataBits;
    internal int MaxCodewords;
    internal int MaxDataCodewords;
    internal int MaxDataBits;
    internal int ErrCorrCodewords;
    internal int BlocksGroup1;
    internal int DataCodewordsGroup1;
    internal int BlocksGroup2;
    internal int DataCodewordsGroup2;
    internal int MaskCode;
    internal EncodingMode[] EncodingSegMode;
    internal byte[] CodewordsArray;
    internal int CodewordsPtr;
    internal uint BitBuffer;
    internal int BitBufferLen;
    internal byte[,] BaseMatrix;
    internal byte[,] MaskMatrix;
    internal byte[,] ResultMatrix;

    /// <summary>
    /// QR Code error correction code (L, M, Q, H)
    /// </summary>
    public ErrorCorrection ErrorCorrection
    {
        get => _errorCorrection;
        set
        {
            // test error correction
            if (value < ErrorCorrection.L || value > ErrorCorrection.H)
                throw new ArgumentException("Error correction is invalid. Must be L, M, Q or H. Default is M");

            // save error correction level
            _errorCorrection = value;
        }
    }
    private ErrorCorrection _errorCorrection = ErrorCorrection.M;

    /// <summary>
    /// Module size (Default: 2)
    /// </summary>
    public int ModuleSize
    {
        get => _moduleSize;
        set
        {
            if (value < 1 || value > 100)
                throw new ArgumentException("Module size error. Default is 2.");
            _moduleSize = value;

            // quiet zone must be at least 4 times module size
            if (_quietZone < 4 * value) _quietZone = 4 * value;

            // recalculate image dimension
            QRCodeImageDimension = 2 * _quietZone + QRCodeDimension * _moduleSize;
        }
    }
    private int _moduleSize = 2;

    /// <summary>
    /// Quiet zone around the barcode in pixels (Default: 8)
    /// Must be at least 4 times module size
    /// </summary>
    public int QuietZone
    {
        get => _quietZone;
        set
        {
            if (value < 4 * _moduleSize || value > 400)
                throw new ArgumentException("Quiet zone must be at least 4 times the module size. Default is 8.");
            _quietZone = value;

            // recalculate image dimension
            QRCodeImageDimension = 2 * _quietZone + QRCodeDimension * _moduleSize;
        }
    }
    private int _quietZone = 8;

    /// <summary>
    /// ECI Assignment Value
    /// </summary>
    public int EciAssignValue
    {
        get => _eciAssignValue;
        set
        {
            if (value < -1 || value > 999999)
            {
                throw new ArgumentException("ECI Assignment Value must be 0-999999 or -1 for none");
            }
            _eciAssignValue = value;
        }
    }
    private int _eciAssignValue = -1;

    /// <summary>
    /// Encode one string into QRCode boolean matrix
    /// </summary>
    /// <param name="stringDataSegment">string data segment</param>
    public void Encode(string stringDataSegment)
    {
        // empty
        if (string.IsNullOrEmpty(stringDataSegment))
        {
            throw new ArgumentNullException(nameof(stringDataSegment));
        }

        // convert string to byte array
        var binaryData = Encoding.UTF8.GetBytes(stringDataSegment);

        // encode data
        Encode([binaryData]);
    }

    /// <summary>
    /// Encode array of strings into QRCode boolean matrix
    /// </summary>
    /// <param name="stringDataSegments">string data segments</param>
    public void Encode(string[] stringDataSegments)
    {
        // empty
        if (stringDataSegments == null || stringDataSegments.Length == 0)
        {
            throw new ArgumentNullException(nameof(stringDataSegments));
        }

        // loop for all segments
        foreach (var segment in stringDataSegments)
        {
            // convert string to byte array
            if (segment == null)
            {
                throw new ArgumentNullException(nameof(segment), "One of the string data segments is null or empty");
            }
        }

        // create bytes arrays
        var tempDataSegArray = new byte[stringDataSegments.Length][];

        // loop for all segments
        for (var segIndex = 0; segIndex < stringDataSegments.Length; segIndex++)
        {
            // convert string to byte array
            tempDataSegArray[segIndex] = Encoding.UTF8.GetBytes(stringDataSegments[segIndex]);
        }

        // convert string to byte array
        Encode(tempDataSegArray);
    }

    /// <summary>
    /// Encode one data segment into QRCode boolean matrix
    /// </summary>
    /// <param name="singleDataSeg">Data segment byte array</param>
    /// <returns>QR Code boolean matrix</returns>
    public void Encode(byte[] singleDataSeg)
    {
        // test data segments array
        if (singleDataSeg == null || singleDataSeg.Length == 0)
        {
            throw new ArgumentNullException(nameof(singleDataSeg));
        }

        // encode data
        Encode([singleDataSeg]);
    }

    /// <summary>
    /// Encode data segments array into QRCode boolean matrix
    /// </summary>
    /// <param name="dataSegArray">Data array of byte arrays</param>
    /// <returns>QR Code boolean matrix</returns>
    public void Encode(byte[][] dataSegArray)
    {
        // test data segments array
        if (dataSegArray == null || dataSegArray.Length == 0)
        {
            throw new ArgumentNullException(nameof(dataSegArray));
        }

        // reset result variables
        QRCodeMatrix = null;
        QRCodeVersion = 0;
        QRCodeDimension = 0;
        QRCodeImageDimension = 0;

        // loop for all segments
        var bytes = 0;
        for (var segIndex = 0; segIndex < dataSegArray.Length; segIndex++)
        {
            // input string length
            var dataSeg = dataSegArray[segIndex];
            if (dataSeg == null)
            {
                dataSegArray[segIndex] = [];
            }
            else
            {
                bytes += dataSeg.Length;
            }
        }
        if (bytes == 0)
        {
            throw new ArgumentException("There is no data to encode.");
        }

        // save data segments array
        DataSegArray = dataSegArray;

        // initialization
        Initialization();

        // encode data
        EncodeData();

        // calculate error correction
        CalculateErrorCorrection();

        // interleave data and error correction codewords
        InterleaveBlocks();

        // build base matrix
        BuildBaseMatrix();

        // load base matrix with data and error correction codewords
        LoadMatrixWithData();

        // data masking
        SelectBastMask();

        // add format information (error code level and mask code)
        AddFormatInformation();

        // output matrix each element is one module
        QRCodeMatrix = new bool[QRCodeDimension, QRCodeDimension];

        // convert result matrix to output matrix
        // Black=true, White=false
        for (var row = 0; row < QRCodeDimension; row++)
        {
            for (var col = 0; col < QRCodeDimension; col++)
            {
                if ((ResultMatrix[row, col] & 1) != 0)
                {
                    QRCodeMatrix[row, col] = true;
                }
            }
        }

        // exit
    }

    /// <summary>
    /// convert black and white matrix to black and white image
    /// </summary>
    /// <returns>Black and white image in pixels</returns>
    public bool[,] ConvertQRCodeMatrixToPixels()
    {
        if (QRCodeMatrix == null)
        {
            throw new ApplicationException("QRCode must be encoded first");
        }

        // output matrix size in pixels all matrix elements are white (false)
        var imageDimension = QRCodeImageDimension;
        var bwImage = new bool[imageDimension, imageDimension];

        var xOffset = _quietZone;
        var yOffset = _quietZone;

        // convert result matrix to output matrix
        for (var row = 0; row < QRCodeDimension; row++)
        {
            for (var col = 0; col < QRCodeDimension; col++)
            {
                // bar is black
                if (QRCodeMatrix[row, col])
                {
                    for (var y = 0; y < ModuleSize; y++)
                    {
                        for (var x = 0; x < ModuleSize; x++) bwImage[yOffset + y, xOffset + x] = true;
                    }
                }
                xOffset += ModuleSize;
            }
            xOffset = _quietZone;
            yOffset += ModuleSize;
        }
        return bwImage;
    }


    ////////////////////////////////////////////////////////////////////
    // Initialization
    ////////////////////////////////////////////////////////////////////

    internal void Initialization()
    {
        // create encoding mode array
        EncodingSegMode = new EncodingMode[DataSegArray.Length];

        // reset total encoded data bits
        EncodedDataBits = 0;

        // test for ECI
        if (_eciAssignValue >= 0)
        {
            if (_eciAssignValue <= 127)
            {
                EncodedDataBits = 12;
            }
            else if (_eciAssignValue <= 16383)
            {
                EncodedDataBits = 20;
            }
            else
            {
                EncodedDataBits = 28;
            }
        }

        // loop for all segments
        for (var segIndex = 0; segIndex < DataSegArray.Length; segIndex++)
        {
            // input string length
            var dataSeg = DataSegArray[segIndex];
            var dataLength = dataSeg.Length;

            // find encoding mode
            var encodingMode = EncodingMode.Numeric;
            for (var index = 0; index < dataLength; index++)
            {
                int code = EncodingTable[dataSeg[index]];
                if (code < 10)
                {
                    continue;
                }

                if (code < 45)
                {
                    encodingMode = EncodingMode.AlphaNumeric;
                    continue;
                }
                encodingMode = EncodingMode.Byte;
                break;
            }

            // calculate required bit length
            var dataBits = 4;
            switch (encodingMode)
            {
                case EncodingMode.Numeric:
                    dataBits += 10 * (dataLength / 3);
                    if (dataLength % 3 == 1) dataBits += 4;
                    else if (dataLength % 3 == 2) dataBits += 7;
                    break;

                case EncodingMode.AlphaNumeric:
                    dataBits += 11 * (dataLength / 2);
                    if ((dataLength & 1) != 0) dataBits += 6;
                    break;

                case EncodingMode.Byte:
                    dataBits += 8 * dataLength;
                    break;
            }

            EncodingSegMode[segIndex] = encodingMode;
            EncodedDataBits += dataBits;
        }

        // find best version
        var totalDataLenBits = 0;
        for (QRCodeVersion = 1; QRCodeVersion <= 40; QRCodeVersion++)
        {
            // number of bits on each side of the QR code square
            QRCodeDimension = 17 + 4 * QRCodeVersion;
            QRCodeImageDimension = 2 * _quietZone + QRCodeDimension * _moduleSize;

            SetDataCodewordsLength();
            totalDataLenBits = 0;
            for (var seg = 0; seg < EncodingSegMode.Length; seg++)
            {
                totalDataLenBits += DataLengthBits(EncodingSegMode[seg]);
            }

            if (EncodedDataBits + totalDataLenBits <= MaxDataBits)
            {
                break;
            }
        }

        if (QRCodeVersion > 40) throw new ApplicationException("Input data string is too long");
        EncodedDataBits += totalDataLenBits;
    }

    ////////////////////////////////////////////////////////////////////
    // QRCode: Convert data to bit array
    ////////////////////////////////////////////////////////////////////
    internal void EncodeData()
    {
        // codewords array
        CodewordsArray = new byte[MaxCodewords];

        // reset encoding members
        CodewordsPtr = 0;
        BitBuffer = 0;
        BitBufferLen = 0;

        // ECI
        if (_eciAssignValue >= 0)
        {
            // first 4 bits is mode indicator
            // ECI mode indicator is 0111,
            SaveBitsToCodewordsArray(7, 4);

            // save value
            if (_eciAssignValue <= 127)
            {
                SaveBitsToCodewordsArray(_eciAssignValue, 8);
            }
            else if (_eciAssignValue <= 16383)
            {
                SaveBitsToCodewordsArray((_eciAssignValue >> 8) | 0x80, 8);
                SaveBitsToCodewordsArray(_eciAssignValue & 0xff, 8);
            }
            else
            {
                SaveBitsToCodewordsArray((_eciAssignValue >> 16) | 0xc0, 8);
                SaveBitsToCodewordsArray((_eciAssignValue >> 8) & 0xff, 8);
                SaveBitsToCodewordsArray(_eciAssignValue & 0xff, 8);
            }
        }

        // loop for all segments
        for (var segIndex = 0; segIndex < DataSegArray.Length; segIndex++)
        {
            // input string length
            var dataSeg = DataSegArray[segIndex];
            var dataLength = dataSeg.Length;

            // first 4 bits is mode indicator
            // numeric code indicator is 0001, alpha numeric 0010, byte 0100
            SaveBitsToCodewordsArray((int)EncodingSegMode[segIndex], 4);

            // character count
            SaveBitsToCodewordsArray(dataLength, DataLengthBits(EncodingSegMode[segIndex]));

            // switch based on encode mode
            switch (EncodingSegMode[segIndex])
            {
                // numeric mode
                case EncodingMode.Numeric:
                    // encode digits in groups of 3
                    var numEnd = dataLength / 3 * 3;
                    for (var index = 0; index < numEnd; index += 3) SaveBitsToCodewordsArray(
                        100 * EncodingTable[dataSeg[index]] +
                        10 * EncodingTable[dataSeg[index + 1]] +
                        EncodingTable[dataSeg[index + 2]], 10);

                    // we have one digit remaining
                    if (dataLength - numEnd == 1) SaveBitsToCodewordsArray(EncodingTable[dataSeg[numEnd]], 4);

                    // we have two digits remaining
                    else if (dataLength - numEnd == 2) SaveBitsToCodewordsArray(10 * EncodingTable[dataSeg[numEnd]] +
                        EncodingTable[dataSeg[numEnd + 1]], 7);
                    break;

                // alphanumeric mode
                case EncodingMode.AlphaNumeric:
                    // encode digits in groups of 2
                    var alphaNumEnd = dataLength / 2 * 2;
                    for (var index = 0; index < alphaNumEnd; index += 2)
                        SaveBitsToCodewordsArray(45 * EncodingTable[dataSeg[index]] + EncodingTable[dataSeg[index + 1]], 11);

                    // we have one character remaining
                    if (dataLength - alphaNumEnd == 1) SaveBitsToCodewordsArray(EncodingTable[dataSeg[alphaNumEnd]], 6);
                    break;


                // byte mode					
                case EncodingMode.Byte:
                    // append the data after mode and character count
                    for (var index = 0; index < dataLength; index++) SaveBitsToCodewordsArray(dataSeg[index], 8);
                    break;
            }
        }

        // set terminator
        if (EncodedDataBits < MaxDataBits)
        {
            SaveBitsToCodewordsArray(0, MaxDataBits - EncodedDataBits < 4 ? MaxDataBits - EncodedDataBits : 4);
        }

        // flush bit buffer
        if (BitBufferLen > 0)
        {
            CodewordsArray[CodewordsPtr++] = (byte)(BitBuffer >> 24);
        }

        // add extra padding if there is still space
        var padEnd = MaxDataCodewords - CodewordsPtr;
        for (var padPtr = 0; padPtr < padEnd; padPtr++)
        {
            CodewordsArray[CodewordsPtr + padPtr] = (byte)((padPtr & 1) == 0 ? 0xEC : 0x11);
        }

        // exit
    }

    ////////////////////////////////////////////////////////////////////
    // Save data to codeword array
    ////////////////////////////////////////////////////////////////////
    internal void SaveBitsToCodewordsArray(int data, int bits)
    {
        BitBuffer |= (uint)data << (32 - BitBufferLen - bits);
        BitBufferLen += bits;
        while (BitBufferLen >= 8)
        {
            CodewordsArray[CodewordsPtr++] = (byte)(BitBuffer >> 24);
            BitBuffer <<= 8;
            BitBufferLen -= 8;
        }
    }

    ////////////////////////////////////////////////////////////////////
    // Calculate Error Correction
    ////////////////////////////////////////////////////////////////////
    internal void CalculateErrorCorrection()
    {
        // set generator polynomial array
        var generator = GenArray[ErrCorrCodewords - 7];

        // error correcion calculation buffer
        var bufSize = Math.Max(DataCodewordsGroup1, DataCodewordsGroup2) + ErrCorrCodewords;
        var errCorrBuff = new byte[bufSize];

        // initial number of data codewords
        var dataCodewords = DataCodewordsGroup1;
        var buffLen = dataCodewords + ErrCorrCodewords;

        // codewords pointer
        var dataCodewordsPtr = 0;

        // codewords buffer error correction pointer
        var codewordsArrayErrCorrPtr = MaxDataCodewords;

        // loop one block at a time
        var totalBlocks = BlocksGroup1 + BlocksGroup2;
        for (var blockNumber = 0; blockNumber < totalBlocks; blockNumber++)
        {
            // switch to group2 data codewords
            if (blockNumber == BlocksGroup1)
            {
                dataCodewords = DataCodewordsGroup2;
                buffLen = dataCodewords + ErrCorrCodewords;
            }

            // copy next block of codewords to the buffer and clear the remaining part
            Array.Copy(CodewordsArray, dataCodewordsPtr, errCorrBuff, 0, dataCodewords);
            Array.Clear(errCorrBuff, dataCodewords, ErrCorrCodewords);

            // update codewords array to next buffer
            dataCodewordsPtr += dataCodewords;

            // error correction polynomial division
            PolynominalDivision(errCorrBuff, buffLen, generator, ErrCorrCodewords);

            // save error correction block			
            Array.Copy(errCorrBuff, dataCodewords, CodewordsArray, codewordsArrayErrCorrPtr, ErrCorrCodewords);
            codewordsArrayErrCorrPtr += ErrCorrCodewords;
        }
    }

    ////////////////////////////////////////////////////////////////////
    // Polynomial division for error correction
    ////////////////////////////////////////////////////////////////////

    internal static void PolynominalDivision(byte[] polynomial, int polyLength, byte[] generator, int errCorrCodewords)
    {
        var dataCodewords = polyLength - errCorrCodewords;

        // error correction polynomial division
        for (var index = 0; index < dataCodewords; index++)
        {
            // current first codeword is zero
            if (polynomial[index] == 0)
            {
                continue;
            }

            // current first codeword is not zero
            int multiplier = IntToExp[polynomial[index]];

            // loop for error correction coofficients
            for (var generatorIndex = 0; generatorIndex < errCorrCodewords; generatorIndex++)
            {
                polynomial[index + 1 + generatorIndex] = (byte)(polynomial[index + 1 + generatorIndex] ^ ExpToInt[generator[generatorIndex] + multiplier]);
            }
        }
    }

    ////////////////////////////////////////////////////////////////////
    // Interleave data and error correction blocks
    ////////////////////////////////////////////////////////////////////
    internal void InterleaveBlocks()
    {
        // allocate temp codewords array
        var tempArray = new byte[MaxCodewords];

        // total blocks
        var totalBlocks = BlocksGroup1 + BlocksGroup2;

        // create array of data blocks starting point
        var start = new int[totalBlocks];
        for (var index = 1; index < totalBlocks; index++) start[index] = start[index - 1] + (index <= BlocksGroup1 ? DataCodewordsGroup1 : DataCodewordsGroup2);

        // step one. iterleave base on group one length
        var ptrEnd = DataCodewordsGroup1 * totalBlocks;

        // iterleave group one and two
        int ptr;
        var block = 0;
        for (ptr = 0; ptr < ptrEnd; ptr++)
        {
            tempArray[ptr] = CodewordsArray[start[block]];
            start[block]++;
            block++;
            if (block == totalBlocks)
            {
                block = 0;
            }
        }

        // interleave group two
        if (DataCodewordsGroup2 > DataCodewordsGroup1)
        {
            // step one. iterleave base on group one length
            ptrEnd = MaxDataCodewords;

            block = BlocksGroup1;
            for (; ptr < ptrEnd; ptr++)
            {
                tempArray[ptr] = CodewordsArray[start[block]];
                start[block]++;
                block++;
                if (block == totalBlocks)
                {
                    block = BlocksGroup1;
                }
            }
        }

        // create array of error correction blocks starting point
        start[0] = MaxDataCodewords;
        for (var index = 1; index < totalBlocks; index++)
        {
            start[index] = start[index - 1] + ErrCorrCodewords;
        }

        // step one. iterleave base on group one length

        // iterleave all groups
        ptrEnd = MaxCodewords;
        block = 0;
        for (; ptr < ptrEnd; ptr++)
        {
            tempArray[ptr] = CodewordsArray[start[block]];
            start[block]++;
            block++;
            if (block == totalBlocks)
            {
                block = 0;
            }
        }

        // save result
        CodewordsArray = tempArray;
    }

    ////////////////////////////////////////////////////////////////////
    // Load base matrix with data and error correction codewords
    ////////////////////////////////////////////////////////////////////
    internal void LoadMatrixWithData()
    {
        // input array pointer initialization
        var ptr = 0;
        var ptrEnd = 8 * MaxCodewords;

        // bottom right corner of output matrix
        var row = QRCodeDimension - 1;
        var col = QRCodeDimension - 1;

        // step state
        var state = 0;
        for (; ; )
        {
            // current module is data
            if ((BaseMatrix[row, col] & NonData) == 0)
            {
                // load current module with
                if ((CodewordsArray[ptr >> 3] & (1 << (7 - (ptr & 7)))) != 0)
                {
                    BaseMatrix[row, col] = DataBlack;
                }

                if (++ptr == ptrEnd)
                {
                    break;
                }
            }

            // current module is non data and vertical timing line condition is on
            else if (col == 6) col--;

            // update matrix position to next module
            switch (state)
            {
                // going up: step one to the left
                case 0:
                    col--;
                    state = 1;
                    continue;

                // going up: step one row up and one column to the right
                case 1:
                    col++;
                    row--;
                    // we are not at the top, go to state 0
                    if (row >= 0)
                    {
                        state = 0;
                        continue;
                    }
                    // we are at the top, step two columns to the left and start going down
                    col -= 2;
                    row = 0;
                    state = 2;
                    continue;

                // going down: step one to the left
                case 2:
                    col--;
                    state = 3;
                    continue;

                // going down: step one row down and one column to the right
                case 3:
                    col++;
                    row++;
                    // we are not at the bottom, go to state 2
                    if (row < QRCodeDimension)
                    {
                        state = 2;
                        continue;
                    }
                    // we are at the bottom, step two columns to the left and start going up
                    col -= 2;
                    row = QRCodeDimension - 1;
                    state = 0;
                    continue;
            }
        }
    }

    ////////////////////////////////////////////////////////////////////
    // Select Mask
    ////////////////////////////////////////////////////////////////////
    internal void SelectBastMask()
    {
        var bestScore = int.MaxValue;
        MaskCode = 0;

        for (var testMask = 0; testMask < 8; testMask++)
        {
            // apply mask
            ApplyMask(testMask);

            // evaluate 4 test conditions
            var score = EvaluationCondition1();
            if (score >= bestScore)
            {
                continue;
            }

            score += EvaluationCondition2();
            if (score >= bestScore)
            {
                continue;
            }

            score += EvaluationCondition3();
            if (score >= bestScore)
            {
                continue;
            }

            score += EvaluationCondition4();
            if (score >= bestScore)
            {
                continue;
            }

            // save as best mask so far
            ResultMatrix = MaskMatrix;
            MaskMatrix = null;
            bestScore = score;
            MaskCode = testMask;
        }
    }

    ////////////////////////////////////////////////////////////////////
    // Evaluation condition #1
    // 5 consecutive or more modules of the same color
    ////////////////////////////////////////////////////////////////////
    internal int EvaluationCondition1()
    {
        var score = 0;

        // test rows
        for (var row = 0; row < QRCodeDimension; row++)
        {
            var count = 1;
            for (var col = 1; col < QRCodeDimension; col++)
            {
                // current cell is not the same color as the one before
                if (((MaskMatrix[row, col - 1] ^ MaskMatrix[row, col]) & 1) != 0)
                {
                    if (count >= 5)
                    {
                        score += count - 2;
                    }

                    count = 0;
                }
                count++;
            }

            // last run
            if (count >= 5)
            {
                score += count - 2;
            }
        }

        // test columns
        for (var col = 0; col < QRCodeDimension; col++)
        {
            var count = 1;
            for (var row = 1; row < QRCodeDimension; row++)
            {
                // current cell is not the same color as the one before
                if (((MaskMatrix[row - 1, col] ^ MaskMatrix[row, col]) & 1) != 0)
                {
                    if (count >= 5)
                    {
                        score += count - 2;
                    }

                    count = 0;
                }
                count++;
            }

            // last run
            if (count >= 5)
            {
                score += count - 2;
            }
        }
        return score;
    }

    ////////////////////////////////////////////////////////////////////
    // Evaluation condition #2
    // same color in 2 by 2 area
    ////////////////////////////////////////////////////////////////////
    internal int EvaluationCondition2()
    {
        var score = 0;
        // test rows
        for (var row = 1; row < QRCodeDimension; row++)
        {
            for (var col = 1; col < QRCodeDimension; col++)
            {
                // all are black
                if ((MaskMatrix[row - 1, col - 1] & MaskMatrix[row - 1, col] & MaskMatrix[row, col - 1] & MaskMatrix[row, col] & 1) != 0)
                {
                    score += 3;
                }

                // all are white
                else if (((MaskMatrix[row - 1, col - 1] | MaskMatrix[row - 1, col] | MaskMatrix[row, col - 1] | MaskMatrix[row, col]) & 1) == 0)
                {
                    score += 3;
                }
            }
        }

        return score;
    }

    ////////////////////////////////////////////////////////////////////
    // Evaluation condition #3
    // pattern dark, light, dark, dark, dark, light, dark
    // before or after 4 light modules
    ////////////////////////////////////////////////////////////////////
    internal int EvaluationCondition3()
    {
        var score = 0;

        // test rows
        for (var row = 0; row < QRCodeDimension; row++)
        {
            var start = 0;

            // look for a lignt run at least 4 modules
            for (var col = 0; col < QRCodeDimension; col++)
            {
                // current cell is white
                if ((MaskMatrix[row, col] & 1) == 0) continue;

                // more or equal to 4
                if (col - start >= 4)
                {
                    // we have 4 or more white
                    // test for pattern before the white space
                    if (start >= 7 && TestHorizontalDarkLight(row, start - 7))
                    {
                        score += 40;
                    }

                    // test for pattern after the white space
                    if (QRCodeDimension - col >= 7 && TestHorizontalDarkLight(row, col))
                    {
                        score += 40;
                        col += 6;
                    }
                }

                // assume next one is white
                start = col + 1;
            }

            // last run
            if (QRCodeDimension - start >= 4 && start >= 7 && TestHorizontalDarkLight(row, start - 7))
            {
                score += 40;
            }
        }

        // test columns
        for (var col = 0; col < QRCodeDimension; col++)
        {
            var start = 0;

            // look for a lignt run at least 4 modules
            for (var row = 0; row < QRCodeDimension; row++)
            {
                // current cell is white
                if ((MaskMatrix[row, col] & 1) == 0) continue;

                // more or equal to 4
                if (row - start >= 4)
                {
                    // we have 4 or more white
                    // test for pattern before the white space
                    if (start >= 7 && TestVerticalDarkLight(start - 7, col))
                    {
                        score += 40;
                    }

                    // test for pattern after the white space
                    if (QRCodeDimension - row >= 7 && TestVerticalDarkLight(row, col))
                    {
                        score += 40;
                        row += 6;
                    }
                }

                // assume next one is white
                start = row + 1;
            }

            // last run
            if (QRCodeDimension - start >= 4 && start >= 7 && TestVerticalDarkLight(start - 7, col))
            {
                score += 40;
            }
        }

        // exit
        return score;
    }

    ////////////////////////////////////////////////////////////////////
    // Evaluation condition #4
    // blak to white ratio
    ////////////////////////////////////////////////////////////////////

    internal int EvaluationCondition4()
    {
        // count black cells
        var black = 0;
        for (var row = 0; row < QRCodeDimension; row++)
        {
            for (var col = 0; col < QRCodeDimension; col++) if ((MaskMatrix[row, col] & 1) != 0) black++;
        }

        // ratio
        var ratio = black / (double)(QRCodeDimension * QRCodeDimension);

        // there are more black than white
        if (ratio > 0.55)
        {
            return (int)(20.0 * (ratio - 0.5)) * 10;
        }

        if (ratio < 0.45)
        {
            return (int)(20.0 * (0.5 - ratio)) * 10;
        }

        return 0;
    }

    ////////////////////////////////////////////////////////////////////
    // Test horizontal dark light pattern
    ////////////////////////////////////////////////////////////////////
    internal bool TestHorizontalDarkLight(int row, int col)
    {
        return (MaskMatrix[row, col] & ~MaskMatrix[row, col + 1] & MaskMatrix[row, col + 2] & MaskMatrix[row, col + 3]
                & MaskMatrix[row, col + 4] & ~MaskMatrix[row, col + 5] & MaskMatrix[row, col + 6] & 1) != 0;
    }

    ////////////////////////////////////////////////////////////////////
    // Test vertical dark light pattern
    ////////////////////////////////////////////////////////////////////
    internal bool TestVerticalDarkLight(int row, int col)
    {
        return (MaskMatrix[row, col] & ~MaskMatrix[row + 1, col] & MaskMatrix[row + 2, col] & MaskMatrix[row + 3, col]
                & MaskMatrix[row + 4, col] & ~MaskMatrix[row + 5, col] & MaskMatrix[row + 6, col] & 1) != 0;
    }

    ////////////////////////////////////////////////////////////////////
    // Add format information
    // version, error correction code plus mask code
    ////////////////////////////////////////////////////////////////////
    internal void AddFormatInformation()
    {
        int mask;

        // version information
        if (QRCodeVersion >= 7)
        {
            var pos = QRCodeDimension - 11;
            var verInfo = VersionCodeArray[QRCodeVersion - 7];

            // top right
            mask = 1;
            for (var row = 0; row < 6; row++)
            {
                for (var col = 0; col < 3; col++)
                {
                    ResultMatrix[row, pos + col] = (verInfo & mask) != 0 ? FixedBlack : FixedWhite;
                    mask <<= 1;
                }
            }

            // bottom left
            mask = 1;
            for (var col = 0; col < 6; col++)
            {
                for (var row = 0; row < 3; row++)
                {
                    ResultMatrix[pos + row, col] = (verInfo & mask) != 0 ? FixedBlack : FixedWhite;
                    mask <<= 1;
                }
            }
        }

        // error correction code and mask number
        var formatInfoPtr = 0; // M is the default
        switch (_errorCorrection)
        {
            case ErrorCorrection.L:
                formatInfoPtr = 8;
                break;

            case ErrorCorrection.Q:
                formatInfoPtr = 24;
                break;

            case ErrorCorrection.H:
                formatInfoPtr = 16;
                break;
        }
        var formatInfo = FormatInfoArray[formatInfoPtr + MaskCode];

        // load format bits into result matrix
        mask = 1;
        for (var index = 0; index < 15; index++)
        {
            int formatBit = (formatInfo & mask) != 0 ? FixedBlack : FixedWhite;
            mask <<= 1;

            // top left corner
            ResultMatrix[FormatInfoOne[index, 0], FormatInfoOne[index, 1]] = (byte)formatBit;

            // bottom left and top right corners
            var row = FormatInfoTwo[index, 0];
            if (row < 0)
            {
                row += QRCodeDimension;
            }

            var col = FormatInfoTwo[index, 1];
            if (col < 0)
            {
                col += QRCodeDimension;
            }

            ResultMatrix[row, col] = (byte)formatBit;
        }
    }
    ////////////////////////////////////////////////////////////////////
    // Set encoded data bits length
    ////////////////////////////////////////////////////////////////////

    internal int DataLengthBits(EncodingMode encodingMode)
    {
        // Data length bits
        switch (encodingMode)
        {
            // numeric mode
            case EncodingMode.Numeric:
                return QRCodeVersion < 10 ? 10 : QRCodeVersion < 27 ? 12 : 14;

            // alpha numeric mode
            case EncodingMode.AlphaNumeric:
                return QRCodeVersion < 10 ? 9 : QRCodeVersion < 27 ? 11 : 13;

            // byte mode
            case EncodingMode.Byte:
                return QRCodeVersion < 10 ? 8 : 16;
        }

        throw new ApplicationException("Encoding mode error");
    }

    ////////////////////////////////////////////////////////////////////
    // Set data and error correction codewords length
    ////////////////////////////////////////////////////////////////////

    internal void SetDataCodewordsLength()
    {
        // index shortcut
        var blockInfoIndex = (QRCodeVersion - 1) * 4 + (int)_errorCorrection;

        // Number of blocks in group 1
        BlocksGroup1 = EcBlockInfo[blockInfoIndex, BLOCKS_GROUP1];

        // Number of data codewords in blocks of group 1
        DataCodewordsGroup1 = EcBlockInfo[blockInfoIndex, DATA_CODEWORDS_GROUP1];

        // Number of blocks in group 2
        BlocksGroup2 = EcBlockInfo[blockInfoIndex, BLOCKS_GROUP2];

        // Number of data codewords in blocks of group 2
        DataCodewordsGroup2 = EcBlockInfo[blockInfoIndex, DATA_CODEWORDS_GROUP2];

        // Total number of data codewords for this version and EC level
        MaxDataCodewords = BlocksGroup1 * DataCodewordsGroup1 + BlocksGroup2 * DataCodewordsGroup2;
        MaxDataBits = 8 * MaxDataCodewords;

        // total data plus error correction bits
        MaxCodewords = MaxCodewordsArray[QRCodeVersion];

        // Error correction codewords per block
        ErrCorrCodewords = (MaxCodewords - MaxDataCodewords) / (BlocksGroup1 + BlocksGroup2);

        // exit
    }

    ////////////////////////////////////////////////////////////////////
    // Build Base Matrix
    ////////////////////////////////////////////////////////////////////

    internal void BuildBaseMatrix()
    {
        // allocate base matrix
        BaseMatrix = new byte[QRCodeDimension + 5, QRCodeDimension + 5];

        // top left finder patterns
        for (var row = 0; row < 9; row++) for (var col = 0; col < 9; col++) BaseMatrix[row, col] = FinderPatternTopLeft[row, col];

        // top right finder patterns
        var pos = QRCodeDimension - 8;
        for (var row = 0; row < 9; row++)
        {
            for (var col = 0; col < 8; col++) BaseMatrix[row, pos + col] = FinderPatternTopRight[row, col];
        }

        // bottom left finder patterns
        for (var row = 0; row < 8; row++)
        {
            for (var col = 0; col < 9; col++) BaseMatrix[pos + row, col] = FinderPatternBottomLeft[row, col];
        }

        // Timing pattern
        for (var z = 8; z < QRCodeDimension - 8; z++)
        {
            BaseMatrix[z, 6] = BaseMatrix[6, z] = (z & 1) == 0 ? FixedBlack : FixedWhite;
        }

        // alignment pattern
        if (QRCodeVersion > 1)
        {
            var alignPos = AlignmentPositionArray[QRCodeVersion];
            var alignmentDimension = alignPos.Length;
            for (var row = 0; row < alignmentDimension; row++)
            {
                for (var col = 0; col < alignmentDimension; col++)
                {
                    if (col == 0 && row == 0 || col == alignmentDimension - 1 && row == 0 || col == 0 && row == alignmentDimension - 1)
                    {
                        continue;
                    }

                    int posRow = alignPos[row];
                    int posCol = alignPos[col];
                    for (var aRow = -2; aRow < 3; aRow++)
                    {
                        for (var aCol = -2; aCol < 3; aCol++)
                        {
                            BaseMatrix[posRow + aRow, posCol + aCol] = AlignmentPattern[aRow + 2, aCol + 2];
                        }
                    }
                }
            }
        }

        // reserve version information
        if (QRCodeVersion >= 7)
        {
            // position of 3 by 6 rectangles
            pos = QRCodeDimension - 11;

            // top right
            for (var row = 0; row < 6; row++)
            {
                for (var col = 0; col < 3; col++) BaseMatrix[row, pos + col] = FormatWhite;
            }

            // bottom right
            for (var col = 0; col < 6; col++)
            {
                for (var row = 0; row < 3; row++) BaseMatrix[pos + row, col] = FormatWhite;
            }
        }
    }

    ////////////////////////////////////////////////////////////////////
    // Apply Mask
    ////////////////////////////////////////////////////////////////////

    internal void ApplyMask(int mask)
    {
        MaskMatrix = (byte[,])BaseMatrix.Clone();
        switch (mask)
        {
            case 0:
                ApplyMask0();
                break;

            case 1:
                ApplyMask1();
                break;

            case 2:
                ApplyMask2();
                break;

            case 3:
                ApplyMask3();
                break;

            case 4:
                ApplyMask4();
                break;

            case 5:
                ApplyMask5();
                break;

            case 6:
                ApplyMask6();
                break;

            case 7:
                ApplyMask7();
                break;
        }
    }

    ////////////////////////////////////////////////////////////////////
    // Apply Mask 0
    // (row + column) % 2 == 0
    ////////////////////////////////////////////////////////////////////

    internal void ApplyMask0()
    {
        for (var row = 0; row < QRCodeDimension; row += 2)
        {
            for (var col = 0; col < QRCodeDimension; col += 2)
            {
                if ((MaskMatrix[row, col] & NonData) == 0)
                {
                    MaskMatrix[row, col] ^= 1;
                }

                if ((MaskMatrix[row + 1, col + 1] & NonData) == 0)
                {
                    MaskMatrix[row + 1, col + 1] ^= 1;
                }
            }
        }
    }

    ////////////////////////////////////////////////////////////////////
    // Apply Mask 1
    // row % 2 == 0
    ////////////////////////////////////////////////////////////////////

    internal void ApplyMask1()
    {
        for (var row = 0; row < QRCodeDimension; row += 2)
        {
            for (var col = 0; col < QRCodeDimension; col++)
            {
                if ((MaskMatrix[row, col] & NonData) == 0)
                {
                    MaskMatrix[row, col] ^= 1;
                }
            }
        }
    }

    ////////////////////////////////////////////////////////////////////
    // Apply Mask 2
    // column % 3 == 0
    ////////////////////////////////////////////////////////////////////

    internal void ApplyMask2()
    {
        for (var row = 0; row < QRCodeDimension; row++)
        {
            for (var col = 0; col < QRCodeDimension; col += 3)
            {
                if ((MaskMatrix[row, col] & NonData) == 0)
                {
                    MaskMatrix[row, col] ^= 1;
                }
            }
        }
    }

    ////////////////////////////////////////////////////////////////////
    // Apply Mask 3
    // (row + column) % 3 == 0
    ////////////////////////////////////////////////////////////////////

    internal void ApplyMask3()
    {
        for (var row = 0; row < QRCodeDimension; row += 3)
        {
            for (var col = 0; col < QRCodeDimension; col += 3)
            {
                if ((MaskMatrix[row, col] & NonData) == 0)
                {
                    MaskMatrix[row, col] ^= 1;
                }

                if ((MaskMatrix[row + 1, col + 2] & NonData) == 0)
                {
                    MaskMatrix[row + 1, col + 2] ^= 1;
                }

                if ((MaskMatrix[row + 2, col + 1] & NonData) == 0)
                {
                    MaskMatrix[row + 2, col + 1] ^= 1;
                }
            }
        }
    }

    ////////////////////////////////////////////////////////////////////
    // Apply Mask 4
    // ((row / 2) + (column / 3)) % 2 == 0
    ////////////////////////////////////////////////////////////////////

    internal void ApplyMask4()
    {
        for (var row = 0; row < QRCodeDimension; row += 4)
        {
            for (var col = 0; col < QRCodeDimension; col += 6)
            {
                if ((MaskMatrix[row, col] & NonData) == 0) MaskMatrix[row, col] ^= 1;
                if ((MaskMatrix[row, col + 1] & NonData) == 0) MaskMatrix[row, col + 1] ^= 1;
                if ((MaskMatrix[row, col + 2] & NonData) == 0) MaskMatrix[row, col + 2] ^= 1;

                if ((MaskMatrix[row + 1, col] & NonData) == 0) MaskMatrix[row + 1, col] ^= 1;
                if ((MaskMatrix[row + 1, col + 1] & NonData) == 0) MaskMatrix[row + 1, col + 1] ^= 1;
                if ((MaskMatrix[row + 1, col + 2] & NonData) == 0) MaskMatrix[row + 1, col + 2] ^= 1;

                if ((MaskMatrix[row + 2, col + 3] & NonData) == 0) MaskMatrix[row + 2, col + 3] ^= 1;
                if ((MaskMatrix[row + 2, col + 4] & NonData) == 0) MaskMatrix[row + 2, col + 4] ^= 1;
                if ((MaskMatrix[row + 2, col + 5] & NonData) == 0) MaskMatrix[row + 2, col + 5] ^= 1;

                if ((MaskMatrix[row + 3, col + 3] & NonData) == 0) MaskMatrix[row + 3, col + 3] ^= 1;
                if ((MaskMatrix[row + 3, col + 4] & NonData) == 0) MaskMatrix[row + 3, col + 4] ^= 1;
                if ((MaskMatrix[row + 3, col + 5] & NonData) == 0) MaskMatrix[row + 3, col + 5] ^= 1;
            }
        }
    }

    ////////////////////////////////////////////////////////////////////
    // Apply Mask 5
    // ((row * column) % 2) + ((row * column) % 3) == 0
    ////////////////////////////////////////////////////////////////////

    internal void ApplyMask5()
    {
        for (var row = 0; row < QRCodeDimension; row += 6)
        {
            for (var col = 0; col < QRCodeDimension; col += 6)
            {
                for (var delta = 0; delta < 6; delta++)
                {
                    if ((MaskMatrix[row, col + delta] & NonData) == 0) MaskMatrix[row, col + delta] ^= 1;
                }

                for (var delta = 1; delta < 6; delta++)
                {
                    if ((MaskMatrix[row + delta, col] & NonData) == 0) MaskMatrix[row + delta, col] ^= 1;
                }

                if ((MaskMatrix[row + 2, col + 3] & NonData) == 0) MaskMatrix[row + 2, col + 3] ^= 1;
                if ((MaskMatrix[row + 3, col + 2] & NonData) == 0) MaskMatrix[row + 3, col + 2] ^= 1;
                if ((MaskMatrix[row + 3, col + 4] & NonData) == 0) MaskMatrix[row + 3, col + 4] ^= 1;
                if ((MaskMatrix[row + 4, col + 3] & NonData) == 0) MaskMatrix[row + 4, col + 3] ^= 1;
            }
        }
    }

    ////////////////////////////////////////////////////////////////////
    // Apply Mask 6
    // (((row * column) % 2) + ((row * column) mod 3)) mod 2 == 0
    ////////////////////////////////////////////////////////////////////

    internal void ApplyMask6()
    {
        for (var row = 0; row < QRCodeDimension; row += 6)
        {
            for (var col = 0; col < QRCodeDimension; col += 6)
            {
                for (var delta = 0; delta < 6; delta++)
                {
                    if ((MaskMatrix[row, col + delta] & NonData) == 0) MaskMatrix[row, col + delta] ^= 1;
                }

                for (var delta = 1; delta < 6; delta++)
                {
                    if ((MaskMatrix[row + delta, col] & NonData) == 0) MaskMatrix[row + delta, col] ^= 1;
                }

                if ((MaskMatrix[row + 1, col + 1] & NonData) == 0) MaskMatrix[row + 1, col + 1] ^= 1;
                if ((MaskMatrix[row + 1, col + 2] & NonData) == 0) MaskMatrix[row + 1, col + 2] ^= 1;
                if ((MaskMatrix[row + 2, col + 1] & NonData) == 0) MaskMatrix[row + 2, col + 1] ^= 1;
                if ((MaskMatrix[row + 2, col + 3] & NonData) == 0) MaskMatrix[row + 2, col + 3] ^= 1;
                if ((MaskMatrix[row + 2, col + 4] & NonData) == 0) MaskMatrix[row + 2, col + 4] ^= 1;
                if ((MaskMatrix[row + 3, col + 2] & NonData) == 0) MaskMatrix[row + 3, col + 2] ^= 1;
                if ((MaskMatrix[row + 3, col + 4] & NonData) == 0) MaskMatrix[row + 3, col + 4] ^= 1;
                if ((MaskMatrix[row + 4, col + 2] & NonData) == 0) MaskMatrix[row + 4, col + 2] ^= 1;
                if ((MaskMatrix[row + 4, col + 3] & NonData) == 0) MaskMatrix[row + 4, col + 3] ^= 1;
                if ((MaskMatrix[row + 4, col + 5] & NonData) == 0) MaskMatrix[row + 4, col + 5] ^= 1;
                if ((MaskMatrix[row + 5, col + 4] & NonData) == 0) MaskMatrix[row + 5, col + 4] ^= 1;
                if ((MaskMatrix[row + 5, col + 5] & NonData) == 0) MaskMatrix[row + 5, col + 5] ^= 1;
            }
        }
    }

    ////////////////////////////////////////////////////////////////////
    // Apply Mask 7
    // (((row + column) % 2) + ((row * column) mod 3)) mod 2 == 0
    ////////////////////////////////////////////////////////////////////

    internal void ApplyMask7()
    {
        for (var row = 0; row < QRCodeDimension; row += 6)
        {
            for (var col = 0; col < QRCodeDimension; col += 6)
            {
                if ((MaskMatrix[row, col] & NonData) == 0) MaskMatrix[row, col] ^= 1;
                if ((MaskMatrix[row, col + 2] & NonData) == 0) MaskMatrix[row, col + 2] ^= 1;
                if ((MaskMatrix[row, col + 4] & NonData) == 0) MaskMatrix[row, col + 4] ^= 1;

                if ((MaskMatrix[row + 1, col + 3] & NonData) == 0) MaskMatrix[row + 1, col + 3] ^= 1;
                if ((MaskMatrix[row + 1, col + 4] & NonData) == 0) MaskMatrix[row + 1, col + 4] ^= 1;
                if ((MaskMatrix[row + 1, col + 5] & NonData) == 0) MaskMatrix[row + 1, col + 5] ^= 1;

                if ((MaskMatrix[row + 2, col] & NonData) == 0) MaskMatrix[row + 2, col] ^= 1;
                if ((MaskMatrix[row + 2, col + 4] & NonData) == 0) MaskMatrix[row + 2, col + 4] ^= 1;
                if ((MaskMatrix[row + 2, col + 5] & NonData) == 0) MaskMatrix[row + 2, col + 5] ^= 1;

                if ((MaskMatrix[row + 3, col + 1] & NonData) == 0) MaskMatrix[row + 3, col + 1] ^= 1;
                if ((MaskMatrix[row + 3, col + 3] & NonData) == 0) MaskMatrix[row + 3, col + 3] ^= 1;
                if ((MaskMatrix[row + 3, col + 5] & NonData) == 0) MaskMatrix[row + 3, col + 5] ^= 1;

                if ((MaskMatrix[row + 4, col] & NonData) == 0) MaskMatrix[row + 4, col] ^= 1;
                if ((MaskMatrix[row + 4, col + 1] & NonData) == 0) MaskMatrix[row + 4, col + 1] ^= 1;
                if ((MaskMatrix[row + 4, col + 2] & NonData) == 0) MaskMatrix[row + 4, col + 2] ^= 1;

                if ((MaskMatrix[row + 5, col + 1] & NonData) == 0) MaskMatrix[row + 5, col + 1] ^= 1;
                if ((MaskMatrix[row + 5, col + 2] & NonData) == 0) MaskMatrix[row + 5, col + 2] ^= 1;
                if ((MaskMatrix[row + 5, col + 3] & NonData) == 0) MaskMatrix[row + 5, col + 3] ^= 1;
            }
        }
    }
}