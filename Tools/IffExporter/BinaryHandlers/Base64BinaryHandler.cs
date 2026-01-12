using System;
using System.Collections.Generic;

namespace IffExporter.BinaryHandlers
{
    /// <summary>
    /// Binary handler that encodes data as base64 strings
    /// </summary>
    public class Base64BinaryHandler : IBinaryHandler
    {
        public object HandleBinaryData(byte[] data, string context, object metadata = null)
        {
            if (data == null || data.Length == 0)
                return null;

            var result = new Dictionary<string, object>
            {
                ["base64"] = Convert.ToBase64String(data),
                ["size"] = data.Length
            };

            // Include metadata if provided
            if (metadata is Dictionary<string, object> metaDict)
            {
                foreach (var kvp in metaDict)
                {
                    result[kvp.Key] = kvp.Value;
                }
            }

            return result;
        }
    }
}
