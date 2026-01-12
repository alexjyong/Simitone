using System;
using System.Collections.Generic;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using IffExporter.BinaryHandlers;

namespace IffExporter.Processors
{
    /// <summary>
    /// Processor for OBJD (Object Definition) chunks
    /// </summary>
    public class OBJDProcessor : IChunkProcessor
    {
        public Dictionary<string, object> Process(IffChunk chunk, Dictionary<string, object> basicData,
            ExportOptions options, IBinaryHandler binaryHandler)
        {
            var objd = chunk as OBJD;
            if (objd == null)
                return basicData;

            var result = new Dictionary<string, object>
            {
                ["chunkId"] = chunk.ChunkID,
                ["chunkLabel"] = chunk.ChunkLabel,
                ["chunkType"] = chunk.ChunkType
            };

            // Include version info
            result["objectVersion"] = objd.Version;

            // Try to extract all properties with field names
            var properties = basicData;
            foreach (var kvp in properties)
            {
                if (kvp.Key.StartsWith("chunk"))
                    continue;  // Skip chunk metadata
                result[kvp.Key] = kvp.Value;
            }

            return result;
        }
    }
}
