namespace bitprim.insight.DTOs
{
    /// <summary>
    /// OutputScriptSummary data structure.
    /// </summary>
    public class OutputScriptSummary
    {
        /// <summary>
        /// Script representation as raw hex data.
        /// </summary>
        public string hex { get; set; }

        /// <summary>
        /// Script representation in Script language.
        /// </summary>
        public string asm { get; set; }

        /// <summary>
        /// Output destination addresses.
        /// </summary>
        public string[] addresses { get; set; }

        /// <summary>
        /// Script type (pub2keyhash, multisig, etc)
        /// </summary>
        public string type { get; set; }
    }

}