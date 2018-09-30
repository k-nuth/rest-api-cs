using Newtonsoft.Json;
using System;

namespace bitprim.insight.DTOs
{
    /// <summary>
    /// TransactionInputSummary data structure.
    /// This class has no fields because the common fields between
    /// coinbase and non coinbase inputs follow a different order,
    /// so having common fields would hinder serialization.
    /// </summary>
    public abstract class TransactionInputSummary
    {
    }

}