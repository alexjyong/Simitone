using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FSO.Files.Formats.IFF;
using Newtonsoft.Json;
using IffExporter.Processors;
using IffExporter.BinaryHandlers;

namespace IffExporter
{
    /// <summary>
    /// Main serializer that orchestrates IFF file export to JSON
    /// </summary>
    public class IffSerializer
    {
        private readonly ExportOptions _options;
        private readonly ChunkExporter _chunkExporter;
        private readonly Dictionary<Type, IChunkProcessor> _processors;
        private readonly IBinaryHandler _binaryHandler;

        public IffSerializer(ExportOptions options)
        {
            _options = options;
            _chunkExporter = new ChunkExporter(options);
            _processors = InitializeProcessors();
            _binaryHandler = CreateBinaryHandler(options.BinaryHandling);
        }

        /// <summary>
        /// Main export method
        /// </summary>
        public void Export()
        {
            if (_options.Verbose)
            {
                Console.WriteLine($"Loading IFF file: {_options.InputFile}");
            }

            // Load IFF file
            IffFile iff;
            try
            {
                iff = new IffFile(_options.InputFile);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load IFF file: {ex.Message}", ex);
            }

            // Get all chunks
            var allChunks = iff.ListAll();

            if (_options.Verbose)
            {
                Console.WriteLine($"Loaded {allChunks.Count} chunks");
            }

            // Build export data structure
            var exportData = BuildExportData(iff);

            // Output result
            if (_options.SummaryOnly)
            {
                PrintSummary(exportData);
            }
            else
            {
                WriteJsonFile(exportData);
            }
        }

        /// <summary>
        /// Builds the complete export data structure
        /// </summary>
        private Dictionary<string, object> BuildExportData(IffFile iff)
        {
            var result = new Dictionary<string, object>();

            // File-level metadata
            result["file"] = Path.GetFileName(_options.InputFile);
            result["version"] = "2.5"; // IFF version
            result["exportLevel"] = _options.Level.ToString();
            result["exportDate"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            // Get all chunks
            var allChunks = iff.ListAll();
            result["chunkCount"] = allChunks.Count;

            if (_options.Level == ExportLevel.Metadata)
            {
                // Metadata-only export: just list chunks
                result["chunks"] = allChunks.Select(obj =>
                {
                    var chunk = (IffChunk)obj;
                    return new Dictionary<string, object>
                    {
                        ["type"] = chunk.ChunkType,
                        ["id"] = chunk.ChunkID,
                        ["label"] = chunk.ChunkLabel,
                        ["size"] = chunk.ChunkData?.Length ?? 0,
                        ["flags"] = chunk.ChunkFlags
                    };
                }).ToList();
            }
            else
            {
                // Full/Deep export: organize chunks by type
                var chunksByType = new Dictionary<string, List<object>>();

                int processedCount = 0;
                foreach (var chunk in allChunks)
                {
                    // Get chunk type name
                    var chunkTypeName = chunk.ChunkType;

                    // Apply chunk filter if specified
                    if (_options.FilterChunkTypes != null &&
                        !_options.FilterChunkTypes.Contains(chunkTypeName))
                    {
                        continue;
                    }

                    // Apply ID filter if specified
                    if (_options.FilterChunkIds != null &&
                        !_options.FilterChunkIds.Contains(chunk.ChunkID))
                    {
                        continue;
                    }

                    if (_options.Verbose)
                    {
                        processedCount++;
                        if (processedCount % 10 == 0)
                        {
                            Console.Write($"\rProcessing chunk {processedCount}/{allChunks.Count}");
                        }
                    }

                    // Extract chunk properties
                    var chunkData = _chunkExporter.ExtractProperties(chunk);

                    // Apply chunk-specific processor if available
                    var chunkType = chunk.GetType();
                    if (_processors.TryGetValue(chunkType, out var processor))
                    {
                        chunkData = processor.Process(chunk, chunkData, _options, _binaryHandler);
                    }

                    // Add to result
                    if (!chunksByType.ContainsKey(chunkTypeName))
                    {
                        chunksByType[chunkTypeName] = new List<object>();
                    }
                    chunksByType[chunkTypeName].Add(chunkData);
                }

                if (_options.Verbose && processedCount > 0)
                {
                    Console.WriteLine();
                }

                result["chunks"] = chunksByType;
            }

            return result;
        }

        /// <summary>
        /// Prints a summary to console
        /// </summary>
        private void PrintSummary(Dictionary<string, object> data)
        {
            Console.WriteLine($"IFF File: {data["file"]}");
            Console.WriteLine($"Version: {data["version"]}");
            Console.WriteLine($"Total Chunks: {data["chunkCount"]}");

            if (data["chunks"] is Dictionary<string, List<object>> chunksByType)
            {
                Console.WriteLine("\nChunks by Type:");
                foreach (var kvp in chunksByType.OrderBy(k => k.Key))
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value.Count}");
                }
            }
        }

        /// <summary>
        /// Writes the export data to a JSON file
        /// </summary>
        private void WriteJsonFile(Dictionary<string, object> data)
        {
            var json = JsonConvert.SerializeObject(data,
                _options.PrettyPrint ? Formatting.Indented : Formatting.None);

            File.WriteAllText(_options.OutputFile, json);

            if (_options.Verbose)
            {
                var fileInfo = new FileInfo(_options.OutputFile);
                Console.WriteLine($"Wrote {fileInfo.Length:N0} bytes to {_options.OutputFile}");
            }
        }

        /// <summary>
        /// Initializes chunk-specific processors
        /// </summary>
        private Dictionary<Type, IChunkProcessor> InitializeProcessors()
        {
            var processors = new Dictionary<Type, IChunkProcessor>();

            // Register processors for specific chunk types
            // These will be implemented in the next phase
            try
            {
                processors[typeof(FSO.Files.Formats.IFF.Chunks.BHAV)] = new BHAVProcessor();
            } catch { }

            try
            {
                processors[typeof(FSO.Files.Formats.IFF.Chunks.STR)] = new STRProcessor();
            } catch { }

            try
            {
                processors[typeof(FSO.Files.Formats.IFF.Chunks.OBJD)] = new OBJDProcessor();
            } catch { }

            try
            {
                processors[typeof(FSO.Files.Formats.IFF.Chunks.SPR2)] = new SPR2Processor();
            } catch { }

            return processors;
        }

        /// <summary>
        /// Creates the appropriate binary handler based on mode
        /// </summary>
        private IBinaryHandler CreateBinaryHandler(BinaryMode mode)
        {
            switch (mode)
            {
                case BinaryMode.Skip:
                    return new SkipBinaryHandler();
                case BinaryMode.Checksum:
                    return new ChecksumBinaryHandler();
                case BinaryMode.Base64:
                    return new Base64BinaryHandler();
                case BinaryMode.External:
                    return new ExternalFileBinaryHandler(_options.OutputDirectory);
                default:
                    throw new ArgumentException($"Unknown binary mode: {mode}");
            }
        }
    }
}
