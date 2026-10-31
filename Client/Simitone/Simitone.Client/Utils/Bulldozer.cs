using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Marshals;
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
    /// rest from what's left. The house's file is copied to UserData/Backups first. A lot with nothing to bulldoze
    /// (IsEmpty) is left alone, as in the game.
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

        private static bool Keep(VMEntity obj) =>
            Keep(obj.MasterDefinition?.GUID ?? obj.Object.OBJ.GUID, obj.Object.OBJ.ChunkLabel ?? "", obj.ToString());

        /// <summary>
        /// If an empty lot has the object (by its main object's GUID), given its part's object label and its name.
        /// </summary>
        private static bool Keep(uint guid, string label, string name)
        {
            var item = Content.Get().WorldCatalog.GetItemByGUID(guid);
            //the tree tool also sells flowers, shrubs and topiaries; only the trees stay, which their (untranslated)
            //object labels name: "Trees - Pine", "Tree - Curb"
            if (item != null) return item.Value.Category == TreeCategory && label.StartsWith("Tree");
            //the hidden objects' names start with a "*" (other towns have their own portals)
            return Fixtures.Contains(guid) || name.StartsWith("*") || label.StartsWith("*");
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

        private static Dictionary<int, bool> EmptyLots = new Dictionary<int, bool>();

        /// <summary>
        /// If the lot has nothing to bulldoze: what Clear leaves, which is also what the game's empty lots have. Kept
        /// until Forget, as reading a lot takes a moment.
        /// </summary>
        public static bool IsEmpty(int houseID)
        {
            if (!EmptyLots.TryGetValue(houseID, out var empty)) EmptyLots[houseID] = empty = FindEmpty(houseID);
            return empty;
        }

        /// <summary>
        /// Lots may have changed since they were read: the neighbourhood is shown again after playing or bulldozing one.
        /// </summary>
        public static void Forget() => EmptyLots.Clear();

        /// <summary>
        /// The game's own saves of a lot have nothing built when their architecture value is 0 (it doesn't count the
        /// trees there). Simitone saves the whole lot (FSOV), whose value does count them, so that's read instead:
        /// no walls or floors on the lot, and no objects there but the ones an empty lot keeps.
        /// </summary>
        private static bool FindEmpty(int houseID)
        {
            var house = Content.Get().Neighborhood.GetHouse(houseID);
            var fsov = house.List<FSOV>()?.FirstOrDefault();
            if (fsov == null) return (house.Get<SIMI>(1)?.ArchitectureValue ?? 0) == 0;
            var lot = new VMMarshal();
            try
            {
                using (var read = new BinaryReader(new MemoryStream(fsov.Data))) lot.Deserialize(read);
            }
            catch (Exception)
            {
                return false; //then bulldozing it is still offered
            }

            var arch = lot.Context.Architecture;
            var fine = arch.FineBuildableArea;
            if (fine == null) return false;
            for (int level = 0; level < arch.Stories; level++)
            {
                for (int i = 0; i < fine.Length; i++)
                {
                    if (fine[i] && (arch.Walls[level][i].Segments != 0 || arch.Floors[level][i].Pattern != 0)) return false;
                }
            }

            var groupNames = new Dictionary<short, string>();
            foreach (var group in lot.MultitileGroups)
            {
                foreach (var id in group.Objects) groupNames[id] = group.Name ?? "";
            }
            foreach (var ent in lot.Entities.OfType<VMGameObjectMarshal>())
            {
                var pos = ent.Position;
                if (pos == LotTilePos.OUT_OF_WORLD || pos.TileX >= arch.Width || pos.TileY >= arch.Height) continue;
                if (!fine[pos.TileX + pos.TileY * arch.Width]) continue;
                var obj = Content.Get().WorldObjects.Get(ent.GUID);
                if (obj == null) continue;
                var label = obj.OBJ.ChunkLabel ?? "";
                //the name as the lot gives it: the group's, else the catalog's, else the label
                groupNames.TryGetValue(ent.ObjectID, out var name);
                if (string.IsNullOrEmpty(name)) name = obj.Resource.Get<CTSS>(obj.OBJ.CatalogStringsID)?.GetString(0) ?? label;
                if (!Keep((ent.MasterGUID != 0) ? ent.MasterGUID : ent.GUID, label, name)) return false;
            }
            return true;
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
