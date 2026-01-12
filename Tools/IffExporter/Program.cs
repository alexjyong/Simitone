using System;
using System.IO;
using System.Linq;

namespace IffExporter
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                // Parse command-line arguments
                var options = ParseArguments(args);
                if (options == null)
                {
                    return 1; // Error or help displayed
                }

                // Validate options
                if (!options.Validate(out string error))
                {
                    Console.Error.WriteLine($"Error: {error}");
                    return 1;
                }

                // Create serializer and export
                var serializer = new IffSerializer(options);
                serializer.Export();

                if (options.Verbose && !options.SummaryOnly)
                {
                    Console.WriteLine($"Export complete: {options.OutputFile}");
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (args.Contains("-v") || args.Contains("--verbose"))
                {
                    Console.Error.WriteLine(ex.StackTrace);
                }
                return 1;
            }
        }

        static ExportOptions ParseArguments(string[] args)
        {
            if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
            {
                ExportOptions.PrintUsage();
                return null;
            }

            var options = new ExportOptions();

            // First argument is always the input file (unless it starts with -)
            if (!args[0].StartsWith("-"))
            {
                options.InputFile = args[0];
            }
            else
            {
                Console.Error.WriteLine("Error: Input file is required");
                ExportOptions.PrintUsage();
                return null;
            }

            // Parse remaining arguments
            for (int i = 1; i < args.Length; i++)
            {
                string arg = args[i];

                switch (arg)
                {
                    case "-o":
                    case "--output":
                        if (i + 1 < args.Length)
                        {
                            options.OutputFile = args[++i];
                        }
                        else
                        {
                            Console.Error.WriteLine("Error: --output requires a value");
                            return null;
                        }
                        break;

                    case "-l":
                    case "--level":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out int level) && level >= 1 && level <= 3)
                        {
                            options.Level = (ExportLevel)level;
                            i++;
                        }
                        else
                        {
                            Console.Error.WriteLine("Error: --level requires a value (1, 2, or 3)");
                            return null;
                        }
                        break;

                    case "-b":
                    case "--binary":
                        if (i + 1 < args.Length)
                        {
                            string mode = args[++i].ToLower();
                            switch (mode)
                            {
                                case "skip":
                                    options.BinaryHandling = BinaryMode.Skip;
                                    break;
                                case "checksum":
                                    options.BinaryHandling = BinaryMode.Checksum;
                                    break;
                                case "base64":
                                    options.BinaryHandling = BinaryMode.Base64;
                                    break;
                                case "external":
                                    options.BinaryHandling = BinaryMode.External;
                                    break;
                                default:
                                    Console.Error.WriteLine($"Error: Invalid binary mode: {mode}");
                                    return null;
                            }
                        }
                        else
                        {
                            Console.Error.WriteLine("Error: --binary requires a value");
                            return null;
                        }
                        break;

                    case "-c":
                    case "--chunks":
                        if (i + 1 < args.Length)
                        {
                            options.FilterChunkTypes = args[++i].Split(',');
                        }
                        else
                        {
                            Console.Error.WriteLine("Error: --chunks requires a value");
                            return null;
                        }
                        break;

                    case "-i":
                    case "--ids":
                        if (i + 1 < args.Length)
                        {
                            try
                            {
                                options.FilterChunkIds = args[++i].Split(',').Select(s => ushort.Parse(s)).ToArray();
                            }
                            catch
                            {
                                Console.Error.WriteLine("Error: --ids requires comma-separated numbers");
                                return null;
                            }
                        }
                        else
                        {
                            Console.Error.WriteLine("Error: --ids requires a value");
                            return null;
                        }
                        break;

                    case "-p":
                    case "--pretty":
                        options.PrettyPrint = true;
                        break;

                    case "-v":
                    case "--verbose":
                        options.Verbose = true;
                        break;

                    case "--summary":
                        options.SummaryOnly = true;
                        break;

                    case "--no-opcode-names":
                        options.IncludeOpcodeNames = false;
                        break;

                    case "--primary-language-only":
                        options.AllLanguages = false;
                        break;

                    case "--output-dir":
                        if (i + 1 < args.Length)
                        {
                            options.OutputDirectory = args[++i];
                        }
                        else
                        {
                            Console.Error.WriteLine("Error: --output-dir requires a value");
                            return null;
                        }
                        break;

                    default:
                        Console.Error.WriteLine($"Error: Unknown option: {arg}");
                        ExportOptions.PrintUsage();
                        return null;
                }
            }

            return options;
        }
    }
}
