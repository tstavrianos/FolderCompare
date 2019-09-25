using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using CommandLine;
using Newtonsoft.Json;
using Standart.Hash.xxHash;

namespace FolderCompare
{
    internal static class Program
    {
        private readonly struct HashSize
        {
            public ulong Hash { get; }
            public long Size { get; }

            public HashSize(ulong hash, long size)
            {
                this.Hash = hash;
                this.Size = size;
            }
        }

        private sealed class Options
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }

            [Option('j', "json", Required = true, HelpText = "Json file to store to or read the hashes from")]
            public string Json { get; set; }

            [Option('d', "directory", Required = true, HelpText = "Directory to check")]
            public string Folder { get; set; }

            [Option('g', "generate", Required = false, Default = false, HelpText = "Run in generator mode")]
            public bool Generate { get; set; }
        }
        private static Options _o;

        public static async Task Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(o1 => _o = o1);
            if(_o != null)
                await ProcessFiles();
        }

        private sealed class SourceFile
        {
            public SourceFile(string filePath, Stream data)
            {
                this.FilePath = filePath;
                this.Data = data;
                this.Size = data.Length;
            }

            public string FilePath { get; }
            public Stream Data { get; }
            public long Size { get; }
        }

        private sealed class TargetFile
        {
            public TargetFile(string filePath, ulong hash, long size)
            {
                this.FilePath = filePath;
                this.Hash = hash;
                this.Size = size;
            }

            public string FilePath { get; }
            public ulong Hash { get; }
            public long Size { get; }
        }

        private sealed class CheckedFile
        {
            public CheckedFile(string filePath, string message, ulong hash, long size)
            {
                this.FilePath = filePath;
                this.Message = message;
                this.Hash = hash;
                this.Size = size;
            }

            public string FilePath { get; }
            public string Message { get; }
            public ulong Hash { get; }
            public long Size { get; }
        }

        private static ConcurrentDictionary<string, HashSize> _database;

        private static readonly ConcurrentDictionary<string, (string message, ulong hash, long size)> Issues = new ConcurrentDictionary<string, (string message, ulong hash, long size)>();

        private static async Task ProcessFiles()
        {
            if (!_o.Generate && !File.Exists(_o.Json))
            {
                Console.Error.WriteLine($"{_o.Json} does not exist");
                return;
            }
            if (!Directory.Exists(_o.Folder))
            {
                Console.Error.WriteLine($"{_o.Folder} does not exist");
                return;
            }
            if (!IsFullPath(_o.Folder))
            {
                Console.Error.WriteLine($"{_o.Folder} should not be a relative path");
                return;
            }
            
            _database = !_o.Generate
                ? JsonConvert.DeserializeObject<ConcurrentDictionary<string, HashSize>>(File.ReadAllText(_o.Json))
                : new ConcurrentDictionary<string, HashSize>();
            var getFilesBlock = new TransformBlock<string, SourceFile>(filePath =>
                new SourceFile(filePath.Remove(0, _o.Folder.Length),
                    new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024,
                        FileOptions.Asynchronous))); //Only lets one thread do this at a time.

            var hashFilesBlock = new TransformBlock<SourceFile, TargetFile>(HashFile,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount, //We can multi-thread this part.
                    BoundedCapacity = Environment.ProcessorCount
                }); //Only allow 50 byte[]'s to be waiting in the queue. It will unblock getFilesBlock once there is room.

            var checkFilesBlock = new TransformBlock<SourceFile, CheckedFile>(CheckFile,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount, //We can multi-thread this part.
                    BoundedCapacity = Environment.ProcessorCount
                }); //Only allow 50 byte[]'s to be waiting in the queue. It will unblock getFilesBlock once there is room.

            var writeHashedFiles = new ActionBlock<TargetFile>(WriteHashedFile,
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = Environment.ProcessorCount
                }); //MaxDegreeOfParallelism defaults to 1 so we don't need to specifiy it.

            var writeCheckedFiles = new ActionBlock<CheckedFile>(WriteCheckedFile,
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = Environment.ProcessorCount
                }); //MaxDegreeOfParallelism defaults to 1 so we don't need to specifiy it.

            //Link the blocks together.
            if (_o.Generate)
                getFilesBlock.LinkTo(hashFilesBlock, new DataflowLinkOptions {PropagateCompletion = true});
            else
                getFilesBlock.LinkTo(checkFilesBlock, new DataflowLinkOptions {PropagateCompletion = true});
            hashFilesBlock.LinkTo(writeHashedFiles, new DataflowLinkOptions {PropagateCompletion = true});
            checkFilesBlock.LinkTo(writeCheckedFiles, new DataflowLinkOptions {PropagateCompletion = true});

            //Queue the work for the first block.
            foreach (var filePath in Directory.EnumerateFiles(_o.Folder, "*", SearchOption.AllDirectories))
            {
                await getFilesBlock.SendAsync(filePath).ConfigureAwait(false);
            }

            //Tell the first block we are done adding files.
            getFilesBlock.Complete();

            //Wait for the last block to finish processing its last item.

            if (_o.Generate)
            {
                await writeHashedFiles.Completion.ConfigureAwait(false);
                var output = JsonConvert.SerializeObject(_database, Formatting.Indented);
                File.WriteAllText(_o.Json, output);
            }
            else
            {
                await writeCheckedFiles.Completion.ConfigureAwait(false);
                if (_database.Count > 0 || Issues.Count > 0)
                {
                    foreach (var kv in _database)
                    {
                        Issues.TryAdd(kv.Key, ("Source file not found", kv.Value.Hash, kv.Value.Size));
                    }

                    foreach (var kv in Issues)
                    {
                        Console.WriteLine($"{kv.Key} => Size: {kv.Value.size}, Hash: {kv.Value.hash} => {kv.Value.message}");
                    }
                }
                else
                {
                    Console.WriteLine("Everything matches perfectly.");
                }
            }
        }

        private static void WriteCheckedFile(CheckedFile obj)
        {
            if (!string.IsNullOrEmpty(obj.Message))
            {
                Issues.TryAdd(obj.FilePath, (obj.Message, obj.Hash, obj.Size));
            }
        }

        private static async Task<TargetFile> HashFile(SourceFile dto)
        {
            var hash = await xxHash64.ComputeHashAsync(dto.Data);
            dto.Data.Dispose();
            var path = dto.FilePath[0] == '\\' ? dto.FilePath.Substring(1) : dto.FilePath;

            if(_o.Verbose)
                Console.WriteLine($"{path} => Size: {dto.Size}, Hash: {hash}");
            return new TargetFile(path, hash, dto.Size);
        }

        private static void WriteHashedFile(TargetFile arg)
        {
            _database.TryAdd(arg.FilePath, new HashSize(arg.Hash, arg.Size));
        }

        private static async Task<CheckedFile> CheckFile(SourceFile file)
        {
            var message = string.Empty;
            var path = file.FilePath[0] == '\\' ? file.FilePath.Substring(1) : file.FilePath;
            var hash = ulong.MinValue;
            if (!_database.TryRemove(path, out var source))
            {
                message = "Did not exist in the source directory";
            }

            if (file.Size != source.Size)
            {
                message = $"Size does not match (source was {source.Size})";
            }
            else
            {
                hash = await xxHash64.ComputeHashAsync(file.Data);

                if (source.Hash != hash)
                {
                    message = $"Contents do not match (source hash was {source.Hash})";
                }
            }

            file.Data.Dispose();

            if (!string.IsNullOrEmpty(message)) return new CheckedFile(path, message, hash, file.Size);
            if (_o.Verbose)
            {
                Console.WriteLine($"{path} => Size: {file.Size}, Hash: {hash} => OK!");
            }

            return new CheckedFile(path, message, hash, file.Size);
        }
        
        private static bool IsFullPath(string path) {
            return !string.IsNullOrWhiteSpace(path)
                   && path.IndexOfAny(Path.GetInvalidPathChars().ToArray()) == -1
                   && Path.IsPathRooted(path)
                   && !Path.GetPathRoot(path).Equals(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal);
        }
    }
}