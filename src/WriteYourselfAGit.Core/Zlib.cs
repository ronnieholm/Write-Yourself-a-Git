using System;
using System.Collections.Generic;
using ICSharpCode.SharpZipLib.Zip.Compression;

namespace WriteYourselfAGit.Core
{
    public static class Zlib
    {
        // .NET Core Deflate class doesn't support RFC 1950. It deals with the
        // raw deflate stream only. zlib wraps the deflate algorithm in a header
        // and trailer which Deflate doesn't support. We could sort of get
        // around this by skipping the first two header bytes before feeding the
        // remaining bytes to Deflate, but that's ugly. And the trailer would be
        // part of the result. Or we could implement RFC1950 header and trailer
        // ourselves. But this code is about writing a simple Git client, not
        // about implementing zlib, so we go with sharpziplib.

        // https://stackoverflow.com/questions/37845440/net-deflatestream-vs-linux-zlib-difference
        // Actually three possible zlib formats. Deflate doesn't add the
        // required header and footer to the compressed data, so we'll have to
        // do it ourselves (RFC 1950).
        public static byte[] Compress(byte[] uncompressed)
        {
            var deflater = new Deflater();
            deflater.SetInput(uncompressed);
            deflater.Finish();

            var outputBuffer = new byte[1024];
            var compressed = new List<byte>();

            while (!deflater.IsFinished)
            {
                var count = deflater.Deflate(outputBuffer);
                compressed.AddRange(outputBuffer[..count]);
                Array.Clear(outputBuffer, 0, outputBuffer.Length);
            }

            return compressed.ToArray();
        }

        public static byte[] Decompress(byte[] compressed)
        {
            var inflater = new Inflater();
            inflater.SetInput(compressed);

            var outputBuffer = new byte[1024];
            var uncompressed = new List<byte>();

            while (!inflater.IsFinished)
            {
                var count = inflater.Inflate(outputBuffer);
                uncompressed.AddRange(outputBuffer[0..count]);
                Array.Clear(outputBuffer, 0, outputBuffer.Length);
            }

            return uncompressed.ToArray();
        }
    }
}