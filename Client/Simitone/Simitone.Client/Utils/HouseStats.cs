using FSO.Content;
using FSO.SimAntics;
using FSO.LotView.Model;
using FSO.SimAntics.Model;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// The House panel's statistics. The game works its ratings out in its engine, so these follow its descriptions of
    /// them (GameData/Live.iff, string table 135), from what the lot knows:
    /// - Size: the area inside the walls for each Sim living there.
    /// - Furnishings: the indoor rooms' scores, from what's in them (the score the Room need uses), by area.
    /// - Yard: the outdoor score, and a pool.
    ///   (The room score takes 10 off indoors and 15 outside, so a bare room is below zero; the ratings count from there.)
    /// - Upkeep: full unless something needs repairing (or is burning).
    /// - Layout: how far the beds are from the nearest toilet, shower and fridge.
    /// Ratings are 0 to 10. A floor tile counts as 10 square feet.
    /// </summary>
    public class HouseStats
    {
        public int SquareFeet;
        public int Bedrooms;
        public int Bathrooms;
        public Rectangle Lot;
        public int Size;
        public int Furnishings;
        public int Yard;
        public int Upkeep;
        public int Layout;

        public static HouseStats Of(VM vm)
        {
            var stats = new HouseStats();
            var context = vm.Context;
            var catalog = Content.Get().WorldCatalog;
            stats.Lot = BuildableBounds(context.Architecture);

            bool has(VMEntity obj, int category, int bits)
            {
                var guid = (obj.MultitileGroup.MultiTile) ? obj.MasterDefinition?.GUID ?? obj.Object.OBJ.GUID : obj.Object.OBJ.GUID;
                var item = catalog.GetItemByGUID(guid);
                return item != null && item.Value.Category == category && (item.Value.Subsort & bits) != 0;
            }
            bool isBed(VMEntity x) => has(x, 0, 1 << 3);
            bool isToilet(VMEntity x) => has(x, 4, 1);
            bool isShower(VMEntity x) => has(x, 4, 1 << 1);
            bool isFridge(VMEntity x) => has(x, 2, 1 << 1);

            var indoorArea = 0;
            var scoreArea = 0f;
            var outdoorScore = 0;
            var pool = false;
            var scored = new HashSet<ushort>();
            for (int r = 1; r < (context.RoomInfo?.Length ?? 0); r++)
            {
                var info = context.RoomInfo[r];
                var room = info.Room;
                if (room.IsPool)
                {
                    pool = true;
                    continue;
                }
                var score = context.GetRoomScore((ushort)r);
                if (room.IsOutside)
                {
                    outdoorScore = Math.Max(outdoorScore, score + 15);
                    continue;
                }
                indoorArea += room.Area;
                scoreArea += Math.Max(0, score + 10) * room.Area;
                var objects = info.Entities?.Where(x => x is VMGameObject).ToList() ?? new List<VMEntity>();
                if (objects.Any(isBed)) stats.Bedrooms++;
                if (objects.Any(x => isToilet(x) || isShower(x))) stats.Bathrooms++;
            }
            stats.SquareFeet = indoorArea * 10;

            var residents = Math.Max(1, vm.TS1State.CurrentFamily?.FamilyGUIDs.Length ?? 1);
            stats.Size = Clamp(indoorArea / (6f * residents));
            stats.Furnishings = (indoorArea == 0) ? 0 : Clamp(scoreArea / indoorArea / 10f);
            stats.Yard = Clamp(outdoorScore / 10f + (pool ? 2 : 0));

            var objectsOnLot = vm.Entities.Where(x => x is VMGameObject && x.MultitileGroup.BaseObject == x && x.Position != LotTilePos.OUT_OF_WORLD).ToList();
            var broken = objectsOnLot.Count(x => (((VMEntityFlags)x.GetValue(VMStackObjectVariable.Flags)) & (VMEntityFlags.NeedsMaintinance | VMEntityFlags.Burning)) != 0);
            stats.Upkeep = Clamp(10 - broken * 2);

            //from each bed, the nearest of each: 6 tiles or less is ideal, 30 or more the worst; a floor away counts as 6 tiles
            float distance(VMEntity a, VMEntity b) => Vector2.Distance(new Vector2(a.Position.TileX, a.Position.TileY), new Vector2(b.Position.TileX, b.Position.TileY))
                + Math.Abs(a.Position.Level - b.Position.Level) * 6;
            var beds = objectsOnLot.Where(isBed).ToList();
            var needs = new[] { objectsOnLot.Where(isToilet).ToList(), objectsOnLot.Where(isShower).ToList(), objectsOnLot.Where(isFridge).ToList() }.Where(x => x.Count > 0).ToList();
            if (beds.Count > 0 && needs.Count > 0)
            {
                var average = beds.Average(bed => needs.Average(kind => kind.Min(x => distance(bed, x))));
                stats.Layout = Clamp(10 * (30 - average) / 24f);
            }
            return stats;
        }

        /// <summary>
        /// The part of the lot that can be built on (TS1 lots mark it tile by tile).
        /// </summary>
        private static Rectangle BuildableBounds(VMArchitecture arch)
        {
            var fine = arch.FineBuildableArea;
            if (fine == null) return arch.BuildableArea;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int i = 0; i < fine.Length; i++)
            {
                if (!fine[i]) continue;
                int x = i % arch.Width, y = i / arch.Width;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
            }
            return (maxX < 0) ? Rectangle.Empty : new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static int Clamp(float rating) => (int)Math.Max(0, Math.Min(10, Math.Round(rating)));
    }
}
