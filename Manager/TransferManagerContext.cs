using System.Collections.Generic;

namespace TransferManager
{
    public interface TransferManagerContext
    {
        /// <summary>
        /// This method returns a list containing ITransferable objects
        /// that are still pending in the TransferManager.
        /// </summary>
        /// <returns>A list of ITransferable objects.</returns>
        ICollection<ITransferable> RetrieveQueued();

        /// <summary>
        /// This method provides a way to retrieve an ITransferable object based on
        /// a BackgroundTransfer Tag property.
        /// </summary>
        /// <param name="Tag">A string that represents the Tag property of a BackgroundTransfer object</param>
        /// <returns>An ITransferable object.</returns>
        ITransferable FindByTag(string Tag);
    }
}
