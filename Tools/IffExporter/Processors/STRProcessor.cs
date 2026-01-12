using System;
using System.Collections.Generic;
using System.Linq;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using IffExporter.BinaryHandlers;

namespace IffExporter.Processors
{
    /// <summary>
    /// Processor for STR# (String Table) chunks
    /// </summary>
    public class STRProcessor : IChunkProcessor
    {
        public Dictionary<string, object> Process(IffChunk chunk, Dictionary<string, object> basicData,
            ExportOptions options, IBinaryHandler binaryHandler)
        {
            var str = chunk as STR;
            if (str == null)
                return basicData;

            var result = new Dictionary<string, object>
            {
                ["chunkId"] = chunk.ChunkID,
                ["chunkLabel"] = chunk.ChunkLabel,
                ["chunkType"] = chunk.ChunkType,
                ["length"] = str.Length
            };

            // Extract language sets
            var languageSets = new List<object>();

            for (int i = 0; i < str.LanguageSets.Length; i++)
            {
                var langSet = str.LanguageSets[i];
                if (langSet?.Strings == null || langSet.Strings.Length == 0)
                    continue;

                // If only primary language is requested, skip others
                if (!options.AllLanguages && i > 0)
                    break;

                var langData = new Dictionary<string, object>
                {
                    ["languageCode"] = i + 1,
                    ["language"] = i < STR.LanguageSetNames.Length ? STR.LanguageSetNames[i] : $"Language {i + 1}"
                };

                var strings = new List<object>();
                foreach (var strItem in langSet.Strings)
                {
                    var item = new Dictionary<string, object>
                    {
                        ["value"] = strItem.Value ?? ""
                    };

                    if (!string.IsNullOrEmpty(strItem.Comment))
                    {
                        item["comment"] = strItem.Comment;
                    }

                    strings.Add(item);
                }

                langData["strings"] = strings;
                languageSets.Add(langData);
            }

            result["languageSets"] = languageSets;

            return result;
        }
    }
}
