using System;
using Bitprim;
using Newtonsoft.Json.Linq;

namespace bitprim.insight.Exceptions
{
    public class BitprimException : Exception
    {
        public ErrorCode ErrorCode { get; set; }
        public string ContentType { get; set; } = @"text/plain";

        public BitprimException(ErrorCode errorCode)
        {
            this.ErrorCode = errorCode;
        }

        public BitprimException(ErrorCode errorCode, string message) : base(message)
        {
            this.ErrorCode = errorCode;
        }

        public BitprimException(ErrorCode errorCode, Exception inner) : this(errorCode, inner.ToString()) { }

        public BitprimException(ErrorCode errorCode, JObject errorObject) : this(errorCode, errorObject.ToString())
        {
            this.ContentType = @"application/json";
        }
    }
}