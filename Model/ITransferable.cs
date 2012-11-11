using Microsoft.Phone.BackgroundTransfer;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace TransferManager
{
    /// <summary>
    /// Handler for the OnStatusChange event of a ITransferable object.
    /// </summary>
    /// <param name="Previous">The transfer status that the ITransferable object had before changing.</param>
    /// <param name="Current">The transfer status that the ITransferable object currently has.</param>
    public delegate void StatusChangeHandler(ExtendedTransferStatus Previous, ExtendedTransferStatus Current, ITransferable Item);

    /// <summary>
    /// This class represents an item that can be transfered using the
    /// TransferManager class.
    /// </summary>
    public interface ITransferable : INotifyPropertyChanged
    {
        /// <summary>
        /// Gets or sets the unique id of this ITransferable object.
        /// </summary>
        int UID { get; set; }

        /// <summary>
        /// Gets or sets transfer method to use: GET (default) or POST.
        /// </summary>
        string Method { get; set; }

        /// <summary>
        /// A KeyValuePair collection that holds the Headers to set. The following headers are reserved for use by the system and cannot be used by calling applications.
        /// Adding one of the following headers to the Headers collection will cause a NotSupportedException to be thrown when the Add(BackgroundTransferRequest) method is used to queue the transfer request:
        /// - If-Modified-Since
        /// - If-None-Match
        /// - If-Range
        /// - Range
        /// - Unless-Modified-Since
        /// </summary>
        //KeyValuePair<string, string> Headers { get; set; }

        /// <summary>
        /// Public property that holds a string representing the path to the file (e.g. /path/filename.ext)
        /// </summary>
        string FilenameWithPath { get; }

        /// <summary>
        /// Gets the URI of temporary location of the transfer.
        /// </summary>
        Uri TransferLocationUri { get; }

        /// <summary>
        /// Gets or sets the Uri of the ITransferable object.
        /// </summary>
        string TransferUrl { get; set; }

        /// <summary>
        /// Gets the Uri of the ITransferable object.
        /// </summary>
        Uri TransferUri { get; }

        /// <summary>
        /// Public property that holds the RequestId property of the BackgroundTransfer
        /// instance asssociated with this object.
        /// </summary>
        string RequestId { get; set; }

        /// <summary>
        /// Public property that holds the reference to an external object that this
        /// transfer is associated to.
        /// </summary>
        string ExternalReference { get; set; }

        /// <summary>
        /// Public property that holds the TransferStatus of the ITransferable object.
        /// The implementation should define the behaviour for each transfer status.
        /// </summary>
        ExtendedTransferStatus TransferStatus { get; set; }

        /// <summary>
        /// This method is called when a transfer has been completed successfully.
        /// </summary>
        void OnComplete();

        /// <summary>
        /// Event fired when the TransferStatus ob the ITransferable object changes.
        /// </summary>
        event StatusChangeHandler OnStatusChanged;

        /// <summary>
        /// Provides an handler for changes of transfer progress. Mainly used to update UI.
        /// Event argument contains BytesReceived / BytesSent that represent current transfer status
        /// and TotalBytesToReceive / TotalBytesToSend that represent the amount to transfer.
        /// 
        /// NOTE: TotalBytesToReceive can be -1 if the amount to receive is unknown.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">The event args.</param>
        void TransferProgressChanged(object sender, BackgroundTransferEventArgs e);

        /// <summary>
        /// Provides a method that is used to perform additional operations before the BackgroundTransferRequest
        /// is addedd to the BackgroundTransferService queue.
        /// </summary>
        void OnBeforeAdd();

        /// <summary>
        /// Provides a method that is used to reset the transfer progres.
        /// </summary>
        void ResetTransferProgress();
    }
}
