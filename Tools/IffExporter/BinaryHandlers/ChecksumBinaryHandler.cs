using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace IffExporter.BinaryHandlers
{
    /// <summary>
    /// Binary handler that calculates SHA256 checksums
    /// </summary>
    public class ChecksumBinaryHandler : IBinaryHandler
    {
        public object HandleBinaryData(byte[] data, string context, object metadata = null)
        {
            if (data == null || data.Length == 0)
                return null;

            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(data);
                var result = new Dictionary<string, object>
                {
                    ["checksum"] = BitConverter.ToString(hash).Replace("-", "").ToLower(),
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
}
