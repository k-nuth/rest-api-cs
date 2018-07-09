using System;
using Bitprim;
using Newtonsoft.Json.Linq;

namespace bitprim.insight.Exceptions
{
    /// <summary>
    /// For domain-specific errors.
    /// </summary>
    public class BitprimException : Exception
    {
        /// <summary>
        /// Underlying node error code.
        /// </summary>
        public ErrorCode ErrorCode { get; set; }
        /// <summary>
        /// For http exception response.
        /// </summary>
        public string ContentType { get; set; } = @"text/plain";

        /// <summary>
        /// Build from node error code.
        /// </summary>
        public BitprimException(ErrorCode errorCode)
        {
            this.ErrorCode = errorCode;
        }

        /// <summary>
        /// Build from node error code and user-friendly message.
        /// </summary>
        public BitprimException(ErrorCode errorCode, string message) : base(message)
        {
            this.ErrorCode = errorCode;
        }

        /// <summary>
        /// Build from node error code and .NET exception.
        /// </summary>
        public BitprimException(ErrorCode errorCode, Exception inner) : this(errorCode, inner.ToString()) { }

        /// <summary>
        /// Build from node error code and parsed JObject.
        /// </summary>
        public BitprimException(ErrorCode errorCode, JObject errorObject) : this(errorCode, errorObject.ToString())
        {
            this.ContentType = @"application/json";
        }
    }
}