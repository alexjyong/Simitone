using System.Collections.Generic;
using FSO.Files.Formats.IFF;
using IffExporter.BinaryHandlers;

namespace IffExporter.Processors
{
    /// <summary>
    /// Interface for chunk-specific processors that format chunk data for export
    /// </summary>
    public interface IChunkProcessor
    {
        /// <summary>
        /// Processes chunk data and returns formatted output
        /// </summary>
        /// <param name="chunk">The original chunk object</param>
        /// <param name="basicData">Basic properties extracted by reflection</param>
        /// <param name="options">Export options</param>
        /// <param name="binaryHandler">Binary data handler</param>
        /// <returns>Processed chunk data dictionary</returns>
        Dictionary<string, object> Process(IffChunk chunk, Dictionary<string, object> basicData,
            ExportOptions options, IBinaryHandler binaryHandler);
    }
}
