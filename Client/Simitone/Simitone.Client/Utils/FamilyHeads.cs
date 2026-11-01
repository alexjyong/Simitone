using FSO.Client;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Engine.TSOTransaction;
using FSO.SimAntics.Model;
using FSO.SimAntics.NetPlay.Drivers;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Model;
using System.Collections.Generic;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// The faces of a family's Sims, outside of any lot (for the neighbourhood and its family bin). Each Sim is made
    /// in a scratch VM just long enough to draw its head, like Simitone's own house panel does.
    /// </summary>
    public static class FamilyHeads
    {
        private static Dictionary<uint, (string Name, Texture2D Head)> Cache = new Dictionary<uint, (string, Texture2D)>();

        public static List<(string Name, Texture2D Head)> Of(FAMI family) => Of(new[] { family })[family];

        public static Dictionary<FAMI, List<(string Name, Texture2D Head)>> Of(IEnumerable<FAMI> families)
        {
            var result = new Dictionary<FAMI, List<(string, Texture2D)>>();
            FSO.LotView.World world = null;
            VM vm = null;
            foreach (var family in families)
            {
                var heads = new List<(string, Texture2D)>();
                foreach (var guid in family.FamilyGUIDs)
                {
                    if (!Cache.TryGetValue(guid, out var head))
                    {
                        if (vm == null)
                        {
                            world = new FSO.LotView.World(GameFacade.GraphicsDevice);
                            world.Initialize(GameFacade.Scenes);
                            var context = new VMContext(world);
                            vm = new VM(context, new VMServerDriver(new VMTS1GlobalLinkStub()), new VMNullHeadlineProvider());
                            vm.Init();
                            var blueprint = new Blueprint(1, 1);
                            context.Blueprint = blueprint;
                            context.Architecture = new VMArchitecture(1, 1, blueprint, vm.Context);
                        }
                        var sim = vm.Context.CreateObjectInstance(guid, LotTilePos.OUT_OF_WORLD, Direction.NORTH, true)?.BaseObject;
                        if (sim == null) continue;
                        head = (sim.Name, UIIconCache.GetObject(sim));
                        sim.Delete(true, vm.Context);
                        Cache[guid] = head;
                    }
                    heads.Add(head);
                }
                result[family] = heads;
            }
            world?.Dispose();
            return result;
        }

        /// <summary>
        /// Forgets a Sim's face, after it was changed in Create-a-Sim.
        /// </summary>
        public static void Forget(uint guid) => Cache.Remove(guid);
    }
}
