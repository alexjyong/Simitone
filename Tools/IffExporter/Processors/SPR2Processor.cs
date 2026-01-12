using System;
using System.Collections.Generic;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using IffExporter.BinaryHandlers;

namespace IffExporter.Processors
{
    /// <summary>
    /// Processor for SPR2 (Sprite) chunks
    /// </summary>
    public class SPR2Processor : IChunkProcessor
    {
        public Dictionary<string, object> Process(IffChunk chunk, Dictionary<string, object> basicData,
            ExportOptions options, IBinaryHandler binaryHandler)
        {
            var spr2 = chunk as SPR2;
            if (spr2 == null)
                return basicData;

            var result = new Dictionary<string, object>
            {
                ["chunkId"] = chunk.ChunkID,
                ["chunkLabel"] = chunk.ChunkLabel,
                ["chunkType"] = chunk.ChunkType,
                ["defaultPaletteId"] = spr2.DefaultPaletteID,
                ["frameCount"] = spr2.Frames?.Length ?? 0
            };

            // Process frames
            if (spr2.Frames != null && spr2.Frames.Length > 0)
            {
                var frames = new List<object>();

                for (int i = 0; i < spr2.Frames.Length; i++)
                {
                    var frame = spr2.Frames[i];
                    var frameData = new Dictionary<string, object>
                    {
                        ["index"] = i,
                        ["width"] = frame.Width,
                        ["height"] = frame.Height,
                        ["flags"] = frame.Flags,
                        ["paletteId"] = frame.PaletteID
                    };

                    // Handle pixel data - note: PixelData is Color[], not byte[]
                    // For now, just include metadata about the pixel data
                    if (frame.PixelData != null && frame.PixelData.Length > 0)
                    {
                        frameData["pixelDataInfo"] = new Dictionary<string, object>
                        {
                            ["width"] = frame.Width,
                            ["height"] = frame.Height,
                            ["paletteId"] = frame.PaletteID,
                            ["colorCount"] = frame.PixelData.Length
                        };
                    }

                    // Handle z-buffer data
                    if (frame.ZBufferData != null && frame.ZBufferData.Length > 0)
                    {
                        var metadata = new Dictionary<string, object>
                        {
                            ["width"] = frame.Width,
                            ["height"] = frame.Height
                        };

                        frameData["zBufferData"] = binaryHandler.HandleBinaryData(
                            frame.ZBufferData,
                            $"sprite_{chunk.ChunkID}_{i}_zbuffer",
                            metadata);
                    }

                    // Note: SPR2Frame does not have AlphaData property
                    // Alpha is handled through the PixelData Color array

                    frames.Add(frameData);
                }

                result["frames"] = frames;
            }

            return result;
        }
    }
}
