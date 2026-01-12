namespace IffExporter.BinaryHandlers
{
    /// <summary>
    /// Binary handler that skips all binary data
    /// </summary>
    public class SkipBinaryHandler : IBinaryHandler
    {
        public object HandleBinaryData(byte[] data, string context, object metadata = null)
        {
            // Return null to omit from output
            return null;
        }
    }
}
