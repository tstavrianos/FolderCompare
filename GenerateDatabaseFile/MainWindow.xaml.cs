using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Newtonsoft.Json;
using Standart.Hash.xxHash;

namespace GenerateDatabaseFile
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        private readonly ConcurrentDictionary<string, DBEntry> _database = new ConcurrentDictionary<string, DBEntry>();

        private class DBEntry
        {
            public DateTime LastWrite { get; }
            public ulong Hash { get; }
            public long Size { get; }

            public DBEntry(DateTime lastWrite, ulong hash, long size)
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

        private string _selectedFolder;
        private string _selectedFile;

        private int _textLength;
        public int FilesFound
        {
            get => this._textLength;
            set => this._UpdateField(ref this._textLength, value);
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

        private void BrowseFolder()
        {
            using (var d = new WPFFolderBrowser.WpfFolderBrowserDialog("Select Folder"))
            {
                if (d.ShowDialog(this) != true) return;
                this._selectedFolder = d.FileName;
                this.SelectedFolder.Dispatcher?.Invoke(() => this.SelectedFolder.Text = d.FileName);
            }
        }

        private void BrowseFile()
        {
            var d = new SaveFileDialog();
            if (d.ShowDialog(this) != true) return;
            this._selectedFile = d.FileName;
            this.SelectedFile.Dispatcher?.Invoke(() => this.SelectedFile.Text = d.FileName);
        }

        private async Task Generate()
        {
            if (string.IsNullOrEmpty(this._selectedFile) || string.IsNullOrEmpty(this._selectedFolder) ||
                !Directory.Exists(this._selectedFolder)) return;
            this.FilesFound = 0;
            this.ControlsEnabled = false;

            var getFilesBlock = new TransformBlock<FileInfo, SourceFile>(file =>
                new SourceFile(file.FullName.Remove(0, this._selectedFolder.Length),
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

            var di = new DirectoryInfo(this._selectedFolder);
            foreach (var filePath in di.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                await getFilesBlock.SendAsync(filePath).ConfigureAwait(false);
                //getFilesBlock.Post(filePath);
            }

            getFilesBlock.Complete();
            await writeHashedFiles.Completion.ConfigureAwait(false);
            var output = JsonConvert.SerializeObject(this._database, Formatting.Indented);
            File.WriteAllText(this._selectedFile, output);

            this.ControlsEnabled = true;
        }

        private void _UpdateField<T>(ref T field, T newValue, [CallerMemberName] string propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, newValue))
            {
                field = newValue;
                this._OnPropertyChanged(propertyName);
            }
        }

        private void _OnPropertyChanged(string propertyName)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            /*switch (propertyName)
            {
                // you can add "case nameof(...):" cases here to handle
                // specific property changes, rather than polluting the
                // property setters themselves
            }*/
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
            this._database.TryAdd(arg.FilePath, new DBEntry(arg.LastWrite, arg.Hash, arg.Size));
        }


        private void BrowseFolderCommand(object sender, RoutedEventArgs e)
        {
            BrowseFolder();
        }

        private void BrowseFileCommand(object sender, RoutedEventArgs e)
        {
            BrowseFile();
        }

        private async void GenerateCommand(object sender, RoutedEventArgs e)
        {
            await Generate();
        }
    }
}