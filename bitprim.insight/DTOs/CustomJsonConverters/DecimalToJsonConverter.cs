using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace bitprim.insight.DTOs
{
    internal class DecimalToJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(decimal);
        }

        public override bool CanRead
        {
            get { return false; }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Unnecessary because CanRead is false. The type will skip the converter.");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            //Remove trailing zeros, and write as a raw value to bypass default decimal rendering
            writer.WriteRawValue(((decimal)value).ToString("0.##########"));
        }
    }
}