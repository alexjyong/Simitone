using FSO.Client;
using FSO.Content;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using FSO.SimAntics.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// The neighbourhood's Import Family: a family file (.FAM) in the neighbourhood's Import folder, such as one from the
    /// Sims Exchange, joins the neighbourhood. A family file is a house file with the family's records added: the family
    /// (FAMI, FAMs), its Sims' neighbour records (NBRS, with their personality, skills and relationships), and for each
    /// Sim, by its neighbour ID there, its body strings (uChr), its name and bio (CTSS, translated 2000 on) and its face
    /// (BMP_, which isn't used: Simitone draws the Sims' heads itself). The Sims' own character files aren't in it, so
    /// each is made the way Create-a-Sim makes one, from the template person. The house goes on the lot it was saved on
    /// (its number in FAMI), displacing a family or vacant house there; a family without a house, or whose lot isn't a
    /// residential lot here (or is the tutorial house), goes to the family bin. The Sims keep their GUIDs, which the
    /// house's saved Sims refer to, so a family whose Sims are already in the neighbourhood isn't imported twice. The
    /// house and the neighbourhood are copied to UserData/Backups first, and the family file is moved there after.
    /// </summary>
    public class FamilyImport
    {
        //the family's records in a family file, which a house file doesn't have
        private static readonly string[] FamilyChunks = { "FAMI", "FAMs", "FAMh", "NBRS", "uChr", "EXPi", "CTSS", "rsmp" };

        public string FilePath;
        public string Name;
        public FAMI Family;
        public bool HasHouse;
        private Dictionary<uint, Neighbour> Sims = new Dictionary<uint, Neighbour>();
        private Dictionary<short, STR> Bodies = new Dictionary<short, STR>();
        private IffFile Iff;

        /// <summary>
        /// The family files waiting in the neighbourhood's Import folder. Ones that can't be read are left out.
        /// </summary>
        public static List<FamilyImport> Find()
        {
            var folder = Folder();
            var result = new List<FamilyImport>();
            if (!Directory.Exists(folder)) return result;
            foreach (var file in Directory.EnumerateFiles(folder).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                if (!Path.GetExtension(file).Equals(".fam", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    var fam = new FamilyImport(file);
                    if (fam.Family != null && fam.Family.FamilyGUIDs.Length > 0) result.Add(fam);
                }
                catch (Exception) { }
            }
            return result;
        }

        public static string Folder() => Path.Combine(Content.Get().Neighborhood.UserPath, "Import");

        private FamilyImport(string path)
        {
            FilePath = path;
            var data = File.ReadAllBytes(path);
            Iff = new IffFile();
            Iff.Read(new MemoryStream(data));
            Family = Iff.List<FAMI>()?.FirstOrDefault();
            if (Family == null) return;
            Name = Iff.Get<FAMs>(Family.ChunkID)?.GetString(0) ?? Path.GetFileNameWithoutExtension(path);
            var neighbours = Iff.List<NBRS>()?.FirstOrDefault();
            foreach (var guid in Family.FamilyGUIDs)
            {
                var sim = neighbours?.Entries.FirstOrDefault(x => x.GUID == guid);
                if (sim == null) throw new Exception("A Sim in the family has no neighbour record.");
                Sims[guid] = sim;
            }
            foreach (var chunk in Chunks(data))
            {
                if (chunk.Type == "HOUS") HasHouse = true;
                if (chunk.Type != "uChr") continue;
                //FreeSO doesn't read this chunk type, but it's a string table
                var body = new STR();
                body.Read(Iff, new MemoryStream(data, chunk.Offset + 76, chunk.Size - 76));
                Bodies[chunk.ID] = body;
            }
        }

        /// <summary>
        /// The family's Sims' names, from their name and bio (their character's catalog strings).
        /// </summary>
        public List<string> Members() => Family.FamilyGUIDs.Select(x => NameAndBio(Sims[x]).Item1).ToList();

        private Tuple<string, string> NameAndBio(Neighbour sim)
        {
            //the translated table, 2000 on from the neighbour ID; some files only have the one at the ID
            var strings = Iff.Get<CTSS>((ushort)(2000 + sim.NeighbourID)) ?? Iff.Get<CTSS>((ushort)sim.NeighbourID);
            return new Tuple<string, string>(strings?.GetString(0) ?? sim.Name, strings?.GetString(1) ?? "");
        }

        /// <summary>
        /// If the family's Sims are already in the neighbourhood (or anything else has their GUIDs).
        /// </summary>
        public bool AlreadyHere()
        {
            var objects = Content.Get().WorldObjects;
            var nhood = Content.Get().Neighborhood;
            return Family.FamilyGUIDs.Any(x => objects.Entries.ContainsKey(x) || nhood.Neighbors.Entries.Any(y => y.GUID == x));
        }

        /// <summary>
        /// The lot the house goes on: the one it was saved on, if that's a residential lot here other than the tutorial
        /// house. 0 for the family bin.
        /// </summary>
        public int Lot()
        {
            var house = Family.HouseNumber;
            if (!HasHouse || house <= 0 || !Bulldozer.Allowed(house) || HouseRollover.IsCommunity(house)) return 0;
            var lots = Content.Get().Neighborhood.LotLocations.Get<STR>(4);
            for (int i = 0; i < (lots?.Length ?? 0); i++)
            {
                if (int.TryParse(lots.GetString(i).Split(',')[0].Trim(), out var lot) && lot == house) return house;
            }
            return 0;
        }

        /// <summary>
        /// Adds the family, and puts its house on the lot (0 for none).
        /// </summary>
        public void Import(int lot)
        {
            var nhood = Content.Get().Neighborhood;
            var backup = Path.Combine(nhood.UserPath, "Backups", "Import " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"));
            Directory.CreateDirectory(backup);
            File.Copy(Path.Combine(nhood.UserPath, "Neighborhood.iff"), Path.Combine(backup, "Neighborhood.iff"), true);

            if (lot != 0)
            {
                var housePath = nhood.GetHousePath(lot);
                if (File.Exists(housePath)) File.Copy(housePath, Path.Combine(backup, Path.GetFileName(housePath)), true);
                //the family living there moves out, as when evicted
                var occupant = nhood.GetFamilyForHouse((short)lot);
                if (occupant != null)
                {
                    occupant.Budget += occupant.ValueInArch;
                    occupant.ValueInArch = 0;
                    nhood.MoveOut((short)lot);
                }
                File.WriteAllBytes(housePath, HouseOf(File.ReadAllBytes(FilePath)));
            }

            var family = SimitoneNeighbourGenerator.CreateFamily(Name, Family.FamilyGUIDs.Length);
            family.FamilyGUIDs = Family.FamilyGUIDs.ToArray();
            family.FamilyFriends = Family.FamilyFriends;
            //a house left behind is sold, as when a family is evicted
            family.Budget = Family.Budget + ((lot == 0) ? Family.ValueInArch : 0);
            family.ValueInArch = (lot == 0) ? 0 : Family.ValueInArch;
            family.Unknown = (lot == 0) ? (Family.Unknown & ~1) : (Family.Unknown | 1); //1: has a house
            if (lot != 0) nhood.SetFamilyForHouse((short)lot, family, false);

            //each Sim's character, made from the template person with the Sim's own body strings
            var template = Content.Get().WorldObjects.Get(SimitoneNeighbourGenerator.TEMPLATE_GUID);
            var templateBody = template.Resource.Get<STR>(200);
            var templateStrings = Enumerable.Range(0, templateBody.Length).Select(x => templateBody.GetString(x)).ToList();
            var ids = new Dictionary<int, short>();
            var added = new List<Tuple<Neighbour, Neighbour>>();
            foreach (var guid in family.FamilyGUIDs)
            {
                var sim = Sims[guid];
                var name = NameAndBio(sim);
                //(the body strings the constructor picks are all replaced)
                var info = new SimTemplateCreateInfo("kat", false) { Name = name.Item1, Bio = name.Item2, BodyStringReplace = new Dictionary<int, string>() };
                if (Bodies.TryGetValue(sim.NeighbourID, out var body))
                {
                    for (int i = 0; i < body.Length; i++) info.BodyStringReplace[i] = body.GetString(i);
                }
                var file = "user" + nhood.NextSim.ToString().PadLeft(5, '0');
                SimitoneNeighbourGenerator.PrepareTemplatePerson(guid, info);
                for (int i = 0; i < templateStrings.Count; i++) templateBody.SetString(i, templateStrings[i]);

                var data = (short[])(sim.PersonData ?? new short[88]).Clone();
                data[61] = (short)family.ChunkID; //the family
                var neighbour = SimitoneNeighbourGenerator.AddNeighbor(guid, sim.PersonMode, data);
                neighbour.Name = file;
                ids[sim.NeighbourID] = neighbour.NeighbourID;
                added.Add(new Tuple<Neighbour, Neighbour>(sim, neighbour));
            }
            //their relationships with each other, by their new neighbour IDs; the ones with the Sims of the
            //neighbourhood they came from don't apply here
            foreach (var pair in added)
            {
                pair.Item2.Relationships = (pair.Item1.Relationships ?? new Dictionary<int, List<short>>())
                    .Where(x => ids.ContainsKey(x.Key))
                    .ToDictionary(x => (int)ids[x.Key], x => x.Value.ToList());
            }
            nhood.SaveNeighbourhood(false);
            File.Move(FilePath, Path.Combine(backup, Path.GetFileName(FilePath)), true);
        }

        private struct Chunk
        {
            public string Type;
            public short ID;
            public int Offset;
            public int Size;
        }

        private static IEnumerable<Chunk> Chunks(byte[] iff)
        {
            var pos = 64;
            while (pos + 76 <= iff.Length)
            {
                var size = (iff[pos + 4] << 24) | (iff[pos + 5] << 16) | (iff[pos + 6] << 8) | iff[pos + 7];
                if (size < 76 || pos + size > iff.Length) yield break;
                yield return new Chunk
                {
                    Type = Encoding.ASCII.GetString(iff, pos, 4),
                    ID = (short)((iff[pos + 8] << 8) | iff[pos + 9]),
                    Offset = pos,
                    Size = size
                };
                pos += size;
            }
        }

        /// <summary>
        /// A family file without the family's records (and the Sims' faces, which have their neighbour IDs): the house
        /// file. It's copied chunk by chunk, as FreeSO only reads (and so can only write) the chunk types it knows. The
        /// resource map is left out, as it would point at the chunks taken out; FreeSO doesn't use it, and doesn't write
        /// one either.
        /// </summary>
        public static byte[] HouseOf(byte[] fam)
        {
            var chunks = Chunks(fam).ToList();
            var faces = new HashSet<short>(chunks.Where(x => x.Type == "uChr").Select(x => x.ID));
            using (var output = new MemoryStream())
            {
                output.Write(fam, 0, 60);
                output.Write(new byte[4], 0, 4);
                foreach (var chunk in chunks)
                {
                    if (FamilyChunks.Contains(chunk.Type) || (chunk.Type == "BMP_" && faces.Contains(chunk.ID))) continue;
                    output.Write(fam, chunk.Offset, chunk.Size);
                }
                return output.ToArray();
            }
        }
    }
}
