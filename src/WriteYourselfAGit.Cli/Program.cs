using System;
using System.Linq;
using System.IO;
using WriteYourselfAGit.Core;
using System.Text;

// TODO: create test that runs through all commands.
// TODO: add non-nullable reference types check.
// TODO: setup DI container with logging and injection

namespace WriteYourselfAGit.Cli
{
    class Program
    {
        static void Usage()
        {
            Console.WriteLine(@"
                add
                cat-file TYPE OBJECTID -- (Provide content of repository objects)
                checkout
                commit
                hash-object [-w] [-t TYPE] FILE -- Compute object ID and optionally creates a blob from a file
                init path -- (Initialize a new, empty repository)
                log
                ls-tree
                merge
                rebase
                rev-parse
                rm
                show-ref
                tag");
            Environment.Exit(0);
        }

        static void Main(string[] args)
        {
            {
                KeyValueListWithMessageParser.Parse(null);

                var repo = new GitRepository("/tmp/git_example");
                repo.ReadGitObject("e27bb34b0807ebf1b91bb66a4c147430cde4f08f");

                var blob = new GitBlob(repo, Encoding.ASCII.GetBytes("123456789"));
                var objectId = repo.WriteGitObject(blob, true);
                var blob1 = repo.ReadGitObject(objectId);
            }

            switch (args[0])
            {
                case "add":
                    break;
                case "cat-file":
                {
                    if (args.Length != 3)
                        Usage();

                    var success = Enum.TryParse<GitObject.ObjectType>(args[1], true, out var format);
                    if (!success)
                    {
                        Console.WriteLine("Specify the type");
                        Usage();
                    }
                    var objectId = args[2];
                    CatFile(format, objectId);
                    break;
                }
                case "checkout":
                    break;
                case "commit":
                    break;
                case "hash-object":
                {
                    if (!(args.Length == 4 || args.Length == 5))
                        Usage();

                    var write = false;
                    GitObject.ObjectType format = GitObject.ObjectType.None;
                    var path = "";

                    var i = 1;
                    do
                    {
                        if (args[i] == "-w")
                        {
                            write = true;
                            i++;
                            continue;
                        }
                        else if (args[i] == "-t")
                        {
                            var success = Enum.TryParse<GitObject.ObjectType>(args[i + 1], true, out format);
                            if (!success)
                            {
                                Console.WriteLine("Specify the type");
                                Usage();                                
                            }
                            i += 2;
                            continue;
                        }
                        else if (i == args.Length - 1)
                        {
                            path = args[i];
                            break;
                        }
                    }
                    while (i < args.Length);
                    HashObject(write, format, path);
                    break;
                }
                case "init":
                {                   
                    if (args.Length != 2)
                        Usage();

                    var path = ".";
                    if (args.Length == 2)
                        path = args[1];
                    Init(path);
                    break;
                }
                case "log":
                    break;
                case "ls-tree":
                    break;
                case "merge":
                    break;
                case "rebase":
                    break;
                case "rev-parse":
                    break;
                case "rm":
                    break;
                case "show-ref":
                    break;
                case "tag":
                    break;
                default:
                    Usage();
                    break;
            }
        }

        static void Init(string path)
        {
            var repository = new GitRepository(path, true);
            if (File.Exists(repository.WorkTree))
                throw new Exception($"Not a directory: {new DirectoryInfo(repository.WorkTree).FullName}");
            if (Directory.Exists(repository.WorkTree) && Directory.EnumerateFileSystemEntries(repository.WorkTree).Any())
                throw new Exception($"Directory not empty: {new DirectoryInfo(repository.WorkTree).FullName}");

            foreach (var p in new[] { "branches", "objects", "refs/tags", "refs/heads" })
                repository.EnsureDirectoryPath(p, true);

            File.WriteAllText(repository.EnsureFilePath("description", false), "Unnamed repository; edit this file 'description' to name the repository\n");
            File.WriteAllText(repository.EnsureFilePath("HEAD", false), "ref: refs/heads/master\n");
            File.WriteAllText(repository.EnsureFilePath("config", false), GitRepository.GetDefaultConfig());
        }

        static void CatFile(GitObject.ObjectType format, string objectId)
        {
            var repo = GitRepository.FindGitRoot(Environment.CurrentDirectory);
            var obj = repo.ReadGitObject(objectId);
            
            if (obj is GitBlob)
                Console.WriteLine(Encoding.ASCII.GetString(obj.Serialize()));
        }

        static void HashObject(bool write, GitObject.ObjectType format, string path)
        {
            GitRepository repo = null;
            if (write)
                repo = GitRepository.FindGitRoot(Environment.CurrentDirectory);
            
            var bytes = File.ReadAllBytes(path);
            GitObject obj = null;
            switch (format)
            {
                case GitObject.ObjectType.Commit:
                    obj = new GitCommit(repo, bytes);
                    break;
                case GitObject.ObjectType.Tree:
                    obj = new GitTree(repo, bytes);
                    break;
                case GitObject.ObjectType.Tag:
                    obj = new GitTag(repo, bytes);              
                    break;
                case GitObject.ObjectType.Blob:
                    obj = new GitBlob(repo, bytes);
                    break;
                default:
                    throw new Exception($"Unsupported format: {format}");                    
            }

            string sha1;
            if (write)
                sha1 = repo.WriteGitObject(obj, write);
            else
                (sha1, _) = GitRepository.HashGitObject(obj);
                
            Console.WriteLine(sha1);
        }
    }
}