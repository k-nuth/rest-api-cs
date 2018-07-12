namespace bitprim.insight.DTOs
{
    /// <summary>
    /// GetCurrencyResponse data structure.
    /// </summary>
    public class GetCurrencyResponse
    {
        /// <summary>
        /// 200 = success, any other value is considered an error.
        /// </summary>
        public int status { get; set; }

        /// <summary>
        /// 200 = success, any other value is considered an error.
        /// </summary>
        public CurrencyData data { get; set; }
    }

    /// <summary>
    /// Currency prices from various providers.
    /// </summary>
    public class CurrencyData
    {
        /// <summary>
        /// Current price in USD as reported by the Bitstamp service. 
        /// </summary>
        public float bitstamp { get; set; }
    }
}