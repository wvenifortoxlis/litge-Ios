namespace LitGe
{
    public class EpubKey
    {
        /// <summary>
        /// Gets or sets the valid from date.
        /// </summary>
        public DateTime ValidFrom { get; set; }

        /// <summary>
        /// Gets or sets the valid to date.
        /// </summary>
        public DateTime ValidTo { get; set; }

        /// <summary>
        /// Gets or sets AES key.
        /// </summary>
        public byte[] Key { get; set; }
    }
}
