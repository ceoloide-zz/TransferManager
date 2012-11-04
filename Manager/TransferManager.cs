using System.Collections.Generic;
using System.IO.IsolatedStorage;
using Microsoft.Phone.BackgroundTransfer;
using System.Windows.Threading;
using System;
using System.Linq;
using System.Windows;

namespace TransferManager
{
    /// <summary>
    /// This class represents a TransferManager that is capable of processing
    /// ITransferable objects.
    /// 
    /// TODO: Move to background thread all operations.
    /// </summary>
    public class TransferManager
    {
        /// <summary>
        /// The internal queue used by the TransferManager to hold the items
        /// to be transfered, since the BackgroundTransferService allows only
        /// 5 items to be queued at a time.
        /// </summary>
        private LinkedList<ITransferable> _InternalQueue;

        /// <summary>
        /// An internal reference to the ITransferManagerContext object
        /// that is used to retrieve pending transfers.
        /// </summary>
        private TransferManagerContext _TransferManagerContext;

        /// <summary>
        /// An internal counter representing the number of BackgroundTransfers that have been queued in
        /// the BackgroundTransferService.
        /// </summary>
        private int _ActiveBackgroundTransfers = 0;

        /// <summary>
        /// Public constructor of the TransferManager class. It checks for existing
        /// transfers and processes them. It also goes through the list of pending
        /// ITransferable objects to add them to the internal queue.
        /// <param name="TransferManagerContext">The TransferManagerContext that the TransferManager leverages on.</param>
        /// </summary>        
        public TransferManager(TransferManagerContext TransferManagerContext)
        {
            _InternalQueue = new LinkedList<ITransferable>();
            _TransferManagerContext = TransferManagerContext;

            // The "/shared/transfers" directory is required
            // we create it if it's not existing.
            using (IsolatedStorageFile IsoStore = IsolatedStorageFile.GetUserStoreForApplication())
            {
                if (!IsoStore.DirectoryExists("/shared/transfers"))
                {
                    IsoStore.CreateDirectory("/shared/transfers");
                }
            }

            IEnumerable<BackgroundTransferRequest> BackgroundTransfers = BackgroundTransferService.Requests;
            _ActiveBackgroundTransfers = BackgroundTransfers.Count<BackgroundTransferRequest>();

            // There can be existing transfer that need to be processed
            foreach (var Transfer in BackgroundTransfers)
            {
                ITransferable Item = TransferManagerContext.FindByTag(Transfer.Tag);

                if (Item != null)
                {
                    // We rewire the event handler for progress change to be notified of changes
                    Transfer.TransferProgressChanged += Item.TransferProgressChanged;

                    // We rewire the event handler for status change to be notified of changes
                    Transfer.TransferStatusChanged += new System.EventHandler<BackgroundTransferEventArgs>(TransferRequest_TransferStatusChanged);
                    
                    // We now process the transfer in case the status changed to Completed while the TransferManager was not active
                    ProcessTransfer(Transfer);
                }
                else
                {
                    // The object related to the tag cannot be retrieved
                    // We have no choice other than removing the transfer

                    RemoveTransferRequest(Transfer.RequestId);

                    //TODO: #LOG error
                }
            }

            // The TransferManager leverages on a ITransferManagerContext object that
            // provides a list of ITransferable items from outside its scope. The object
            // can be persisted by the ITransferManagerContext if needed.
            foreach (ITransferable Item in _TransferManagerContext.RetrieveQueued())
            {
                _InternalQueue.AddLast(Item);
            }

            // The internal queue is now processed to add all transfers that are pending, based on
            // available slots in the BackgroundTransferService queue
            ProcessQueue();
        }

        /// <summary>
        /// This method processes the internal queue and adds a new transfer to the BackgroundTransferService
        /// queue when there are slots available and queued transfers.
        /// </summary>
        private void ProcessQueue()
        {
            lock (this)
            {
                // New transfers can be added to the BackgroundTransferService queue only
                // if less than 5 items have been queued already.
                if (_ActiveBackgroundTransfers < 5)
                {
                    lock (_InternalQueue)
                    {
                        if (_InternalQueue.Count > 0)
                        { // If there are pending transfers
                            ITransferable Item = _InternalQueue.First.Value;
                            _InternalQueue.RemoveFirst();

                            bool IsAddSuccessful = AddTransferRequest(Item);

                            if (!IsAddSuccessful && Item.TransferStatus == ExtendedTransferStatus.Queued)
                            { // The transfer was not successfully added to the BackgroundTransferService
                                // queue, we update the status of the request accordingly only if it was in
                                // Queued state, otherwise there might be a copy of the transfer request
                                // currently in transfer.

                                Item.TransferStatus = ExtendedTransferStatus.Failed;
                            }

                            // We recursively check for other items to be added to the BackgroundTransferService queue
                            ProcessQueue();
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// This method creates a BackgroundTransferRequest object based on the information
        /// of the ITransferable object passed as a parameter. The BackgroundTransferRequest
        /// is then addedd to the BackgroundTransferService queue.
        /// </summary>
        /// <param name="Item">The ITransferable item.</param>
        /// <returns>True, if the BackgroundTransferRequest has been created and added successfully to the BackgroundTransferService.</returns>
        private bool AddTransferRequest(ITransferable Item)
        {
            //TODO: Improve validation of Item.TransferUri and Item.TransferLocationUri -> If URI is not valid then the object will not be removable

            // First, validate the TransferURI
            Uri TransferUri;
            if (!Uri.TryCreate(Item.TransferUrl, UriKind.Absolute, out TransferUri) || String.IsNullOrEmpty(Item.TransferUrl) || String.IsNullOrWhiteSpace(Item.TransferUrl))
            {
#if DEBUG
                // In debug mode show MessageBox with exception details.
                MessageBox.Show("I'm unable to add the background transfer request due to a malformed remote URI (" + Item.TransferUrl + ").", "Unable to start transfer", MessageBoxButton.OK);
#endif
                return false;
            }

            // Second, validate the TransferLocation
            Uri TransferLocation;
            if (!Uri.TryCreate(Item.FilenameWithPath, UriKind.Relative, out TransferLocation) || String.IsNullOrEmpty(Item.FilenameWithPath) || String.IsNullOrWhiteSpace(Item.FilenameWithPath))
            {
#if DEBUG
                // In debug mode show MessageBox with exception details.
                MessageBox.Show("I'm unable to add the background transfer request due to a malformed local URI (" + Item.TransferLocationUri + ").", "Unable to start transfer", MessageBoxButton.OK);
#endif
                return false;
            }

            // A new BackgroundTransferRequest is created by passing the TransferUri of the ITransferable object.
            BackgroundTransferRequest TransferRequest = new BackgroundTransferRequest(Item.TransferUri);

            // Set the transfer properties according to the TransferSettings of the ITransferable object
            #region Set transfer properties
            try
            {
                TransferRequest.Method = Item.Method; // GET or POST
                if (TransferRequest.Method == "GET")
                { // The temporary transfer location for download
                    TransferRequest.DownloadLocation = Item.TransferLocationUri;
                }
                else
                {// The temporary transfer location for upload
                    TransferRequest.UploadLocation = Item.TransferLocationUri;
                }
            }
            catch (ArgumentException)
            {
#if DEBUG
                // In debug mode show MessageBox with exception details.
                MessageBox.Show("I'm unable to add the background transfer request due to a malformed local URI (" + Item.TransferLocationUri + ").", "Unable to start transfer", MessageBoxButton.OK);
#endif
                return false;
            }

            TransferRequest.Tag = Item.UID.ToString(); // The TransferId
            //TransferRequest.Headers.Add(Item.Headers); // Additional headers
            TransferRequest.TransferPreferences = TransferSettings.I.TransferPreferences; // Transfer preferences such as None, AllowCellular, AllowBattery, AllowCellularAndBattery.
            #endregion

            // We register the event handlers for the progress change
            TransferRequest.TransferProgressChanged += Item.TransferProgressChanged;

            // The BackgroundTransferRequest is ready to be addedd to the BackgroundTransferService queue.
            // Before adding, we call the ITransferable BeforeAdding() method to perform additional
            // operations (e.g. placing the file in the right folder).
            Item.OnBeforeAdd();

            // Now the BackgroundTrabsferRequest is ready to be added to the BackgroundTransferService queue
            // A try/catch block is necessary to handle the possible exceptions
            TransferRequest.TransferStatusChanged += new EventHandler<BackgroundTransferEventArgs>(TransferRequest_TransferStatusChanged); // Transfer manager handler for status change

            try
            {
                BackgroundTransferService.Add(TransferRequest);

                // If add was successful we can retrieve the RequestId value and store it in the ITransferable object
                // The RequestId is generated right after the instantiation of the BackgroundTransferRequest, but to
                // avoid overwriting a previous value on the ITransferable object, we store it only after "Add" is
                // successful (which means that the same object is not currently transferring).
                Item.RequestId = TransferRequest.RequestId;

                lock (this)
                {
                    // We increment the internal counter
                    _ActiveBackgroundTransfers++;
                }

                var BackgroundTransfers = BackgroundTransferService.Requests;

                // The BackgroundTransferRequest was successfully added to the BackgroundTransferService queue.
                return true;
            }
            catch (BackgroundTransferInternalException ex)
            {   //BackgroundTransferInternalException is thrown when:
                // - The total request limit across all applications has been reached.
                // - The HTTP network provider returned an error.
                // - The HTTP network provider returned an error related to content-range request or response.
                // - The HTTP network provider returned a network-related error.
                // - The HTTP network provider returned a slow transfer error.
                // - The isolated storage provider returned an error.

#if DEBUG
                // In debug mode show MessageBox with exception details.
                MessageBox.Show("I'm unable to add the background transfer request (BackgroundTransferInternalException). " + ex.Message, "Unable to start transfer", MessageBoxButton.OK);
#endif
            }
            catch (InvalidOperationException ex)
            {   // InvalidOperationException is thrown when:
                // - The request has already been submitted.
                // - The maximum number of requests per application has been reached.
                // - A request with the same DownloadLocation URI has already been submitted.
                // - The user has disabled background tasks in the device’s Settings.

#if DEBUG
                // In debug mode show MessageBox with exception details.
                MessageBox.Show("I'm unable to add the background transfer request (InvalidOperationException). " + ex.Message, "Unable to start transfer", MessageBoxButton.OK);
#endif
            }
            catch (SystemException ex)
            {   //SystemException is thrown when:
                // - The maximum number of requests on the device has been reached.
                // - The underlying transport layer returned an error.
                // - The underlying transport layer returned an error related to the content-range request or response.
                // - There is not enough space on the disk.

                if (ex.Message == "There is not enough space on the disk.")
                {
                    MessageBox.Show("There is not enough space on the device to start the transfer. Please free up some space and then retry.", "Unable to start transfer", MessageBoxButton.OK);
                }

#if DEBUG
                // In debug mode show MessageBox with exception details.
                MessageBox.Show("I'm unable to add the background transfer request (SystemException). " + ex.Message, "Unable to start transfer", MessageBoxButton.OK);
#endif

            }
            catch (Exception ex)
            {
#if DEBUG
                // In debug mode show MessageBox with exception details.
                MessageBox.Show("I'm unable to add the background transfer request (Exception). " + ex.Message, "Unable to start transfer", MessageBoxButton.OK);
#endif
            }

            return false;
        }

        /// <summary>
        /// This method handles the TransferStatusChanged event raised by a BackgroundTransferRequest and processes the new status.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">The event arguments. The BackgroundTransferRequest object is stored in the Request property.</param>
        void TransferRequest_TransferStatusChanged(object sender, BackgroundTransferEventArgs e)
        {
            ProcessTransfer(e.Request);
        }

        /// <summary>
        /// This method processes the changes in status of a BackgroundTransferRequest.
        /// </summary>
        /// <param name="Transfer">The BackgroundTransferRequest to process.</param>
        private void ProcessTransfer(BackgroundTransferRequest Transfer)
        {
            // We retrieve from the TransferManagerContext the ITransferable object
            // associated to the BackgroundTransferRequest. 
            ITransferable Item = _TransferManagerContext.FindByTag(Transfer.Tag);

            // We process the possible statuses.
            switch (Transfer.TransferStatus)
            {
                case TransferStatus.None:
                    // The request has not yet been queued.
                    if (Item != null) Item.TransferStatus = ExtendedTransferStatus.None;
                    break;
                case TransferStatus.Paused:
                    // The request has been paused and waiting in the background transfer service queue.
                    if (Item != null) Item.TransferStatus = ExtendedTransferStatus.Paused;
                    break;
                case TransferStatus.Transferring:
                    // The requested file is currently being transferred.
                    if (Item != null) Item.TransferStatus = ExtendedTransferStatus.Transferring;
                    break;
                case TransferStatus.Unknown:
                    // The background transfer service could not retrieve the request status from the service.
                    // Once in this state, a BackgroundTransferRequest object is no longer useable.
                    // You may attempt to retrieve a new instance using the Find(String) method.
                    // We ignore this status.
                    break;
                case TransferStatus.Waiting:
                    if (Item != null)
                    {
                        if (Transfer.StatusCode >= 500 && Transfer.StatusCode <= 599)
                        {
                            // The request is waiting in the background transfer service queue. 
                            // This status indicates that the request is queued and waiting to retry
                            // due to a server error (5XX).
                            Item.TransferStatus = ExtendedTransferStatus.WaitingForRetry;
                        }
                        else
                        {
                            // The request is waiting in the background transfer service queue. 
                            // This status can indicate that the request is queued and waiting for previous transfers
                            // to complete or that the service is retrying the request due to a network error.
                            Item.TransferStatus = ExtendedTransferStatus.Waiting;
                        }
                    }
                    break;
                case TransferStatus.WaitingForExternalPower:
                    // The request is waiting in the background transfer service queue for external power to be connected.
                    if (Item != null) Item.TransferStatus = ExtendedTransferStatus.WaitingForExternalPower;
                    break;
                case TransferStatus.WaitingForExternalPowerDueToBatterySaverMode:
                    // The request is waiting for the device to be connected to external power because the user 
                    // has enabled Battery Saver mode on the device.
                    if (Item != null) Item.TransferStatus = ExtendedTransferStatus.WaitingForExternalPowerDueToBatterySaverMode;
                    break;
                case TransferStatus.WaitingForNonVoiceBlockingNetwork:
                    // The background transfer service does not run when the device is on a non-simultaneous voice and data network, 
                    // including 2G, EDGE, and standard GPRS. This status indicates the service is waiting for a supported network connection.
                    if (Item != null) Item.TransferStatus = ExtendedTransferStatus.WaitingForNonVoiceBlockingNetwork;
                    break;
                case TransferStatus.WaitingForWiFi:
                    // The request is waiting in the background transfer service queue for a Wi-Fi connection.
                    if (Item != null) Item.TransferStatus = ExtendedTransferStatus.None;
                    break;
                case TransferStatus.Completed:
                    // The request has completed. This means that the request is no longer actionable by the background transfer service 
                    // regardless of whether the transfer was completed successfully. To confirm that a transfer was successful, 
                    // confirm that the TransferError property is null.

                    // First of all we remove the BackgroundTransferRequest object from the BackgroundTransferService queue.
                    RemoveTransferRequest(Transfer.RequestId);

                    // Second we process the internal queue to add all transfers that are pending, based on
                    // available slots in the BackgroundTransferService queue
                    ProcessQueue();

                    // TODO: If item is null, we need to clean the local folder if the transfer was a Download
                    if (Item != null)
                    {
                        if (Transfer.TransferError == null)
                        {
                            // If the status code of a completed transfer is 200 or 206, the
                            // transfer was successful
                            if (Transfer.StatusCode == 200 || Transfer.StatusCode == 206)
                            {
                                Item.OnComplete();
                            }
                            else
                            {   // TODO: Handle other possible success status codes
#if DEBUG
                                // In debug mode show MessageBox with exception details.
                                MessageBox.Show("Unhandled successful transfer status. " + Transfer.StatusCode, "Unable to process transfer", MessageBoxButton.OK);
#endif
                                // It seems that BackgroundTransferService handles redirects (3XX) internally.
                                throw new System.NotImplementedException();
                            }
                        }
                        else
                        {   //Transfer has been completed but failed.

                            if (Transfer.TransferError is InvalidOperationException)
                            {   // TransferError is InvalidOperationException when the BackgroundTransferRequest
                                // has been removed before completing the transfer (canceled).

                                Item.TransferStatus = ExtendedTransferStatus.Canceled;
                            }
                            else
                            {
                                if (Transfer.StatusCode >= 400 && Transfer.StatusCode <= 499)
                                {   // The transfer failed due to a client error that cannot be recovered
                                    Item.TransferStatus = ExtendedTransferStatus.Failed;
                                }
                                else if (Transfer.StatusCode >= 500 && Transfer.StatusCode <= 599)
                                {   // Transfer failed due to a server error that cannot be recovered
                                    Item.TransferStatus = ExtendedTransferStatus.FailedServer;
                                }
                                else if (Transfer.StatusCode == 0)
                                {   // The transfer did not even start, probably due to an error in the Transfer URI
                                    Item.TransferStatus = ExtendedTransferStatus.Failed;
                                }
                                else
                                {   // The transfer faled permanently for other reasons
                                    Item.TransferStatus = ExtendedTransferStatus.Failed;
                                }
                            }
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// This method removes a BackgroundTransferRequest from the BackgroundTransferService queue.
        /// </summary>
        /// <param name="RequestId">The RequestId of the BackgroundTransferRequest to remove.</param>
        private void RemoveTransferRequest(string RequestId)
        {
            // First we need to find the BackgroundTransferRequest associated with the RequestId
            // This method may throw one InvalidOperationException if the request has previously been cancelled.
            try
            {
                // Returns BackgroundTransferRequest. The transfer request associated with the specified ID or NULL 
                // if a transfer request with the specified ID cannot be found in the queue.
                BackgroundTransferRequest Transfer = BackgroundTransferService.Find(RequestId);

                if (Transfer != null)
                {   // The BackgroundTransferRequest was found in the BackgroundTransferService queue
                    try
                    {
                        // After removing a transfer, if the TransferStatus is not already Completed, the TransferStatusChanged
                        // event will be fired and the request’s TransferError property will be set to an InvalidOperationException
                        // with the message “The request has previously been cancelled.”
                        BackgroundTransferService.Remove(Transfer);

                        // Remove was successful, we decrement the counter for active background transfers
                        lock (this)
                        {
                            _ActiveBackgroundTransfers--;
                        }

                        // Dispose of the transfer to avoid memory leaks.
                        Transfer.Dispose();
                        Transfer = null;
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        // In debug mode show MessageBox with exception details.
                        MessageBox.Show("I'm unable to remove the background transfer request (" + ex.GetType() + "). " + ex.Message, "Unable to remove transfer", MessageBoxButton.OK);
#endif
                    }
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                // In debug mode show MessageBox with exception details.
                MessageBox.Show("I'm unable to find the background transfer request (" + ex.GetType() + "). " + ex.Message, "Unable to find transfer", MessageBoxButton.OK);
#endif
            }
        }

        /// <summary>
        /// This method queues an ITransferable item as first
        /// in the internal queue of the TransferManager.
        /// </summary>
        /// <param name="Item">An ITransferable item to be added to the queue.</param>
        private void QueueFirst(ITransferable Item)
        {
            lock (_InternalQueue)
            {
                _InternalQueue.AddFirst(Item);
                Item.TransferStatus = ExtendedTransferStatus.Queued;
            }
        }

        /// <summary>
        /// This method queues an ITransferable item as last
        /// in the internal queue of the TransferManager.
        /// </summary>
        /// <param name="Item">An ITransferable item to be added to the queue.</param>
        private void Queue(ITransferable Item)
        {
            // We reset the transfer progress
            Item.ResetTransferProgress();

            lock (_InternalQueue)
            {
                _InternalQueue.AddLast(Item);
                Item.TransferStatus = ExtendedTransferStatus.Queued;
            }

            // We process the internal queue to add all transfers that are pending, based on
            // available slots in the BackgroundTransferService queue
            ProcessQueue();
        }

        /// <summary>
        /// This method queues a list of ITransferable items as last
        /// in the internal queue of the TransferManager.
        /// </summary>
        /// <param name="Item">A list of ITransferable items to be added to the queue.</param>
        private void Queue(ICollection<ITransferable> Items)
        {
            foreach (ITransferable Item in Items)
            {
                Queue(Item);
            }
        }

        /// <summary>
        /// This method queues an ITransferable item, adding it as first
        /// in the internal queue of the TransferManager.
        /// </summary>
        /// <param name="Item">An ITransferable item to be added to the queue.</param>
        public void StartAsFirst(ITransferable Item)
        {
            QueueFirst(Item);
        }

        /// <summary>
        /// This method queues an ITransferable item, adding it as last
        /// in the internal queue of the TransferManager.
        /// </summary>
        /// <param name="Item">An ITransferable item to be added to the queue.</param>
        public void Start(ITransferable Item)
        {   
            Queue(Item);
        }

        /// <summary>
        /// This method queues a list of ITransferable items, adding each as last
        /// in the internal queue of the TransferManager.
        /// The FailedTransferAttempts of each item is reset to 0;
        /// </summary>
        /// <param name="Item">An ITransferable item to be added to the queue.</param>
        public void Start(ICollection<ITransferable> Items)
        {
            foreach (ITransferable Item in Items)
            {
                Item.ResetTransferProgress();
                Queue(Item);
            }
        }

        /// <summary>
        /// This method removes an Itransferable item from the TransferManager
        /// internal queue or from the BackgroundTransferService queue.
        /// </summary>
        /// <param name="Item">The ITransferable item to cancel.</param>
        public void Cancel(ITransferable Item)
        {
            // If the transfer is currently queued then we need to just remove 
            // it from the internal queue.
            if (Item.TransferStatus == ExtendedTransferStatus.Queued)
            {
                lock (_InternalQueue)
                {
                    _InternalQueue.Remove(Item);
                }

                // Now we should set the TransferStatus accordingly
                Item.TransferStatus = ExtendedTransferStatus.Canceled;
            }
            // Else, if the transfer is currently inside the BackgroundTransferService
            // queue, then we need to request its removal from there.
            else if (Item.TransferStatus != ExtendedTransferStatus.Queued &&
                Item.TransferStatus != ExtendedTransferStatus.Canceled &&
                Item.TransferStatus != ExtendedTransferStatus.Completed &&
                Item.TransferStatus != ExtendedTransferStatus.Failed &&
                Item.TransferStatus != ExtendedTransferStatus.FailedServer &&
                Item.TransferStatus != ExtendedTransferStatus.None)
            {
                RemoveTransferRequest(Item.RequestId); // This will set TransferStatus accordingly
            }
        }

        /// <summary>
        /// This method removes all Itransferable item from the TransferManager
        /// internal queue or from the BackgroundTransferService queue.
        /// </summary>
        public void CancelAll()
        {
            lock (_InternalQueue)
            {
                // We remove all items from the internal queue
                foreach (ITransferable Item in _InternalQueue.ToList<ITransferable>())
                {
                    Cancel(Item);
                }

                // We remove all items from the BackgroundTransferService queue
                foreach (BackgroundTransferRequest Request in BackgroundTransferService.Requests)
                {
                    ITransferable Item = _TransferManagerContext.FindByTag(Request.Tag);
                    Cancel(Item);
                }
            }            
        }
    }
}