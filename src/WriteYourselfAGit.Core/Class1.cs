using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Text;
using System.Security.Cryptography;
using ICSharpCode.SharpZipLib.Zip.Compression;

// See also: https://matthew-brett.github.io/curious-git/
// Git Parable with images: http://practical-neuroimaging.github.io/git_parable.html

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
        // ourselves. But this code is about writing a simple git client, not
        // about zlib implementation, so we go with sharpziplib.

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

    public class IniFileReaderWriter
    {
        private static readonly Regex SectionPattern = new Regex(@"^\[(?<section>(.+?))\]$");
        private static readonly Regex KeyValuePattern = new Regex(@"\t(?<key>(.+?)) = (?<value>(.+?))$");

        public readonly Dictionary<string, Dictionary<string, string>> Entries = new Dictionary<string, Dictionary<string, string>>();

        public void Deserialize(string[] lines)
        {
            var section = "";
            foreach (var line in lines)
            {
                var sectionMatch = SectionPattern.Match(line);
                var keyValueMatch = KeyValuePattern.Match(line);

                if (sectionMatch.Success)
                {
                    section = sectionMatch.Groups["section"].Value;
                    Entries[section] = new Dictionary<string, string>();
                }
                else if (keyValueMatch.Success)
                    Entries[section].Add(keyValueMatch.Groups["key"].Value, keyValueMatch.Groups["value"].Value);
            }
        }

        public void Set(string section, string key, string value)
        {
            var success = Entries.TryGetValue(section, out var keyValues);
            if (!success)
            {
                keyValues = new Dictionary<string, string>();
                Entries[section] = keyValues;
            }
            Entries[section][key] = value;
        }

        public string Serialize()
        {
            var sb = new StringBuilder();
            foreach (var section in Entries.Keys)
            {
                sb.AppendLine($"[{section}]");
                foreach (var keyValues in Entries[section])
                    sb.AppendLine($"\t{keyValues.Key} = {keyValues.Value}");
            }
            return sb.ToString();
        }
    }

    public static class KeyValueListWithMessageParser
    {
        public static void Parse(byte[] raw)
        {
            var o = new OrderedDictionary {{"c", "x"}, {"a", "y"}, {"b", "z"}};

            foreach (var x in o.Keys)
                System.Console.WriteLine($"{x} : {o[x]}");
        }
    }

    public class GitRepository
    {
        public string WorkTree { get; }
        public string GitDirectory { get; }
        private readonly IniFileReaderWriter iniFileReaderWriter = new IniFileReaderWriter();

        public GitRepository(string path, bool force = false)
        {
            WorkTree = path;
            GitDirectory = Path.Combine(path, ".git");

            if (!(force || Directory.Exists(GitDirectory)))
                throw new Exception($"Not a Git repository: {GitDirectory}");

            var configFile = Path.Combine(GitDirectory, "config");
            if (configFile != null && File.Exists(configFile))
            {
                var lines = File.ReadAllLines(configFile);
                iniFileReaderWriter.Deserialize(lines);
            }
            else if (!force)
                throw new Exception($"Config file missing: {configFile}");

            if (!force)
            {
                var version = iniFileReaderWriter.Entries["core"]["repositoryformatversion"];
                if (version != "0")
                    throw new Exception($"Unsupported repositoryformatversion: {version}");
            }
        }

        // Get, and optionally creates directory path, up to filename. It
        // assumes the last component of the path is a file.
        public string EnsureFilePath(string path, bool createDirectories)
        {
            var directoryName = Path.GetDirectoryName(path);
            if (EnsureDirectoryPath(directoryName, createDirectories) != null)
                return Path.Join(GitDirectory, path);
            return null;
        }

        // Get, and optionally creates directory path, up to directory. It
        // assumes the last component of the path is a directory.
        public string EnsureDirectoryPath(string path, bool createDirectories)
        {
            var directoryPath = Path.Join(GitDirectory, path);
            if (Directory.Exists(directoryPath))
                return directoryPath;
            else if (File.Exists(directoryPath))
                throw new Exception($"Not a directory: {directoryPath}");

            if (createDirectories)
            {
                Directory.CreateDirectory(directoryPath);
                return directoryPath;
            }
            return null;
        }

        public static string GetDefaultConfig()
        {
            var i = new IniFileReaderWriter();
            i.Set("core", "repositoryformatversion", "0");
            i.Set("core", "filemode", "false");
            i.Set("core", "bare", "false");
            return i.Serialize();
        }

        public static GitRepository FindGitRoot(string path = ".", bool required = true)
        {
            var gitPath = Path.Join(path, ".git");
            if (Directory.Exists(gitPath))
                return new GitRepository(path);

            var parentPath = Directory.GetParent(path);

            // We're at root and cannot navigate up any further
            if (parentPath == null)
                if (required)
                    throw new Exception("No git directory");
                else
                    return null;

            return FindGitRoot(parentPath.FullName, required);
        }

        public GitObject ReadGitObject(string sha1)
        {
            var path = Path.Join(GitDirectory, "objects", sha1[0..2], sha1[2..]);
            var compressed = File.ReadAllBytes(path);
            var decompressed = Zlib.Decompress(compressed);

            var formatEnd = Array.IndexOf(decompressed, (byte)0x20);
            var format = Encoding.ASCII.GetString(decompressed[..formatEnd]);

            var sizeEnd = Array.IndexOf(decompressed, (byte)0x00, formatEnd);
            var sizeString = Encoding.ASCII.GetString(decompressed[(formatEnd + 1)..sizeEnd]);
            var size = int.Parse(sizeString);
            var raw = decompressed[(sizeEnd + 1)..];

            if (size != raw.Length)
                throw new Exception($"Malformed object {path}. Bad length");

            return format switch
            {
                // "commit" => new GitCommit().Deserialize(content),
                // "tree" => new GitCommit(content),
                // "tag" => new GitCommit(content),
                "blob" => new GitBlob(this, raw),
                _ => throw new Exception($"Unsupported object type: {format}")
            };
        }

        public static (string, byte[]) HashGitObject(GitObject obj)
        {
            var raw = obj.Serialize();
            var header = Encoding.ASCII.GetBytes(obj.Format.ToString().ToLower())
                .Append((byte)0x20)
                .Concat(Encoding.ASCII.GetBytes(raw.Length.ToString()))
                .Append((byte)0x00);
            var uncompressed = header.Concat(raw).ToArray();
            var compressed = Zlib.Compress(uncompressed);

            using var sha1Computer = new SHA1Managed();
            var sha1 = sha1Computer.ComputeHash(compressed);
            var objectId = BitConverter.ToString(sha1).Replace("-", "").ToLower();
            return (objectId, compressed);
        }

        public string WriteGitObject(GitObject obj, bool actuallyWrite = true)
        {
            var (objectId, compressed) = HashGitObject(obj);
            if (actuallyWrite)
            {
                var relativePath = Path.Join("objects", objectId[..2], objectId[2..]);
                var fullPath = EnsureFilePath(relativePath, true);
                File.WriteAllBytes(fullPath, compressed);
            }
            return objectId;
        }

        public string FindObject(string name)
        {
            return name;
        }
    }

    public abstract class GitObject
    {
        public enum ObjectType
        {
            None,
            Commit,
            Tree,
            Tag,
            Blob
        }

        public abstract ObjectType Format { get; }
        public abstract byte[] Serialize();
        public abstract void Deserialize(byte[] bytes);
    }

    public class GitCommit : GitObject
    {
        public override ObjectType Format => ObjectType.Commit;
        public byte[] Raw { get; private set; }

        public GitCommit(GitRepository repository, byte[] raw)
        {
            Raw = raw;
        }

        public override byte[] Serialize()
        {
            return null;
        }

        public override void Deserialize(byte[] bytes)
        {
        }
    }

    public class GitTree : GitObject
    {
        public override ObjectType Format => ObjectType.Tree;
        public byte[] Raw { get; private set; }

        public GitTree(GitRepository repository, byte[] raw)
        {
            Raw = raw;
        }

        public override byte[] Serialize()
        {
            return null;
        }

        public override void Deserialize(byte[] bytes)
        {
        }
    }

    public class GitTag : GitObject
    {
        public override ObjectType Format => ObjectType.Tag;
        public byte[] Raw { get; private set; }

        public GitTag(GitRepository repository, byte[] raw)
        {
            Raw = raw;
        }

        public override byte[] Serialize()
        {
            return null;
        }

        public override void Deserialize(byte[] bytes)
        {
        }
    }

    public class GitBlob : GitObject
    {
        public override ObjectType Format => ObjectType.Blob;
        public byte[] Raw { get; private set; }

        public GitBlob(GitRepository repository, byte[] raw) => Raw = raw;
        public override byte[] Serialize() => Raw;
        public override void Deserialize(byte[] raw) => Raw = raw;
    }
}
