using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WPFFolderBrowser.Interop;

namespace WPFFolderBrowser
{
    public sealed class WpfFolderBrowserDialog : IDisposable //, IDialogControlHost
    {
        private readonly Collection<string> _fileNames;
        internal NativeDialogShowState ShowState = NativeDialogShowState.PreShow;

        private IFileDialog _nativeDialog;
        private bool? _canceled;
        private Window _parentWindow;

        private const string IllegalPropertyChangeString = " cannot be changed while dialog is showing";

        #region Constructors
        
        public WpfFolderBrowserDialog()
        {
            this._fileNames = new Collection<string>();
        }

        public WpfFolderBrowserDialog(string title)
        {
            this._fileNames = new Collection<string>();
            this._title = title;
        }

        #endregion

        // Template method to allow derived dialog to create actual
        // specific COM coclass (e.g. FileOpenDialog or FileSaveDialog)
        private NativeFileOpenDialog _openDialogCoClass;


        internal IFileDialog GetNativeFileDialog()
        {
            Debug.Assert(this._openDialogCoClass != null,
                "Must call Initialize() before fetching dialog interface");
            return this._openDialogCoClass;
        }

        internal void InitializeNativeFileDialog()
        {
            this._openDialogCoClass = new NativeFileOpenDialog();
        }

        internal void CleanUpNativeFileDialog()
        {
            if (this._openDialogCoClass != null)
                Marshal.ReleaseComObject(this._openDialogCoClass);
        }

        internal void PopulateWithFileNames(Collection<string> names)
        {
            //IShellItem directory;
            if (names == null) return;
            this._openDialogCoClass.GetResults(out var resultsArray);
            resultsArray.GetCount(out var count);

            names.Clear();
            for (var i = 0; i < count; i++)
                names.Add(this.GetFileNameFromShellItem(this.GetShellItemAt(resultsArray, i)));

            if (count > 0)
            {
                this.FileName = names[0];
            }
        }

        internal NativeMethods.FOS GetDerivedOptionFlags(NativeMethods.FOS flags)
        {
            
                flags |= NativeMethods.FOS.FOS_PICKFOLDERS;
            // TODO: other flags

            return flags;
        }
    

        #region Public API

        private string _title;
        public string Title
        {
            get { return this._title; }
            set 
            { 
                this._title = value;
                if (this.NativeDialogShowing)
                    this._nativeDialog.SetTitle(value);
            }
        }

        // TODO: implement AddExtension
        internal bool AddExtension { get; set; }

        // This is the first of many properties that are backed by the FOS_*
        // bitflag options set with IFileDialog.SetOptions(). SetOptions() fails
        // if called while dialog is showing (e.g. from a callback)
        private bool _checkFileExists;
        internal bool CheckFileExists
        {
            get { return this._checkFileExists; }
            set 
            {
                this.ThrowIfDialogShowing("CheckFileExists" + IllegalPropertyChangeString);
                this._checkFileExists = value; 
            }
        }

        private bool _checkPathExists;
        internal bool CheckPathExists
        {
            get { return this._checkPathExists; }
            set 
            {
                this.ThrowIfDialogShowing("CheckPathExists" + IllegalPropertyChangeString);
                this._checkPathExists = value;
            }
        }

        private bool _checkValidNames;
        internal bool CheckValidNames
        {
            get { return this._checkValidNames; }
            set 
            {
                this.ThrowIfDialogShowing("CheckValidNames" + IllegalPropertyChangeString);
                this._checkValidNames = value; 
            }
        }

        private bool _checkReadOnly;
        internal bool CheckReadOnly
        {
            get { return this._checkReadOnly; }
            set 
            {
                this.ThrowIfDialogShowing("CheckReadOnly" + IllegalPropertyChangeString);
                this._checkReadOnly = value; 
            }
        }

        // TODO: Bizzare semantics bug here, needs resolution
        // semantics of FOS_NOCHANGEDIR, as the specs indicate that it has changed;
        // if so, we'll need to cache this ourselves
        private bool _restoreDirectory;
        internal bool RestoreDirectory
        {
            get { return this._restoreDirectory; }
            set 
            {
                this.ThrowIfDialogShowing("RestoreDirectory" + IllegalPropertyChangeString);
                this._restoreDirectory = value; 
            }
        }

        private bool _showPlacesList = true;
        public bool ShowPlacesList
        {

            get { return this._showPlacesList; }
            set 
            {
                this.ThrowIfDialogShowing("ShowPlacesList" + IllegalPropertyChangeString);
                this._showPlacesList = value; 
            }
        }

        private bool _addToMruList = true;
        public bool AddToMruList
        {
            get { return this._addToMruList; }
            set 
            {
                this.ThrowIfDialogShowing("AddToMruList" + IllegalPropertyChangeString);
                this._addToMruList = value; 
            }
        }

        private bool _showHiddenItems;
        public bool ShowHiddenItems
        {
            get { return this._showHiddenItems; }
            set 
            {
                this.ThrowIfDialogShowing("ShowHiddenItems" + IllegalPropertyChangeString);
                this._showHiddenItems = value; 
            }
        }

        // TODO: Implement property editing
        internal bool AllowPropertyEditing { get; set; }

        private bool _dereferenceLinks;
        public bool DereferenceLinks
        {
            get { return this._dereferenceLinks; }
            set 
            {
                this.ThrowIfDialogShowing("DereferenceLinks" + IllegalPropertyChangeString);
                this._dereferenceLinks = value; }
        }

        private string _fileName;
        public string FileName
        {
            get
            {
                this.CheckFileNamesAvailable();
                if (this._fileNames.Count > 1)
                    throw new InvalidOperationException("Multiple files selected - the FileNames property should be used instead");
                this._fileName = this._fileNames[0];
                return this._fileNames[0];
            }
            set
            {
                this._fileName = value;
            }
        }

        public string InitialDirectory { get; set; }

        public bool? ShowDialog(Window owner)
        {
            this._parentWindow = owner;
            return this.ShowDialog();
        }

        public bool? ShowDialog()
        {
            bool? result;

            try
            {
                // Fetch derived native dialog (i.e. Save or Open)

                this.InitializeNativeFileDialog();
                this._nativeDialog = this.GetNativeFileDialog();

                // Process custom controls, and validate overall state
                this.ProcessControls();
                this.ValidateCurrentDialogState();

                // Apply outer properties to native dialog instance
                this.ApplyNativeSettings(this._nativeDialog);

                // Show dialog
                this.ShowState = NativeDialogShowState.Showing;
                var hresult = this._nativeDialog.Show(this.GetHandleFromWindow(this._parentWindow));
                this.ShowState = NativeDialogShowState.Closed;

                // Create return information
                if (ErrorHelper.Matches(hresult, Win32ErrorCode.ERROR_CANCELLED))
                {
                    this._canceled = true;
                    this._fileNames.Clear();
                }
                else
                {
                    this._canceled = false;

                    // Populate filenames - though only if user didn't cancel
                    this.PopulateWithFileNames(this._fileNames);
                }
                result = !this._canceled.Value;
            }
            catch
            {
                //If Vista Style dialog is unavailable, fall back to Windows Forms

                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    SelectedPath = this._fileName, ShowNewFolderButton = true, Description = this.Title
                };

                result = (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK);
                if (result == true)
                {
                    this._canceled = false;
                    this._fileNames.Clear();
                    this._fileNames.Add(dialog.SelectedPath);
                }
                else
                {
                    this._fileNames.Clear();
                    this._canceled = true;
                }
            }
            finally
            {
                this.CleanUpNativeFileDialog();
                this.ShowState = NativeDialogShowState.Closed;
            }
            return result;
        }
        

        #endregion

        #region Configuration

        private void ApplyNativeSettings(IFileDialog dialog)
        {
            Debug.Assert(dialog != null, "No dialog instance to configure");

            if (this._parentWindow == null)
                this._parentWindow = Helpers.GetDefaultOwnerWindow();

            // Apply option bitflags
            dialog.SetOptions(this.CalculateNativeDialogOptionFlags());

            // Other property sets
            dialog.SetTitle(this._title);

            // TODO: Implement other property sets

            var directory = (string.IsNullOrEmpty(this._fileName)) ? this.InitialDirectory : System.IO.Path.GetDirectoryName(this._fileName);


            if (directory != null)
            {
                SHCreateItemFromParsingName(directory, IntPtr.Zero, new Guid(IIDGuid.IShellItem), out var folder);

                if (folder != null)
                    dialog.SetFolder(folder);
            }


            if (string.IsNullOrEmpty(this._fileName)) return;
            var name = System.IO.Path.GetFileName(this._fileName);
            dialog.SetFileName(name);
        }

        private NativeMethods.FOS CalculateNativeDialogOptionFlags()
        {
            // We start with only a few flags set by default, then go from there based
            // on the current state of the managed dialog's property values
            var flags = 
                NativeMethods.FOS.FOS_NOTESTFILECREATE
                | NativeMethods.FOS.FOS_FORCEFILESYSTEM;

            // Call to derived (concrete) dialog to set dialog-specific flags
            flags = this.GetDerivedOptionFlags(flags);

            // Apply other optional flags
            if (this._checkFileExists)
                flags |= NativeMethods.FOS.FOS_FILEMUSTEXIST;
            if (this._checkPathExists)
                flags |= NativeMethods.FOS.FOS_PATHMUSTEXIST;
            if (!this._checkValidNames)
                flags |= NativeMethods.FOS.FOS_NOVALIDATE;
            if (!this._checkReadOnly)
                flags |= NativeMethods.FOS.FOS_NOREADONLYRETURN;
            if (this._restoreDirectory)
                flags |= NativeMethods.FOS.FOS_NOCHANGEDIR;
            if (!this._showPlacesList)
                flags |= NativeMethods.FOS.FOS_HIDEPINNEDPLACES;
            if (!this._addToMruList)
                flags |= NativeMethods.FOS.FOS_DONTADDTORECENT;
            if (this._showHiddenItems)
                flags |= NativeMethods.FOS.FOS_FORCESHOWHIDDEN;
            if (!this._dereferenceLinks)
                flags |= NativeMethods.FOS.FOS_NODEREFERENCELINKS;
            return flags;
        }

        private void ValidateCurrentDialogState()
        {
            // TODO: Perform validation - both cross-property and pseudo-controls
        }

        private void ProcessControls()
        {
            // TODO: Sort controls if necesarry - COM API might not require it, however
        }

        #endregion

        //#region IDialogControlHost Members

        //bool IDialogControlHost.IsCollectionChangeAllowed()
        //{
        //    throw new Exception("The method or operation is not implemented.");
        //}

        //void IDialogControlHost.ApplyCollectionChanged()
        //{
        //    throw new Exception("The method or operation is not implemented.");
        //}

        //bool IDialogControlHost.IsControlPropertyChangeAllowed(string propertyName, DialogControl control)
        //{
        //    throw new Exception("The method or operation is not implemented.");
        //}

        //void IDialogControlHost.ApplyControlPropertyChange(string propertyName, DialogControl control)
        //{
        //    throw new Exception("The method or operation is not implemented.");
        //}

        //#endregion

        #region Helpers

        private void CheckFileNamesAvailable()
        {
            if (this.ShowState != NativeDialogShowState.Closed)
                throw new InvalidOperationException("Filename not available - dialog has not closed yet");
            if (this._canceled.GetValueOrDefault())
                throw new InvalidOperationException("Filename not available - dialog was canceled");
            Debug.Assert(this._fileNames.Count != 0,
                    "FileNames empty - shouldn't happen dialog unless dialog canceled or not yet shown");
        }

        private IntPtr GetHandleFromWindow(Window window)
        {
            if (window == null)
                return NativeMethods.NO_PARENT;
            return (new WindowInteropHelper(window)).Handle;
        }

        private bool IsOptionSet(IFileDialog dialog, NativeMethods.FOS flag)
        {
            var currentFlags = this.GetCurrentOptionFlags(dialog);

            return (currentFlags & flag) == flag;
        }

        internal NativeMethods.FOS GetCurrentOptionFlags(IFileDialog dialog)
        {
            dialog.GetOptions(out var currentFlags);
            return currentFlags;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
        [In, MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        [In] IntPtr pbc,
        [In, MarshalAs(UnmanagedType.LPStruct)] Guid iIdIShellItem,
        [Out, MarshalAs(UnmanagedType.Interface, IidParameterIndex = 2)] out IShellItem iShellItem);

        #endregion

        #region Helpers

        private bool NativeDialogShowing
        {
            get
            {
                return (this._nativeDialog != null)
                    && (this.ShowState == NativeDialogShowState.Showing ||
                    this.ShowState == NativeDialogShowState.Closing);
            }
        }

        internal string GetFileNameFromShellItem(IShellItem item)
        {
            item.GetDisplayName(NativeMethods.SIGDN.SIGDN_DESKTOPABSOLUTEPARSING, out var filename);
            return filename;
        }

        internal IShellItem GetShellItemAt(IShellItemArray array, int i)
        {
            var index = (uint)i;
            array.GetItemAt(index, out var result);
            return result;
        }

        private void ThrowIfDialogShowing(string message)
        {
            if (this.NativeDialogShowing)
                throw new NotSupportedException(message);
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            //throw new Exception("The method or operation is not implemented.");
        }

        #endregion

        #region Event handling members

        private void OnFileOk(CancelEventArgs e)
        {
            //CancelEventHandler handler = FileOk;
            //if (handler != null)
            //{
            //    handler(this, e);
            //}
        }

        //protected virtual void OnFolderChanging(CommonFileDialogFolderChangeEventArgs e)
        //{
        //    //EventHandler<CommonFileDialogFolderChangeEventArgs> handler = FolderChanging;
        //    //if (handler != null)
        //    //{
        //    //    handler(this, e);
        //    //}
        //}

        private void OnFolderChanged(EventArgs e)
        {
            //EventHandler handler = FolderChanged;
            //if (handler != null)
            //{
            //    handler(this, e);
            //}
        }

        private void OnSelectionChanged(EventArgs e)
        {
            //EventHandler handler = SelectionChanged;
            //if (handler != null)
            //{
            //    handler(this, e);
            //}
        }

        private void OnFileTypeChanged(EventArgs e)
        {
            //EventHandler handler = FileTypeChanged;
            //if (handler != null)
            //{
            //    handler(this, e);
            //}   
        }

        private void OnOpening(EventArgs e)
        {
            //EventHandler handler = Opening;
            //if (handler != null)
            //{
            //    handler(this, e);
            //}
        }

        #endregion

        #region NativeDialogEventSink Nested Class

        private class NativeDialogEventSink : IFileDialogEvents //, IFileDialogControlEvents
        {
            private WpfFolderBrowserDialog _parent;
            private bool _firstFolderChanged = true; 

            public NativeDialogEventSink(WpfFolderBrowserDialog commonDialog)
            {
                this._parent = commonDialog;
            }

            public uint Cookie { get; set; }

            public HRESULT OnFileOk(IFileDialog pfd)
            {
                var args = new CancelEventArgs();
                this._parent.OnFileOk(args);
                return (args.Cancel ? HRESULT.S_FALSE : HRESULT.S_OK);
            }

            public HRESULT OnFolderChanging(IFileDialog pfd, IShellItem psiFolder)
            {
                return HRESULT.S_OK;
                //CommonFileDialogFolderChangeEventArgs args =
                //    new CommonFileDialogFolderChangeEventArgs(parent.GetFileNameFromShellItem(psiFolder));
                //if (!firstFolderChanged)
                //    parent.OnFolderChanging(args);
                //return (args.Cancel ? HRESULT.S_FALSE : HRESULT.S_OK);
            }

            public void OnFolderChange(IFileDialog pfd)
            {
                if (this._firstFolderChanged)
                {
                    this._firstFolderChanged = false;
                    this._parent.OnOpening(EventArgs.Empty);
                }
                else
                    this._parent.OnFolderChanged(EventArgs.Empty);
            }

            public void OnSelectionChange(IFileDialog pfd)
            {
                this._parent.OnSelectionChanged(EventArgs.Empty);
            }

            public void OnShareViolation(IFileDialog pfd, IShellItem psi, out NativeMethods.FDE_SHAREVIOLATION_RESPONSE pResponse)
            {
                // Do nothing: we will ignore share violations, and don't register
                // for them, so this method should never be called
                pResponse = NativeMethods.FDE_SHAREVIOLATION_RESPONSE.FDESVR_ACCEPT;
            }

            public void OnTypeChange(IFileDialog pfd)
            {
                this._parent.OnFileTypeChanged(EventArgs.Empty);
            }

            public void OnOverwrite(IFileDialog pfd, IShellItem psi, out NativeMethods.FDE_OVERWRITE_RESPONSE pResponse)
            {
                // TODO: Implement overwrite notification support
                pResponse = NativeMethods.FDE_OVERWRITE_RESPONSE.FDEOR_ACCEPT;
            }
            //public void OnItemSelected(IFileDialogCustomize pfdc, int dwIDCtl, int dwIDItem)
            //{
            //    // TODO: Implement OnItemSelected
            //}

            //public void OnButtonClicked(IFileDialogCustomize pfdc, int dwIDCtl)
            //{
            //    // TODO: Implement OnButtonClicked
            //}

            //public void OnCheckButtonToggled(IFileDialogCustomize pfdc, int dwIDCtl, bool bChecked)
            //{
            //    // TODO: Implement OnCheckButtonToggled
            //}

            //public void OnControlActivating(IFileDialogCustomize pfdc, int dwIDCtl)
            //{
            //    // TODO: Implement OnControlActivating
            //}
        }

        #endregion
    }


}