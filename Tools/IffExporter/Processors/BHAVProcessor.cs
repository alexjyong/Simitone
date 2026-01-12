using System;
using System.Collections.Generic;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using IffExporter.BinaryHandlers;

namespace IffExporter.Processors
{
    /// <summary>
    /// Processor for BHAV (Behavior) chunks - disassembles SimAntics bytecode
    /// </summary>
    public class BHAVProcessor : IChunkProcessor
    {
        // Opcode names from PrimitiveRegistry (simplified mapping)
        private static readonly Dictionary<ushort, string> OpcodeNames = new Dictionary<ushort, string>
        {
            {0, "Sleep"},
            {1, "GenericSimsOnlineCall"},
            {2, "Expression"},
            {3, "ReportMetric"},
            {4, "Grab"},
            {5, "Drop"},
            {6, "ChangeSuit"},
            {7, "Refresh"},
            {8, "RandomNumber"},
            {11, "GetDistanceTo"},
            {12, "GetDirectionTo"},
            {13, "PushInteraction"},
            {14, "FindBestObjectForFunction"},
            {15, "Breakpoint"},
            {16, "FindLocationFor"},
            {17, "IdleForInput"},
            {18, "RemoveObjectInstance"},
            {20, "RunFunctionalTree"},
            {21, "ShowString"},
            {22, "LookTowards"},
            {23, "PlaySoundEvent"},
            {24, "OldRelationship"},
            {25, "TransferFunds"},
            {26, "Relationship"},
            {27, "GotoRelativePosition"},
            {28, "RunTreeByName"},
            {29, "SetMotiveChange"},
            {31, "SetToNext"},
            {32, "TestObjectType"},
            {36, "Dialog"},
            {38, "Dialog2"},
            {39, "Dialog3"},
            {41, "SetBalloonHeadline"},
            {42, "CreateObjectInstance"},
            {43, "DropOnto"},
            {44, "AnimateSim"},
            {45, "GotoRoutingSlot"},
            {46, "Snap"},
            {49, "NotifyOutOfIdle"},
            {50, "ChangeActionString"},
            {51, "TS1InventoryOperations"},
            {62, "InvokePlugin"},
            {67, "TSOInventoryOperations"}
        };

        public Dictionary<string, object> Process(IffChunk chunk, Dictionary<string, object> basicData,
            ExportOptions options, IBinaryHandler binaryHandler)
        {
            var bhav = chunk as BHAV;
            if (bhav == null)
                return basicData;

            var result = new Dictionary<string, object>
            {
                ["chunkId"] = chunk.ChunkID,
                ["chunkLabel"] = chunk.ChunkLabel,
                ["chunkType"] = chunk.ChunkType,
                ["type"] = bhav.Type,
                ["args"] = bhav.Args,
                ["locals"] = bhav.Locals,
                ["version"] = bhav.Version,
                ["instructionCount"] = bhav.Instructions?.Length ?? 0
            };

            // Process instructions
            if (bhav.Instructions != null && bhav.Instructions.Length > 0)
            {
                var instructions = new List<object>();

                for (int i = 0; i < bhav.Instructions.Length; i++)
                {
                    var inst = bhav.Instructions[i];
                    var instData = new Dictionary<string, object>
                    {
                        ["index"] = i,
                        ["opcode"] = inst.Opcode,
                        ["truePointer"] = inst.TruePointer,
                        ["falsePointer"] = inst.FalsePointer
                    };

                    // Include opcode name if requested
                    if (options.IncludeOpcodeNames)
                    {
                        if (OpcodeNames.TryGetValue(inst.Opcode, out string opcodeName))
                        {
                            instData["opcodeName"] = opcodeName;
                        }
                        else if (inst.Opcode >= 256)
                        {
                            instData["opcodeName"] = $"SubRoutine_{inst.Opcode}";
                        }
                        else
                        {
                            instData["opcodeName"] = $"Unknown_{inst.Opcode}";
                        }
                    }

                    // Format operand as hex string
                    if (inst.Operand != null && inst.Operand.Length > 0)
                    {
                        instData["operand"] = BitConverter.ToString(inst.Operand).Replace("-", " ");

                        // Also include as array for easier parsing
                        instData["operandBytes"] = inst.Operand;
                    }

                    instructions.Add(instData);
                }

                result["instructions"] = instructions;
            }

            return result;
        }
    }
}
