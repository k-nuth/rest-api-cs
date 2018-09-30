using Newtonsoft.Json;

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
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string[] addresses { get; set; }

        /// <summary>
        /// Script type (pub2keyhash, multisig, etc)
        /// </summary>
        public string type { get; set; }

        /// <summary>
        /// Returns true if and only if the "type" property should be serialized.
        /// Naming convention is intentionally violated because Newtonsoft.Json relies
        /// on the "ShouldSerialize" prefix before the exact property name.
        /// </summary>
        public bool ShouldSerializetype()
        {
            return type != "non_standard";
        }
    }

}