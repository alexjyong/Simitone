namespace IffExporter.BinaryHandlers
{
    /// <summary>
    /// Interface for handling binary data during export
    /// </summary>
    public interface IBinaryHandler
    {
        /// <summary>
        /// Handles binary data and returns an object suitable for JSON serialization
        /// </summary>
        /// <param name="data">Binary data</param>
        /// <param name="context">Context information (e.g., "sprite_pixels", "z_buffer")</param>
        /// <param name="metadata">Optional metadata about the binary data</param>
        /// <returns>Object to include in JSON export, or null to omit</returns>
        object HandleBinaryData(byte[] data, string context, object metadata = null);
    }
}
