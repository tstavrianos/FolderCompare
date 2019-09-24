using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using CommandLine;
using Newtonsoft.Json;

namespace FolderCompare
{
    internal static class Program
    {
        private sealed class Options
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }
            
            [Option('j', "json", Required = true, HelpText = "Json file to store to or read the hashes from")]
            public string Json { get; set; }
            
            [Option('d', "directory", Required = true, HelpText = "Directory to check")]
            public string Folder { get; set; }
            
            [Option('g', "generate", Required = false, Default=false, HelpText = "Run in generator mode")]
            public bool Generate { get; set; }
        }
        
        private static readonly SHA256CryptoServiceProvider  Provider = new SHA256CryptoServiceProvider();
        public static void Main(string[] args)
        {

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o =>
                {
                    var dic = !o.Generate ? JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(o.Json)) : new Dictionary<string, string>();
            
                    foreach(var file in Directory.GetFiles(o.Folder, "*", SearchOption.AllDirectories))
                    {
                        var hash = Hash(file);
                        var relative = file.Remove(0, o.Folder.Length);
                        if (!o.Generate)
                        {
                            var ok = true;
                            if (!dic.ContainsKey(relative))
                            {
                                ok = false;
                            }
                            else if(dic[relative] != hash)
                            {
                                ok = false;
                            }

                            if (o.Verbose)
                            {
                                Console.WriteLine("{0}, calculated Sha256 hash: {1}, matches: {2}", relative, hash, ok ? "yes" : "no");
                            }
                            else
                            {
                                if (!ok)
                                {
                                    Console.WriteLine("{0}, calculated Sha256 hash: {1}", relative, hash);
                                }
                            }
                        }
                        else
                        {
                            dic.Add(relative, Hash(file));
                            if (o.Verbose)
                            {
                                Console.WriteLine("{0}, calculated Sha256 hash: {1}", relative, hash);
                            }
                        }
                    }

                    if (!o.Generate) return;
                    var output = JsonConvert.SerializeObject(dic);
                    File.WriteAllText(o.Json, output);
                });

        }

        private static string Hash(string filename)
        {
            using (var f = File.OpenRead(filename))
            using (var b = new BufferedStream(f, 8192))
            {
                var hash = Provider.ComputeHash(b);
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }
    }
}