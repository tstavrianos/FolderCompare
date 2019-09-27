using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;
using Newtonsoft.Json;
using Standart.Hash.xxHash;
// ReSharper disable AccessToDisposedClosure

namespace CheckAgainstDatabaseFile
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public sealed partial class MainWindow: INotifyPropertyChanged
    {
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
            public TargetFile(string nameL, DateTime? dateL, long? sizeL, ulong? hashL, string nameR, DateTime? dateR, long? sizeR, ulong? hashR)
            {
                this.NameL = nameL;
                this.DateL = dateL;
                this.SizeL = sizeL;
                this.HashL = hashL;
                this.NameR = nameR;
                this.DateR = dateR;
                this.SizeR = sizeR;
                this.HashR = hashR;
            }

            public string NameL { get; }
            public DateTime? DateL { get; }
            public long? SizeL { get; }
            public ulong? HashL { get; }

            public string NameR { get; }
            public DateTime? DateR { get; }
            public long? SizeR { get; }
            public ulong? HashR { get; }
        }

        private string _selectedFolder;
        private string _selectedFile;
        private AsyncObservableCollection<Entry> Entries { get; }
        public ICollectionView<Entry> EntriesView { get; set; }
        private ConcurrentDictionary<string, DbEntry> _database;

        private bool? _hashFiles = false;
        public bool? HashFiles
        {
            get => this._hashFiles;
            set => this._UpdateField(ref this._hashFiles, value);
        }
        private bool? _ignoreDates = false;
        public bool? IgnoreDates
        {
            get => this._ignoreDates;
            set => this._UpdateField(ref this._ignoreDates, value);
        }
        private bool? _missing = false;
        public bool? Missing
        {
            get => this._missing;
            set => this._UpdateField(ref this._missing, value);
        }
        private bool? _correct = false;
        public bool? Correct
        {
            get => this._correct;
            set => this._UpdateField(ref this._correct, value);
        }
        private bool? _incorrect = false;
        public bool? Incorrect
        {
            get => this._incorrect;
            set => this._UpdateField(ref this._incorrect, value);
        }
        private bool? _extra = false;
        public bool? Extra
        {
            get => this._extra;
            set => this._UpdateField(ref this._extra, value);
        }
        private bool _controlsEnabled = true;
        public bool ControlsEnabled
        {
            get => this._controlsEnabled;
            set => this._UpdateField(ref this._controlsEnabled, value);
        }
        
        public event PropertyChangedEventHandler PropertyChanged;

        public MainWindow()
        {
            this.Entries = new AsyncObservableCollection<Entry>();

            this.EntriesView = new MyCollectionViewGeneric<Entry>(CollectionViewSource.GetDefaultView(this.Entries));
            this.EntriesView.Filter = this.EntryFilter;

            this.InitializeComponent();
        }

        private bool EntryFilter(object obj)
        {
            var e = obj as Entry;

            if (this._missing == true && e.Eq == "=>") return true;
            if (this._extra == true && e.Eq == "<=") return true;
            if (this._incorrect == true && e.Eq == "!=") return true;
            return this._correct == true && e.Eq == "=";
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

        private void BrowseFolder(object sender, RoutedEventArgs e)
        {
            using (var d = new WPFFolderBrowser.WpfFolderBrowserDialog("Select Folder"))
            {
                if (d.ShowDialog(this) != true) return;
                this._selectedFolder = d.FileName;
                this.SelectedFolder.Dispatcher.Invoke(() => this.SelectedFolder.Text = d.FileName);
            }
        }

        private void BrowseFile(object sender, RoutedEventArgs e)
        {
            var d = new OpenFileDialog();
            if (d.ShowDialog(this) != true) return;
            this._selectedFile = d.FileName;
            this.SelectedFile.Dispatcher.Invoke(() => this.SelectedFile.Text = d.FileName);
        }
        
        private void Refresh(object sender, RoutedEventArgs e)
        {
            this.Dispatcher?.Invoke(() => this.EntriesView.Refresh());
        }

        private async void Check(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(this._selectedFile) || string.IsNullOrEmpty(this._selectedFolder) ||
                !Directory.Exists(this._selectedFolder) || !File.Exists(this._selectedFile)) return;
            this.ControlsEnabled = false;
            this.Entries.Clear();
            _database = JsonConvert.DeserializeObject<ConcurrentDictionary<string, DbEntry>>(File.ReadAllText(this._selectedFile));
            var getFilesBlock = new TransformBlock<FileInfo, SourceFile>(file =>
                new SourceFile(file.FullName.Remove(0, this._selectedFolder.Length),
                    file.OpenRead(), file.LastWriteTimeUtc)); //Only lets one thread do this at a time.

            var checkFilesBlock = new TransformBlock<SourceFile, TargetFile>(CheckFile,
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount, //We can multi-thread this part.
                    BoundedCapacity = Environment.ProcessorCount
                }); //Only allow 50 byte[]'s to be waiting in the queue. It will unblock getFilesBlock once there is room.
            
            var writeCheckedFiles = new ActionBlock<TargetFile>(WriteCheckedFile,
                new ExecutionDataflowBlockOptions
                {
                    BoundedCapacity = Environment.ProcessorCount
                }); //MaxDegreeOfParallelism defaults to 1 so we don't need to specifiy it.

            getFilesBlock.LinkTo(checkFilesBlock, new DataflowLinkOptions { PropagateCompletion = true });
            checkFilesBlock.LinkTo(writeCheckedFiles, new DataflowLinkOptions { PropagateCompletion = true });

            var di = new DirectoryInfo(this._selectedFolder);
            foreach (var filePath in di.EnumerateFiles("*", SearchOption.AllDirectories))
            {
                await getFilesBlock.SendAsync(filePath).ConfigureAwait(false);
            }

            getFilesBlock.Complete();
            await writeCheckedFiles.Completion.ConfigureAwait(false);

            foreach (var e1 in this._database.Select(kv => new Entry(kv.Key, null, kv.Value.Size, null, kv.Value.LastWrite, null, kv.Value.Hash, null)))
            {
                this.Entries.Add(e1);
            }

            this.ControlsEnabled = true;
        }
        
        private async Task<TargetFile> CheckFile(SourceFile file)
        {
            var pathR = file.FilePath[0] == '\\' ? file.FilePath.Substring(1) : file.FilePath;
            ulong? hashR = 0;
            DateTime? lastWriteR = file.LastWrite;
            long? sizeR = file.Size;

            string pathL = null;
            ulong? hashL = null; 
            long? sizeL = null; 
            DateTime? lastWriteL = null; 
            if (this._database.TryRemove(pathR, out var entry))
            {
                pathL = pathR;
                hashL = entry.Hash;
                sizeL = entry.Size;
                lastWriteL = entry.LastWrite;
            }

            if (pathL != null && hashL.Value != 0 && this._hashFiles == true)
            {
                hashR = await xxHash64.ComputeHashAsync(file.Data);
            }

            if (this._hashFiles != true)
            {
                hashL = 0;
                hashR = 0;
            }

            if (this._ignoreDates == true)
            {
                lastWriteL = lastWriteR;
            }
            file.Data.Dispose();
            
            return new TargetFile(pathL, lastWriteL, sizeL, hashL, pathR, lastWriteR, sizeR, hashR);
        }
        
        private void WriteCheckedFile(TargetFile obj)
        {
            var e = new Entry(obj.NameL, obj.NameR, obj.SizeL, obj.SizeR, obj.DateL, obj.DateR, obj.HashL, obj.HashR);
            this.Entries.Add(e);
        }
    }
}