using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using CommandLine;
using Newtonsoft.Json;

namespace FolderCompare
{
    internal static class Program
    {
        private sealed class Options
        {
            /* TODO: Figure out how to use this properly
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }
            */

            [Option('j', "json", Required = true, HelpText = "Json file to store to or read the hashes from")]
            public string Json { get; set; }

            [Option('d', "directory", Required = true, HelpText = "Directory to check")]
            public string Folder { get; set; }

            [Option('g', "generate", Required = false, Default = false, HelpText = "Run in generator mode")]
            public bool Generate { get; set; }
        }

        private static readonly SHA256CryptoServiceProvider Provider = new SHA256CryptoServiceProvider();

        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    if (!Directory.Exists(o.Folder))
                    {
                        Console.Error.WriteLine($"{o.Folder} does not exist");
                        return;
                    }

                    if (!o.Generate && !File.Exists(o.Json))
                    {
                        Console.Error.WriteLine($"{o.Json} does not exist");
                        return;
                    }
                    
                    var dic = !o.Generate
                        ? JsonConvert.DeserializeObject<ConcurrentDictionary<string, (string hash, long size)>>(File.ReadAllText(o.Json))
                        : new ConcurrentDictionary<string, (string hash, long size)>();

                    var issues = new ConcurrentDictionary<string, string>();
                    
                    var di = new DirectoryInfo(o.Folder);

                    

                    Parallel.ForEach(di.EnumerateFiles("*", SearchOption.AllDirectories), fi =>
                    {
                        var relative = fi.FullName.Remove(0, o.Folder.Length);
                        if (!o.Generate)
                        {
                            var message = string.Empty;
                            if (!dic.TryRemove(relative, out var source))
                            {
                                message = "Did not exist in the source directory";
                            }

                            if (fi.Length != source.size)
                            {
                                message = "Size does not match";
                            }
                            else
                            {
                                var calculated = Hash(fi);

                                if (source.hash != calculated.hash)
                                {
                                    message = "Contents do not match";
                                }
                            }

                            if (!string.IsNullOrEmpty(message))
                            {
                                issues.TryAdd(relative, message);
                            }
                        }
                        else
                        {
                            dic.TryAdd(relative, Hash(fi));
                        }
                    });

                    if (o.Generate)
                    {
                        foreach (var kv in dic)
                        {
                            issues.TryAdd(kv.Key, "Source file not found");
                        }

                        foreach (var kv in issues)
                        {
                            Console.WriteLine($"{kv.Key} => {kv.Value}");
                        }
                    }
                    else
                    {
                        var output = JsonConvert.SerializeObject(dic);
                        File.WriteAllText(o.Json, output);
                    }
                });
        }

        private static (string hash, long size) Hash(FileInfo fi)
        {
            using (var f = fi.OpenRead())
            using (var b = new BufferedStream(f, 8192))
            {
                var hash = Provider.ComputeHash(b);
                return (BitConverter.ToString(hash).Replace("-", string.Empty), fi.Length);
            }
        }
    }
}