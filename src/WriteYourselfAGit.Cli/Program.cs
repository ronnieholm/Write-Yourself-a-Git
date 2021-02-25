using System;
using System.Linq;
using System.IO;
using System.Text;
using WriteYourselfAGit.Core;

// TODO: create test that runs through all commands.
// TODO: add non-nullable reference types check.
// TODO: setup DI container with logging and injection

namespace WriteYourselfAGit.Cli
{
    static class Program
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
        
        static int _idx;
        
        static void Main(string[] args)
        {
            // {
            //     KeyValueListWithMessageParser.Parse(null);
            //
            //     var repo = new GitRepository("/tmp/git_example");
            //     repo.ReadGitObject("e27bb34b0807ebf1b91bb66a4c147430cde4f08f");
            //
            //     var blob = new GitBlob(repo, Encoding.ASCII.GetBytes("123456789"));
            //     var objectId = repo.WriteGitObject(blob, true);
            //     var blob1 = repo.ReadGitObject(objectId);
            // }

            if (args.Length == 0)
                Usage();

            var command = args[_idx++];
            switch (command)
            {
                case "init":
                {
                    if (args.Length != 2)
                        Usage();

                    // Path is optional. If missing, assume current directory.
                    var path = args.Length == 2 ? args[_idx++] : ".";
                    GitRepository.Init(path);
                    break;
                }
                case "abc":
                {
                    break;
                }
            }

            return;
            
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

                        if (args[i] == "-t")
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

                        if (i == args.Length - 1)
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
                    GitRepository.Init(path);
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
        
        static void CatFile(GitObject.ObjectType format, string objectId)
        {
            var repo = GitRepository.FindGitRoot(Environment.CurrentDirectory);
            var obj = repo.ReadGitObject(objectId);
            
            if (obj is GitBlob)
                Console.WriteLine(Encoding.ASCII.GetString(obj.Serialize()));
        }

        static void HashObject(bool write, GitObject.ObjectType format, string path)
        {
            GitRepository? repo = null;
            if (write)
                repo = GitRepository.FindGitRoot(Environment.CurrentDirectory);
            
            var bytes = File.ReadAllBytes(path);
            GitObject? obj = null;
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