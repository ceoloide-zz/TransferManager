using System.Data.Linq;

namespace TransferManager
{
    public class TransferDataContext<T> : DataContext 
        where T : class, ITransferable
    {
        /// <summary>
        /// This constructor just passes the ConnectionString to the base class constructor.
        /// </summary>
        /// <param name="ConnectionString">The ConnectionString to pass to the base class.</param>
        public TransferDataContext(string ConnectionString) : base(ConnectionString)
        { }

        /// <summary>
        /// A table that holds all the transfers.
        /// </summary>
        public Table<T> Transfers;
    }
}
