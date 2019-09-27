using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using Newtonsoft.Json;
using Standart.Hash.xxHash;
// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable AccessToDisposedClosure

namespace GenerateDatabaseFile
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public sealed partial class MainWindow : INotifyPropertyChanged
    {
        public string SelectedDirectory { get; set; }
        public string SelectedFile { get; set; }
        private readonly ConcurrentDictionary<string, DbEntry> _database = new ConcurrentDictionary<string, DbEntry>();

        private sealed class DbEntry
        {
            public DateTime LastWrite { get; }
            public ulong Hash { get; }
            public long Size { get; }

            public DbEntry(DateTime lastWrite, ulong hash, long size)
            {
                this.LastWrite = lastWrite;
                this.Hash = hash;
                this.Size = size;
            }
        }

        private sealed class SourceFile
        {
            public SourceFile(string filePath, Stream data, DateTime lastWrite)
            {
                this.FilePath = filePath;
                this.Data = data;
                this.Size = data.Length;
                this.LastWrite = lastWrite;
            }

            public string FilePath { get; }
            public Stream Data { get; }
            public long Size { get; }
            public DateTime LastWrite { get; }
        }

        private sealed class TargetFile
        {
            public TargetFile(string filePath, ulong hash, long size, DateTime lastWrite)
            {
                this.FilePath = filePath;
                this.Hash = hash;
                this.Size = size;
                this.LastWrite = lastWrite;
            }

            public string FilePath { get; }
            public ulong Hash { get; }
            public long Size { get; }
            public DateTime LastWrite { get; }
        }

        private int _filesFound;
        public int FilesFound
        {
            get => this._filesFound;
            set => this._UpdateField(ref this._filesFound, value);
        }

        private bool _controlsEnabled = true;
        public bool ControlsEnabled
        {
            get => this._controlsEnabled;
            set => this._UpdateField(ref this._controlsEnabled, value);
        }

        private bool? _hashFiles = false;
        public bool? HashFiles
        {
            get => this._hashFiles;
            set => this._UpdateField(ref this._hashFiles, value);
        }

        public MainWindow()
        {
            this.InitializeComponent();
        }

        private async Task Generate()
        {
            if (string.IsNullOrEmpty(this.SelectedFile) || string.IsNullOrEmpty(this.SelectedDirectory) ||
                !Directory.Exists(this.SelectedDirectory)) return;
            this.FilesFound = 0;
            this.ControlsEnabled = false;

            var getFilesBlock = new TransformBlock<FileInfo, SourceFile>(file =>
                new SourceFile(file.FullName.Remove(0, this.SelectedDirectory.Length),
                    file.OpenRead(), file.LastWriteTimeUtc)); //Only lets one thread do this at a time.

            var hashFilesBlock = new TransformBlock<SourceFile, TargetFile>(this.HashFile,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount, //We can multi-thread this part.
                    BoundedCapacity = Environment.ProcessorCount
                }); //Only allow 50 byte[]'s to be waiting in the queue. It will unblock getFilesBlock once there is room.

            var writeHashedFiles = new ActionBlock<TargetFile>(this.WriteHashedFile,
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = Environment.ProcessorCount
                }); //MaxDegreeOfParallelism defaults to 1 so we don't need to specifiy it.

            getFilesBlock.LinkTo(hashFilesBlock, new DataflowLinkOptions { PropagateCompletion = true });
            hashFilesBlock.LinkTo(writeHashedFiles, new DataflowLinkOptions { PropagateCompletion = true });

            var di = new DirectoryInfo(this.SelectedDirectory);
            foreach (var filePath in di.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                await getFilesBlock.SendAsync(filePath).ConfigureAwait(false);
            }

            getFilesBlock.Complete();
            await writeHashedFiles.Completion.ConfigureAwait(false);
            var output = JsonConvert.SerializeObject(this._database, Formatting.Indented);
            File.WriteAllText(this.SelectedFile, output);

            this.ControlsEnabled = true;
        }

        private void _UpdateField<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, newValue)) return;
            field = newValue;
            this._OnPropertyChanged(propertyName);
        }

        private void _OnPropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private TargetFile HashFile(SourceFile dto)
        {
            ulong hash = 0;
            if (this.HashFiles == true)
                hash = xxHash64.ComputeHash(dto.Data);

            dto.Data.Dispose();
            var path = dto.FilePath[0] == '\\' ? dto.FilePath.Substring(1) : dto.FilePath;

            return new TargetFile(path, hash, dto.Size, dto.LastWrite);
        }

        private void WriteHashedFile(TargetFile arg)
        {
            this.FilesFound++;
            this._database.TryAdd(arg.FilePath, new DbEntry(arg.LastWrite, arg.Hash, arg.Size));
        }

        private async void GenerateCommand(object sender, RoutedEventArgs e)
        {
            await this.Generate();
        }
    }
}