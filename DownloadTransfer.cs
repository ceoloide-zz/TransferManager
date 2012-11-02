using System;
using Microsoft.Phone.BackgroundTransfer;
using System.IO.IsolatedStorage;
using System.Data.Linq.Mapping;
using System.Collections.Generic;
using System.Data.Linq;
using System.ComponentModel;
using System.Windows;

namespace TransferManager
{
    /// <summary>
    /// 
    /// </summary>
    [Table]
    public partial class DownloadTransfer : AbstractTransfer
    {
        // Version column aids update performance.
        [Column(IsVersion = true)]
        private Binary _version;

        private int _UID;
        /// <summary>
        /// Gets or sets the unique ID of the transfer.
        /// </summary>
        [Column(IsPrimaryKey = true, IsDbGenerated = true, DbType = "INT NOT NULL Identity", CanBeNull = false, AutoSync = AutoSync.OnInsert)]
        public int UID
        {
            get { return _UID; }
            set
            {
                if (_UID != value)
                {
                    NotifyPropertyChanging("UID");
                    _UID = value;
                    NotifyPropertyChanged("UID");
                }
            }
        }

        /// <summary>
        /// Parameterless constructor.
        /// </summary>
        public DownloadTransfer()
            : base()
        {
            _Method = "GET";
            //_Headers = new KeyValuePair<string, string>();
            _TransferStatus = ExtendedTransferStatus.None;
            _IsIndeterminateTransfer = true;
            _TotalBytesToTransfer = -1;
            _BytesTransferred = 0;
            _RequestId = string.Empty;
        }

        /// <summary>
        /// Checks whether the destination folder of the download exists. If it doesn't
        /// exists, this method creates it.
        /// </summary>
        public void OnBeforeAdd()
        {
            using (IsolatedStorageFile IsoStore = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (!IsoStore.DirectoryExists("shared/transfers" + Path))
                    IsoStore.CreateDirectory("shared/transfers" + Path);
            }
        }

        /// <summary>
        /// This method is called when a transfer has been completed successfully.
        /// </summary>
        public void OnComplete()
        {
            // We need to move the file to its intended location.
            // To avoid blocking the UI, we let a BackgroundWorker do the job and asynchronously
            // inform us of its completion.

            BackgroundWorker Worker = new BackgroundWorker { WorkerReportsProgress = false, WorkerSupportsCancellation = false };
            Worker.DoWork += new DoWorkEventHandler(Worker_DoWork);
            Worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Worker_RunWorkerCompleted);

            Worker.RunWorkerAsync();
        }

        /// <summary>
        /// The DoWork method of the BackgroundWorker used to process the Completed state. This method
        /// moves the downloaded file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            // TODO: What happens if the user closes the application while the BackgroundWorker is still pending?
            // Should it be handled?
            try
            {
                using (IsolatedStorageFile IsoStore = IsolatedStorageFile.GetUserStoreForApplication())
                {
                    // First we ensure that the destination folder is existing
                    if (!IsoStore.DirectoryExists(Path))
                        IsoStore.CreateDirectory(Path);

                    // Then we check whether the downloaded file is existing
                    if (IsoStore.FileExists(TransferLocationUri.OriginalString))
                    {
                        // Then we check whether the destination file is existing and in case we delete it
                        if (IsoStore.FileExists(FilenameWithPath))
                            IsoStore.DeleteFile(FilenameWithPath);

                        // Finally we move the downloaded file to its new location
                        IsoStore.MoveFile(TransferLocationUri.OriginalString, FilenameWithPath);

                        // The job has been done, the result is Completed.
                        e.Result = ExtendedTransferStatus.Completed;
                    }
                    else
                    {
                        // An error occurred during completion, the result is Failed.
                        e.Result = ExtendedTransferStatus.Failed;
                    }
                }
            }
            catch (Exception)
            {
                // An error occurred during completion, the result is Failed.
                e.Result = ExtendedTransferStatus.Failed;
            }
        }

        /// <summary>
        /// Processes the completion of the BackgroundWorker.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">The arguments passed to this method.</param>
        void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if ((ExtendedTransferStatus)e.Result == ExtendedTransferStatus.Completed)
            {
                // We set the BytesReceived to the TotalBytesToReceive
                BytesTransferred = TotalBytesToTransfer;
                TransferProgress = 1;
            }
            TransferStatus = (ExtendedTransferStatus)e.Result;
        }
    
        /// <summary>
        /// Provides an handler for changes of transfer progress. Mainly used to update UI.
        /// Event argument contains BytesReceived / BytesSent that represent current transfer status
        /// and TotalBytesToReceive / TotalBytesToSend that represent the amount to transfer.
        /// 
        /// NOTE: TotalBytesToReceive can be -1 if the amount to receive is unknown.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">The event args.</param>
        void ITransferable.TransferProgressChanged(object sender, BackgroundTransferEventArgs e)
        {
            // If TotalBytesToReceive == -1 the transfer is indeterminate
            IsIndeterminateTransfer = (e.Request.TotalBytesToReceive == -1 && TransferStatus == ExtendedTransferStatus.Transferring);

            TotalBytesToTransfer = e.Request.TotalBytesToReceive;
            BytesTransferred = e.Request.BytesReceived;

            if (!IsIndeterminateTransfer)
                TransferProgress = (double)BytesTransferred / (double)TotalBytesToTransfer;
            else
                TransferProgress = 0;
        }
    }
}