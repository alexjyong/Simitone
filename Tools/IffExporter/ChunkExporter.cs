using System;
using System.Collections.Generic;
using System.Reflection;
using FSO.Files.Formats.IFF;

namespace IffExporter
{
    /// <summary>
    /// Uses reflection to extract properties from IFF chunks
    /// </summary>
    public class ChunkExporter
    {
        private readonly ExportOptions _options;
        private readonly HashSet<object> _visitedObjects;

        public ChunkExporter(ExportOptions options)
        {
            _options = options;
            _visitedObjects = new HashSet<object>();
        }

        /// <summary>
        /// Extracts all public properties/fields from a chunk using reflection
        /// </summary>
        public Dictionary<string, object> ExtractProperties(IffChunk chunk)
        {
            var result = new Dictionary<string, object>();
            var type = chunk.GetType();
            
            // Clear visited objects for each new chunk
            _visitedObjects.Clear();

            // Always include basic chunk info
            result["chunkId"] = chunk.ChunkID;
            result["chunkLabel"] = chunk.ChunkLabel;
            result["chunkType"] = chunk.ChunkType;
            result["chunkFlags"] = chunk.ChunkFlags;

            // For metadata level, that's all we need
            if (_options.Level == ExportLevel.Metadata)
            {
                result["chunkSize"] = chunk.ChunkData?.Length ?? 0;
                return result;
            }

            // Extract all public fields
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var field in fields)
            {
                // Skip inherited IffChunk fields (already handled above)
                if (field.DeclaringType == typeof(IffChunk))
                    continue;

                // Skip runtime-only fields
                if (field.Name == "RuntimeTree" || field.Name == "ChunkProcessed" ||
                    field.Name == "ChunkParent" || field.Name == "RuntimeInfo")
                    continue;

                try
                {
                    var value = field.GetValue(chunk);
                    result[field.Name] = ProcessValue(value, field.FieldType);
                }
                catch (Exception ex)
                {
                    if (_options.Verbose)
                    {
                        Console.WriteLine($"Warning: Could not extract field {field.Name}: {ex.Message}");
                    }
                }
            }

            // Extract all public properties
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                // Skip inherited IffChunk properties
                if (prop.DeclaringType == typeof(IffChunk))
                    continue;

                // Skip indexers and properties with parameters
                if (prop.GetIndexParameters().Length > 0)
                    continue;

                // Skip write-only properties
                if (!prop.CanRead)
                    continue;

                try
                {
                    var value = prop.GetValue(chunk);
                    result[prop.Name] = ProcessValue(value, prop.PropertyType);
                }
                catch (Exception ex)
                {
                    if (_options.Verbose)
                    {
                        Console.WriteLine($"Warning: Could not extract property {prop.Name}: {ex.Message}");
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Processes a value for JSON serialization, handling special types
        /// </summary>
        private object ProcessValue(object value, Type type)
        {
            if (value == null)
                return null;

            // Handle primitive types
            if (type.IsPrimitive || type == typeof(string))
                return value;

            // Handle enums
            if (type.IsEnum)
                return value.ToString();

            // Handle byte arrays (binary data)
            if (type == typeof(byte[]))
            {
                // Will be handled by binary handlers later
                return null;
            }

            // Check for circular references (only for reference types we'll recurse into)
            if (type.IsClass && !_visitedObjects.Add(value))
            {
                // Object already visited, return reference indicator to avoid infinite recursion
                return $"<CircularReference:{type.Name}>";
            }

            // Handle arrays
            if (type.IsArray)
            {
                var array = (Array)value;
                var list = new List<object>();
                foreach (var item in array)
                {
                    list.Add(ProcessValue(item, type.GetElementType()));
                }
                return list;
            }

            // Handle lists/collections
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && type != typeof(string))
            {
                var list = new List<object>();
                foreach (var item in (System.Collections.IEnumerable)value)
                {
                    if (item != null)
                    {
                        list.Add(ProcessValue(item, item.GetType()));
                    }
                }
                return list;
            }

            // For complex objects, recursively extract their properties
            if (type.IsClass && type != typeof(string))
            {
                var dict = new Dictionary<string, object>();
                var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (var field in fields)
                {
                    try
                    {
                        var fieldValue = field.GetValue(value);
                        dict[field.Name] = ProcessValue(fieldValue, field.FieldType);
                    }
                    catch { }
                }
                return dict;
            }

            // Default: convert to string
            return value.ToString();
        }
    }
}
