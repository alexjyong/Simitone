using FSO.Content;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// The neighbourhood's Bulldoze: a house becomes an empty lot. The game does this in its own code, so here the lot
    /// is opened as usual, emptied, and saved: inside the lot's buildable area, the walls, fences and floors (pools and
    /// ponds too) are cleared on every floor, and everything on the lot is removed except what an empty lot has: its trees (not the flowers, shrubs or
    /// hedges), and the fixtures the game puts on every lot: the mailbox, the newspaper, and the hidden objects (named with a "*": the outdoor
    /// trash can, the phone line and the car and walking portals). The terrain stays as it is, and the controllers
    /// kept off the lot are left alone. The lot's price then comes from its land alone, as the save works out the
    /// rest from what's left. The house's file is copied to UserData/Backups first.
    /// </summary>
    public static class Bulldozer
    {
        private const sbyte TreeCategory = 11; //the Build mode plants tool's catalog category
        private static readonly HashSet<uint> Fixtures = new HashSet<uint>
        {
            0x39CCF441, //MailBox
            0xE6B26801, //Newspaper
            0xA4258067, //*Trash Outside
            0xAED879C5, //*Phone Line
            0x865A6812, //*Portal - Car
            0x81E6BEF9, //*Portal - Pedestrian
        };

        /// <summary>
        /// The tutorial house can't be bulldozed, as in the game.
        /// </summary>
        public static bool Allowed(int houseID) => houseID != 7;

        /// <summary>
        /// Copies the house's file to UserData/Backups, before it's replaced.
        /// </summary>
        public static void Backup(int houseID)
        {
            var nhood = Content.Get().Neighborhood;
            var path = nhood.GetHousePath(houseID);
            if (!File.Exists(path)) return;
            var backup = Path.Combine(nhood.UserPath, "Backups", "Bulldoze " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"));
            Directory.CreateDirectory(backup);
            File.Copy(path, Path.Combine(backup, Path.GetFileName(path)));
        }

        private static bool Keep(VMEntity obj)
        {
            var guid = obj.MasterDefinition?.GUID ?? obj.Object.OBJ.GUID;
            var item = Content.Get().WorldCatalog.GetItemByGUID(guid);
            //the tree tool also sells flowers, shrubs and topiaries; only the trees stay, which their (untranslated)
            //object labels name: "Trees - Pine", "Tree - Curb"
            if (item != null) return item.Value.Category == TreeCategory && obj.Object.OBJ.ChunkLabel.StartsWith("Tree");
            //the hidden objects' names start with a "*" (other towns have their own portals)
            return Fixtures.Contains(guid) || obj.ToString().StartsWith("*") || obj.Object.OBJ.ChunkLabel.StartsWith("*");
        }

        /// <summary>
        /// If the tile is part of the lot rather than the street around it (TS1 lots mark this tile by tile).
        /// </summary>
        private static bool Buildable(VMArchitecture arch, int x, int y)
        {
            if (x < 0 || y < 0 || x >= arch.Width || y >= arch.Height) return false;
            var fine = arch.FineBuildableArea;
            return (fine != null) ? fine[x + y * arch.Width] : arch.BuildableArea.Contains(x, y);
        }

        /// <summary>
        /// Empties the open lot. It still needs saving.
        /// </summary>
        public static void Clear(VM vm)
        {
            var context = vm.Context;
            var arch = context.Architecture;
            var groups = vm.Entities.OfType<VMGameObject>().Select(x => x.MultitileGroup).Distinct().ToList();
            foreach (var group in groups)
            {
                var baseObj = group.BaseObject;
                if (baseObj == null || baseObj.Dead || baseObj.Position == LotTilePos.OUT_OF_WORLD || Keep(baseObj)) continue;
                //the street and sidewalk around the lot belong to the neighbourhood
                if (!Buildable(arch, baseObj.Position.TileX, baseObj.Position.TileY)) continue;
                foreach (var obj in new List<VMEntity>(group.Objects))
                {
                    if (!obj.Dead) obj.Delete(true, context);
                }
            }

            for (sbyte level = 1; level <= arch.Stories; level++)
            {
                for (short y = 0; y < arch.Height; y++)
                {
                    for (short x = 0; x < arch.Width; x++)
                    {
                        if (!Buildable(arch, x, y)) continue; //the road and sidewalk are floors too
                        arch.SetWall(x, y, level, new WallTile());
                        arch.SetFloor(x, y, level, new FloorTile(), true);
                    }
                }
            }
            arch.RegenWallsAt();
            arch.SignalAllDirty();
            arch.Tick();
        }
    }
}
