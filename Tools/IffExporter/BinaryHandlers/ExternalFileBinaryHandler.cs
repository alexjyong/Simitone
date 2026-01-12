using System;
using System.Collections.Generic;
using System.IO;

namespace IffExporter.BinaryHandlers
{
    /// <summary>
    /// Binary handler that saves data to external files
    /// </summary>
    public class ExternalFileBinaryHandler : IBinaryHandler
    {
        private readonly string _outputDirectory;
        private int _fileCounter = 0;

        public ExternalFileBinaryHandler(string outputDirectory)
        {
            _outputDirectory = outputDirectory;

            // Create output directory if it doesn't exist
            if (!Directory.Exists(_outputDirectory))
            {
                Directory.CreateDirectory(_outputDirectory);
            }
        }

        public object HandleBinaryData(byte[] data, string context, object metadata = null)
        {
            if (data == null || data.Length == 0)
                return null;

            // Generate unique filename
            _fileCounter++;
            var extension = GetFileExtension(context);
            var filename = $"{context}_{_fileCounter:D4}{extension}";
            var filePath = Path.Combine(_outputDirectory, filename);

            // Write data to file
            File.WriteAllBytes(filePath, data);

            var result = new Dictionary<string, object>
            {
                ["file"] = filename,
                ["path"] = filePath,
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

        private string GetFileExtension(string context)
        {
            // Determine file extension based on context
            if (context.Contains("sprite") || context.Contains("pixel"))
                return ".png";  // Will need PNG encoding in processor
            if (context.Contains("audio") || context.Contains("wave"))
                return ".wav";
            if (context.Contains("palette"))
                return ".pal";

            return ".bin";  // Default binary extension
        }
    }
}
