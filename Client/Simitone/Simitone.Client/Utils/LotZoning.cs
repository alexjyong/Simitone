using FSO.Content;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using System;
using System.IO;
using System.Linq;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// The neighbourhood's Rezone (Unleashed): an empty lot becomes a residential or a community lot. The zoning is the
    /// neighbourhood's LotZoning.iff, string table 1, one "58, community" or "59, residential" line for each lot in each
    /// language; the street or lot name in each line's comment is kept. The file is copied to UserData/Backups first.
    /// </summary>
    public static class LotZoning
    {
        private const string Residential = "residential";
        private const string Community = "community";

        public static void Set(int houseID, bool community)
        {
            var nhood = Content.Get().Neighborhood;
            var path = Path.Combine(nhood.UserPath, "LotZoning.iff");
            var iff = new IffFile(path);
            var zones = iff.Get<STR>(1);
            if (zones == null) return;

            var backup = Path.Combine(nhood.UserPath, "Backups", "Rezone " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"));
            Directory.CreateDirectory(backup);
            File.Copy(path, Path.Combine(backup, "LotZoning.iff"), true);

            var line = houseID + ", " + (community ? Community : Residential);
            var found = false;
            foreach (var set in zones.LanguageSets.Where(x => x?.Strings != null))
            {
                foreach (var item in set.Strings)
                {
                    if (item.Value.Split(',')[0].Trim() != houseID.ToString()) continue;
                    item.Value = line;
                    found = true;
                }
            }
            //a lot the file doesn't list yet goes on the end, in each language that has the others
            if (!found) zones.InsertString(zones.Length, new STRItem { Value = line, Comment = "" });

            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                iff.Write(stream);
            nhood.ZoningDictionary[(short)houseID] = (short)(community ? 1 : 0);
        }
    }
}
