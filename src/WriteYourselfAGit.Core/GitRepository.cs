using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace WriteYourselfAGit.Core
{
    public class GitRepository
    {
        public string WorkTree { get; }
        public string GitDirectory { get; }
        private readonly IniFileReaderWriter _iniFileReaderWriter = new();

        // Constructor is for when a Git repository already exists. To create
        // the initial repository, call the static GitRepository.Init().
        //
        // The purpose of the force parameters becomes apparent later in
        // the tutorial. For now assume it's always false.
        public GitRepository(string path)
        {
            WorkTree = path;
            GitDirectory = Path.Combine(path, ".git");

            if (!Directory.Exists(GitDirectory))
                throw new Exception($"Not a Git repository: {GitDirectory}");

            var configFile = Path.Combine(GitDirectory, "config");
            if (!File.Exists(configFile))
                throw new Exception($"Config file missing: {configFile}");
            
            var lines = File.ReadAllLines(configFile);
            _iniFileReaderWriter.Deserialize(lines);
            
            var version = _iniFileReaderWriter.Entries["core"]["repositoryformatversion"];
            if (version != "0")
                throw new Exception($"Unsupported repositoryformatversion: {version}");
        }

        public static void Init(string workTree)
        {
            if (File.Exists(workTree))
                throw new Exception($"Not a directory: {new DirectoryInfo(workTree).FullName}");
            if (Directory.Exists(workTree) && Directory.EnumerateFileSystemEntries(workTree).Any())
                throw new Exception($"Directory not empty: {new DirectoryInfo(workTree).FullName}");

            var dotGit = Path.Join(workTree, ".git");
            foreach (var directory in new[] { "branches", "objects", "refs/tags", "refs/heads" })
                EnsureDirectoryPath(Path.Join(dotGit, directory), true);

            File.WriteAllText(EnsureFilePath(Path.Join(dotGit, "description"), false), "Unnamed repository; edit this file 'description' to name the repository\n");
            File.WriteAllText(EnsureFilePath(Path.Join(dotGit, "HEAD"), false), "ref: refs/heads/master\n");
            
            var content = new IniFileReaderWriter();
            content.Set("core", "repositoryformatversion", "0");
            content.Set("core", "filemode", "false");
            content.Set("core", "bare", "false");
            File.WriteAllText(EnsureFilePath(Path.Join(dotGit, "config"), false), content.Serialize());
        }
        
        // Get and optionally creates directory path up to filename. It
        // assumes the last component of the path is a file.
        private static string EnsureFilePath(string path, bool createDirectories)
        {
            var directoryName = Path.GetDirectoryName(path);
            return EnsureDirectoryPath(directoryName, createDirectories) != null 
                ? path 
                : null;
        }

        // Get and optionally creates directory path up to directory. It
        // assumes the last component of the path is a directory.
        private static string EnsureDirectoryPath(string path, bool createDirectories)
        {
            if (Directory.Exists(path))
                return path;
            if (File.Exists(path))
                throw new Exception($"Not a directory: {path}");

            if (!createDirectories)
                return null;
            
            Directory.CreateDirectory(path);
            return path;
        }
        
        public static GitRepository FindGitRoot(string path)
        {
            var git = Path.Join(path, ".git");
            if (Directory.Exists(git))
                return new GitRepository(path);

            var parent = Directory.GetParent(path);

            // We're at root and cannot navigate up any further
            return parent != null 
                ? FindGitRoot(parent.FullName) 
                : throw new Exception("No .git directory");
        }

        // Output of FindObject is argument to ReadGitObject
        public string FindObject(string name) => name;

        public GitObject ReadGitObject(string sha1)
        {
            var path = Path.Join(GitDirectory, "objects", sha1[..2], sha1[2..]);
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

        // Is kind of equal to Save Git Object. WriteObject calls this method.
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

        // TODO: Would be cleaner if output of HashGitObject could be passed into WriteGitObject
        public string WriteGitObject(GitObject obj, bool actuallyWrite = true)
        {
            var (objectId, compressed) = HashGitObject(obj);
            if (!actuallyWrite)
                return objectId;
            
            var relativePath = Path.Join(GitDirectory, "objects", objectId[..2], objectId[2..]);
            var fullPath = EnsureFilePath(relativePath, true); // TODO: When would we use createDirectories = false?
            File.WriteAllBytes(fullPath, compressed);
            return objectId;
        }
    }

    public abstract class GitObject
    {
        public enum ObjectType
        {
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
        public override ObjectType Format => ObjectType.Blob; // TODO: Why not infer format from typeof(GitBlob)?
        public byte[] Raw { get; private set; }

        public GitBlob(GitRepository repository, byte[] raw) => Raw = raw; // TODO: why pass in repository?
        public override byte[] Serialize() => Raw;
        public override void Deserialize(byte[] raw) => Raw = raw;
    }
}