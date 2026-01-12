using System;

namespace IffExporter
{
    /// <summary>
    /// Export detail level
    /// </summary>
    public enum ExportLevel
    {
        /// <summary>Metadata only - chunk inventory without parsing</summary>
        Metadata = 1,
        /// <summary>Full structured export with all properties</summary>
        Full = 2,
        /// <summary>Deep export with binary data extraction</summary>
        Deep = 3
    }

    /// <summary>
    /// Binary data handling mode
    /// </summary>
    public enum BinaryMode
    {
        /// <summary>Skip binary data entirely</summary>
        Skip,
        /// <summary>Include SHA256 checksums of binary data</summary>
        Checksum,
        /// <summary>Embed binary data as base64 strings</summary>
        Base64,
        /// <summary>Save binary data to external files</summary>
        External
    }

    /// <summary>
    /// Command-line options for IFF export
    /// </summary>
    public class ExportOptions
    {
        public string InputFile { get; set; }
        public string OutputFile { get; set; }
        public ExportLevel Level { get; set; } = ExportLevel.Full;
        public BinaryMode BinaryHandling { get; set; } = BinaryMode.Checksum;
        public string[] FilterChunkTypes { get; set; }
        public ushort[] FilterChunkIds { get; set; }
        public bool PrettyPrint { get; set; } = true;
        public bool Verbose { get; set; } = false;
        public bool SummaryOnly { get; set; } = false;
        public bool IncludeOpcodeNames { get; set; } = true;
        public bool AllLanguages { get; set; } = true;
        public string OutputDirectory { get; set; }

        /// <summary>
        /// Validates the options and sets defaults
        /// </summary>
        public bool Validate(out string error)
        {
            if (string.IsNullOrEmpty(InputFile))
            {
                error = "Input file is required";
                return false;
            }

            if (!System.IO.File.Exists(InputFile))
            {
                error = $"Input file not found: {InputFile}";
                return false;
            }

            if (string.IsNullOrEmpty(OutputFile) && !SummaryOnly)
            {
                OutputFile = System.IO.Path.ChangeExtension(InputFile, ".json");
            }

            if (BinaryHandling == BinaryMode.External && string.IsNullOrEmpty(OutputDirectory))
            {
                OutputDirectory = System.IO.Path.GetDirectoryName(OutputFile)
                    ?? System.IO.Directory.GetCurrentDirectory();
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Prints usage information
        /// </summary>
        public static void PrintUsage()
        {
            Console.WriteLine("IFF Exporter - Convert binary IFF files to human/LLM-readable JSON");
            Console.WriteLine();
            Console.WriteLine("Usage: IffExporter <input.iff> [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -o, --output <file>          Output file (default: input.json)");
            Console.WriteLine("  -l, --level <1|2|3>          Export level (default: 2)");
            Console.WriteLine("                               1 = Metadata only");
            Console.WriteLine("                               2 = Full structured export");
            Console.WriteLine("                               3 = Deep with binary extraction");
            Console.WriteLine("  -b, --binary <mode>          Binary handling (default: checksum)");
            Console.WriteLine("                               skip | checksum | base64 | external");
            Console.WriteLine("  -c, --chunks <types>         Filter chunk types (comma-separated)");
            Console.WriteLine("                               Example: BHAV,STR,OBJD");
            Console.WriteLine("  -i, --ids <ids>              Filter chunk IDs (comma-separated)");
            Console.WriteLine("                               Example: 4096,4097");
            Console.WriteLine("  -p, --pretty                 Pretty-print JSON (default: true)");
            Console.WriteLine("  -v, --verbose                Verbose output");
            Console.WriteLine("  --summary                    Print summary only (no file output)");
            Console.WriteLine("  --no-opcode-names            Skip BHAV opcode name resolution");
            Console.WriteLine("  --primary-language-only      Export only primary language (English US)");
            Console.WriteLine("  --output-dir <dir>           Directory for external files");
            Console.WriteLine("  -h, --help                   Show this help");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  IffExporter chair.iff");
            Console.WriteLine("  IffExporter chair.iff -l 1");
            Console.WriteLine("  IffExporter chair.iff -b external -o chair.json");
            Console.WriteLine("  IffExporter chair.iff -c BHAV,STR --summary");
        }
    }
}
