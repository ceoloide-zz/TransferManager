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

    public abstract class AbstractTransfer : INotifyPropertyChanged, INotifyPropertyChanging, ITransferable
    {

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        // Used to notify that a property changed
        protected void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion

        #region INotifyPropertyChanging Members

        public event PropertyChangingEventHandler PropertyChanging;

        // Used to notify that a property is about to change
        protected void NotifyPropertyChanging(string propertyName)
        {
            if (PropertyChanging != null)
            {
                PropertyChanging(this, new PropertyChangingEventArgs(propertyName));
            }
        }

        #endregion

        /// <summary>
        /// Parameterless constructor.
        /// </summary>
        public AbstractTransfer();

        protected string _Path;
        /// <summary>
        /// The path to the transferred resource (e.g. /local/download/).
        /// </summary>
        public string Path
        {
            get { return _Path; }
            set
            {
                // TODO: Improve path validation

                Uri LocalPath;

                if (Uri.IsWellFormedUriString(value, UriKind.Relative) && Uri.TryCreate(value, UriKind.Relative, out LocalPath))
                {
                    NotifyPropertyChanging("Path");

                    _Path = value;

                    // The path should start and end with a "/" 
                    if (_Path.EndsWith("/"))
                        _Path = _Path.Substring(0, _Path.Length - 1);
                    if (!_Path.StartsWith("/"))
                        _Path = "/" + _Path;

                    NotifyPropertyChanged("Path");
                }
                else
                {
                    throw new ArgumentException("The provided path is not valid (" + value + ")");
                }
            }
        }

        protected string _Filename;
        /// <summary>
        /// Gets the filename of the transferred resource.
        /// </summary>
        public string Filename
        {
            get { return _Filename; }
            set
            {
                NotifyPropertyChanging("Filename");

                // TODO: Validate file name

                _Filename = value;
                NotifyPropertyChanged("Filename");
            }
        }

        /// <summary>
        /// Gets the full local path to the transferred resource (e.g. /local/download/file.txt)
        /// </summary>
        public string FilenameWithPath { get { return _Path + "/" + _Filename; } }

        /// <summary>
        /// Operations to be performed before adding the transfer to the transfer manager
        /// </summary>
        public void OnBeforeAdd();

        protected string _Method;
        /// <summary>
        /// Gets the transfer method (e.g. GET).
        /// </summary>
        public string Method
        {
            get { return _Method; }
            set
            {
                NotifyPropertyChanging("Method");
                _Method = value;
                NotifyPropertyChanged("Method");
            }
        }

        //protected KeyValuePair<string, string> _Headers;
        ///// <summary>
        ///// A KeyValuePair collection that holds the Headers to set. The following headers are reserved for use by the system and cannot be used by calling applications.
        ///// Adding one of the following headers to the Headers collection will cause a NotSupportedException to be thrown when the Add(BackgroundTransferRequest) method is used to queue the transfer request:
        ///// - If-Modified-Since
        ///// - If-None-Match
        ///// - If-Range
        ///// - Range
        ///// - Unless-Modified-Since
        ///// </summary>
        //public KeyValuePair<string, string> Headers 
        //{ 
        //    get { return _Headers; }
        //    set { _Headers = value; }
        //}

        /// <summary>
        /// Gets the URI of temporary location of the transfer.
        /// </summary>
        public Uri TransferLocationUri { get { return new Uri("shared/transfers" + FilenameWithPath, UriKind.RelativeOrAbsolute); } }

        protected string _TransferUrl;
        /// <summary>
        /// Gets or sets the url of the ITransferable object.
        /// </summary>
        public string TransferUrl
        {
            get { return _TransferUrl; }
            set
            {
                Uri Url;

                if (Uri.IsWellFormedUriString(value, UriKind.Absolute) && Uri.TryCreate(value, UriKind.Absolute, out Url))
                {
                    NotifyPropertyChanging("TransferUrl");
                    _TransferUrl = value;
                    NotifyPropertyChanged("TransferUrl");
                }
                else
                {
                    throw new ArgumentException("The provided TransferUrl is not valid (" + value + ")");
                }
            }
        }

        /// <summary>
        /// Gets the URI of the ITransferable object.
        /// </summary>
        public Uri TransferUri
        {
            get { return new Uri(_TransferUrl, UriKind.RelativeOrAbsolute); }
        }

        protected string _RequestId;
        /// <summary>
        /// Holds the RequestId property of the BackgroundTransfer
        /// instance asssociated with this object.
        /// </summary>
        public string RequestId
        {
            get { return _RequestId; }
            set
            {
                NotifyPropertyChanging("RequestId");
                _RequestId = value;
                NotifyPropertyChanged("RequestId");
            }
        }

        protected ExtendedTransferStatus _TransferStatus;
        /// <summary>
        /// Public property that holds the TransferStatus of the ITransferable object.
        /// The implementation should define the behaviour for each transfer status.
        /// </summary>
        public ExtendedTransferStatus TransferStatus
        {
            get
            {
                return _TransferStatus;
            }
            set
            {
                ExtendedTransferStatus PreviousTransferStatus = _TransferStatus;

                NotifyPropertyChanging("TransferStatus");
                _TransferStatus = value;
                NotifyPropertyChanged("TransferStatus");

                if (_TransferStatus == ExtendedTransferStatus.Canceled)
                    ResetTransferProgress();

                if (OnStatusChanged != null)
                {
                    StatusChangeHandler EventHandler = OnStatusChanged;
                    EventHandler(PreviousTransferStatus, _TransferStatus, this);
                }
            }
        }

        /// <summary>
        /// Event fired when the TransferStatus ob the ITransferable object changes.
        /// </summary>
        public event StatusChangeHandler OnStatusChanged;

        /// <summary>
        /// This method is called when a transfer has been completed successfully.
        /// </summary>
        public void OnComplete();

        protected bool _IsIndeterminateTransfer;
        /// <summary>
        /// Gets or sets whether the current transfer is indeterminate or not. A download
        /// transfer is indeterminate if TotalBytesToReceive is -1.
        /// </summary>
        public bool IsIndeterminateTransfer
        {
            get { return _IsIndeterminateTransfer; }
            set
            {
                NotifyPropertyChanging("IsIndeterminateTransfer");
                _IsIndeterminateTransfer = value;
                NotifyPropertyChanged("IsIndeterminateTransfer");
            }
        }

        protected long _TotalBytesToTransfer;
        /// <summary>
        /// Gets or sets the total bytes to receive for the current download transfer.
        /// </summary>
        public long TotalBytesToTransfer
        {
            get { return _TotalBytesToTransfer; }
            set
            {
                NotifyPropertyChanging("TotalBytesToTransfer");
                _TotalBytesToTransfer = value;
                NotifyPropertyChanged("TotalBytesToTransfer");
            }
        }

        protected long _BytesTransferred;
        /// <summary>
        /// Gets or sets the bytes that have been currently received.
        /// </summary>
        public long BytesTransferred
        {
            get { return _BytesTransferred; }
            set
            {
                NotifyPropertyChanging("BytesTransferred");
                _BytesTransferred = value;
                NotifyPropertyChanged("BytesTransferred");
            }
        }

        protected double _TransferProgress;
        /// <summary>
        /// Gets or sets the transfer progress.
        /// </summary>
        public double TransferProgress
        {
            get { return _TransferProgress; }
            set
            {
                NotifyPropertyChanging("TransferProgress");
                _TransferProgress = value;
                NotifyPropertyChanged("TransferProgress");
            }
        }

        /// <summary>
        /// Provides an handler for changes of transfer progress. Mainly used to update UI.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">The event args.</param>
        void ITransferable.TransferProgressChanged(object sender, BackgroundTransferEventArgs e);

        /// <summary>
        /// Provides a method that can be used to reset the transfer progress.
        /// </summary>
        public void ResetTransferProgress()
        {
            BytesTransferred = 0;
            IsIndeterminateTransfer = true;
            TransferProgress = 0;
        }
    }
}
