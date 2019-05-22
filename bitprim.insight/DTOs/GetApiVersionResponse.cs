namespace bitprim.insight.DTOs
{
    /// <summary>
    /// GetApiVersionResponse data structure.
    /// </summary>
    public class GetApiVersionResponse
    {
        /// <summary>
        /// bitprim-insight API version in semantic versioning notation (major.minor.patch).
        /// </summary>
        public string version { get; set; }
    }
}