namespace TransferManager
{
    /// <summary>
    /// Transfer statuses that map and extend 
    /// Microsoft.Phone.BackgroundTransfer.TransferStatus enumeration.
    /// </summary>
    public enum ExtendedTransferStatus
    {
        /// <summary>
        /// The request has not yet been queued.
        /// </summary>
        None,
        /// <summary>
        /// The request has been added to the DownloadManager queue.
        /// </summary>
        Queued,
        /// <summary>
        /// The requested file is currently being transferred.
        /// </summary>
        Transferring,
        /// <summary>
        /// The request is waiting in the background transfer service queue.
        /// This status can indicate that the request is queued and waiting for previous 
        /// transfers to complete or that the service is retrying the request due to a 
        /// network error.
        /// </summary>
        Waiting,
        /// <summary>
        /// The request is waiting in the background transfer service queue.
        /// This indicates that the request is queued and waiting to retry due
        /// to a server error (5XX).
        /// </summary>
        WaitingForRetry,
        /// <summary>
        /// The request is waiting in the background transfer service queue for a Wi-Fi connection.
        /// </summary>
        WaitingForWiFi,
        /// <summary>
        /// The request is waiting in the background transfer service queue for external power to be connected.
        /// </summary>
        WaitingForExternalPower,
        /// <summary>
        /// The request is waiting for the device to be connected to external power because the user has enabled 
        /// Battery Saver mode on the device.
        /// </summary>
        WaitingForExternalPowerDueToBatterySaverMode,
        /// <summary>
        /// The background transfer service does not run when the device is on a non-simultaneous voice 
        /// and data network, including 2G, EDGE, and standard GPRS. 
        /// This status indicates the service is waiting for a supported network connection.
        /// </summary>
        WaitingForNonVoiceBlockingNetwork,
        /// <summary>
        /// The request has been paused and waiting in the background transfer service queue.
        /// </summary>
        Paused,
        /// <summary>
        /// The transfer has been completed successfully.
        /// </summary>
        Completed,
        /// <summary>
        /// The transfer has been completed but failed due to internal reasons.
        /// </summary>
        Failed,
        /// <summary>
        /// The transfer has been completed but failed due to server errors.
        /// </summary>
        FailedServer,
        /// <summary>
        /// The transfer has been manually canceled by the user.
        /// </summary>
        Canceled
    }
}