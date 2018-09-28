namespace bitprim.insight.DTOs
{
    /// <summary>
    /// InputScriptSummary data structure.
    /// </summary>
    public class InputScriptSummary
    {
        /// <summary>
        /// Script representation as raw hex data.
        /// </summary>
        public string hex { get; set; }

        /// <summary>
        /// Script representation in Script language.
        /// </summary>
        public string asm { get; set; }
    }

}