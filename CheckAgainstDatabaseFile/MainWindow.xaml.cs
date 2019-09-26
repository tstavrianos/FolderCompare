using System.Windows;
using Microsoft.Win32;

namespace CheckAgainstDatabaseFile
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public sealed partial class MainWindow
    {
        private string _selectedFolder;
        private string _selectedFile;
        public MainWindow()
        {
            InitializeComponent();
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
    }
}