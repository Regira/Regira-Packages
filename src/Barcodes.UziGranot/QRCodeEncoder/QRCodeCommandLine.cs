/////////////////////////////////////////////////////////////////////
//
//	QR Code Encoder Library
//
//	QR Code Encoder command line
//
//	Author: Uzi Granot
//	Original Version: 1.0
//	Date: June 30, 2018
//	Copyright (C) 2018-2019 Uzi Granot. All Rights Reserved
//	For full version history please look at QREncode.cs
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



// ReSharper disable once CheckNamespace
namespace QRCodeEncoderLibrary;

/// <summary>
/// Command line class
/// </summary>
public static class QRCodeCommandLine
{
    /// <summary>
    /// Command line help text
    /// </summary>
    public static readonly string Help =
        "QRCode encoder console application support\r\n" +
        "AppName [optional arguments] input-file output-file\r\n" +
        "Output file must have .png extension\r\n" +
        "Options format /code:value or -code:value (the : can be =)\r\n" +
        "Error correction level. code=[error|e], value=[low|l|medium|m|quarter|q|high|h], default=m\r\n" +
        "Module size. code=[module|m], value=[1-100], default=2\r\n" +
        "Quiet zone. code=[quiet|q], value=[2-200], default=4, min=2*width\r\n" +
        "Text file format. code=[text|t] see notes below\r\n" +
        "Input file is binary unless text file option is specified\r\n" +
        "If input file format is text character set is iso-8859-1\r\n";

    /// <summary>
    /// Encode QRCode using command line class
    /// </summary>
    /// <param name="commandLine">Command line text</param>
    public static void Encode(string commandLine)
    {
        // command line has no quote characters
        if (commandLine.IndexOf('"') < 0)
        {
            Encode(commandLine.Split([' ']));
            return;
        }

        // command line has quote characters
        var args = new List<string>();
        var ptr = 0;
        for (; ; )
        {
            // skip white
            for (; ptr < commandLine.Length && commandLine[ptr] == ' '; ptr++)
            {
                // continue
            }

            if (ptr == commandLine.Length)
            {
                break;
            }

            // test for quote
            int ptr1;
            int ptr2;
            if (commandLine[ptr] == '"')
            {
                // look for next quote
                ptr++;
                ptr1 = commandLine.IndexOf('"', ptr);
                if (ptr1 < 0)
                {
                    throw new ArgumentException("Unbalanced double quote");
                }

                ptr2 = ptr1 + 1;
            }
            else
            {
                // look for next white
                ptr1 = commandLine.IndexOf(' ', ptr);
                if (ptr1 < 0)
                {
                    ptr1 = commandLine.Length;
                }

                ptr2 = ptr1;
            }
            args.Add(commandLine.Substring(ptr, ptr1 - ptr));
            ptr = ptr2;
        }
        Encode(args.ToArray());
    }

    /// <summary>
    /// Command line encode
    /// </summary>
    /// <param name="args">Arguments array</param>
    public static void Encode(string[] args)
    {
        // help
        if (args == null || args.Length < 2)
        {
            throw new ArgumentException("help");
        }

        var textFile = false;
        string inputFileName = null;
        string outputFileName = null;
        string code;

        var encoder = new QRCodeEncoder();

        for (var argPtr = 0; argPtr < args.Length; argPtr++)
        {
            var arg = args[argPtr];

            // file name
            if (arg[0] != '/' && arg[0] != '-')
            {
                if (inputFileName == null)
                {
                    inputFileName = arg;
                    continue;
                }
                if (outputFileName == null)
                {
                    outputFileName = arg;
                    continue;
                }
                throw new ArgumentException($"Invalid option. Argument={argPtr + 1}");
            }

            // search for colon
            var ptr = arg.IndexOf(':');
            if (ptr < 0) ptr = arg.IndexOf('=');
            string value;
            if (ptr > 0)
            {
                code = arg.Substring(1, ptr - 1);
                value = arg.Substring(ptr + 1);
            }
            else
            {
                code = arg.Substring(1);
                value = string.Empty;
            }

            code = code.ToLower();
            value = value.ToLower();

            switch (code)
            {
                case "error":
                case "e":
                    ErrorCorrection ec;
                    switch (value)
                    {
                        case "low":
                        case "l":
                            ec = ErrorCorrection.L;
                            break;

                        case "medium":
                        case "m":
                            ec = ErrorCorrection.M;
                            break;

                        case "quarter":
                        case "q":
                            ec = ErrorCorrection.Q;
                            break;

                        case "high":
                        case "h":
                            ec = ErrorCorrection.H;
                            break;

                        default:
                            throw new ArgumentException("Error correction option in error");
                    }
                    encoder.ErrorCorrection = ec;
                    break;

                case "module":
                case "m":
                    if (!int.TryParse(value, out var moduleSize)) moduleSize = -1;
                    encoder.ModuleSize = moduleSize;
                    break;

                case "quiet":
                case "q":
                    if (!int.TryParse(value, out var quietZone)) quietZone = -1;
                    encoder.QuietZone = quietZone;
                    break;

                case "text":
                case "t":
                    textFile = true;
                    break;

                default:
                    throw new ApplicationException($"Invalid argument no {argPtr + 1}, code {code}");
            }
        }

        if (textFile)
        {
            var inputText = File.ReadAllText(inputFileName!);
            encoder.Encode(inputText);
        }
        else
        {
            var inputBytes = File.ReadAllBytes(inputFileName!);
            encoder.Encode(inputBytes);
        }

        encoder.SaveQRCodeToPngFile(outputFileName);
    }
}