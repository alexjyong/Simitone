using FSO.Client;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Model;
using Microsoft.Xna.Framework.Graphics;
using Simitone.Client.UI.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// TS1's Export HTML option: web pages about the families, written to UserData/Export when a family's lot is saved.
    /// The game filled in the templates in "UserData/Web Templates", which rely on Netscape and old Internet Explorer
    /// scripting that today's browsers don't run, so these are plain pages with the same things in them: an address
    /// book of the neighbourhood's families, and for each family that's been saved its money, members (face, details,
    /// personality, skills and job), house (with the House panel's ratings) and photo album.
    /// </summary>
    public static class FamilyWebPages
    {
        private static readonly (string Name, VMPersonDataVariable Var)[] Traits =
        {
            ("Neat", VMPersonDataVariable.NeatPersonality), ("Outgoing", VMPersonDataVariable.OutgoingPersonality), ("Active", VMPersonDataVariable.ActivePersonality),
            ("Playful", VMPersonDataVariable.PlayfulPersonality), ("Nice", VMPersonDataVariable.NicePersonality)
        };
        private static readonly (string Name, VMPersonDataVariable Var)[] Skills =
        {
            ("Cooking", VMPersonDataVariable.CookingSkill), ("Mechanical", VMPersonDataVariable.MechanicalSkill), ("Charisma", VMPersonDataVariable.CharismaSkill),
            ("Body", VMPersonDataVariable.BodySkill), ("Logic", VMPersonDataVariable.LogicSkill), ("Creativity", VMPersonDataVariable.CreativitySkill)
        };

        private const string Style = @"<style>
body { font-family: 'Comic Sans MS', 'Comic Neue', sans-serif; background: #2a3a8c; color: #fff; margin: 0; padding: 16px; }
main { max-width: 860px; margin: 0 auto; background: #1a2468; border: 2px solid #7890c8; border-radius: 10px; padding: 16px 20px; }
h1, h2 { color: #fff99d; margin: 0.4em 0; } a { color: #9ef0ff; }
table { border-collapse: collapse; width: 100%; } td, th { padding: 4px 8px; text-align: left; vertical-align: top; }
tr:nth-child(even) { background: rgba(255,255,255,0.05); }
.sim { display: flex; gap: 16px; border-top: 1px solid #4a5ca8; padding: 12px 0; flex-wrap: wrap; }
.sim img { width: 74px; height: 74px; background: #0c1240; border-radius: 6px; }
.bar { display: inline-block; width: 100px; height: 8px; background: #0c1240; border-radius: 4px; vertical-align: middle; }
.bar span { display: block; height: 100%; background: #30e860; border-radius: 4px; }
.album { display: flex; flex-wrap: wrap; gap: 12px; } .album figure { margin: 0; width: 200px; }
.album img, .house img { max-width: 100%; border: 2px solid #7890c8; } figcaption { font-size: 0.85em; }
</style>";

        /// <summary>
        /// Writes the pages for the family on the lot that was just saved, and the address book.
        /// </summary>
        /// <param name="houseThumb">the lot's picture, as the save made it</param>
        public static void Export(VM vm, Texture2D houseThumb)
        {
            var family = vm.TS1State.CurrentFamily;
            var nhood = Content.Get().Neighborhood;
            var root = Path.Combine(nhood.UserPath, "Export");
            Directory.CreateDirectory(root);
            if (family != null && family.HouseNumber > 0) ExportFamily(vm, family, houseThumb, root);
            ExportAddressBook(root);
        }

        private static string Html(string text) => WebUtility.HtmlEncode(text ?? "");

        private static string FamilyName(FAMI family)
        {
            return Content.Get().Neighborhood.GetFamilyString(family.ChunkID)?.GetString(0) ?? ("Family " + family.ChunkID);
        }

        private static string Folder(FAMI family)
        {
            var bad = Path.GetInvalidFileNameChars();
            return new string(FamilyName(family).Select(x => bad.Contains(x) || x == ' ' ? '_' : x).ToArray()) + "_" + family.ChunkID;
        }

        private static string Page(string title, string body)
        {
            return "<!doctype html>\n<html><head><meta charset=\"utf-8\"><title>" + Html(title) + "</title>" + Style + "</head><body><main>\n" + body + "\n</main></body></html>\n";
        }

        private static void ExportAddressBook(string root)
        {
            var nhood = Content.Get().Neighborhood;
            var families = (nhood.MainResource.List<FAMI>() ?? new List<FAMI>()).Where(x => x.HouseNumber > 0).OrderBy(x => x.HouseNumber).ToList();
            var body = new StringBuilder("<h1>Address Book</h1>\n<table><tr><th>Family</th><th>Address</th><th>Members</th></tr>\n");
            foreach (var family in families)
            {
                var name = Html(FamilyName(family));
                var page = Path.Combine(root, Folder(family), "index.html");
                var link = File.Exists(page) ? "<a href=\"" + Folder(family) + "/index.html\">" + name + "</a>" : name;
                body.Append("<tr><td>" + link + "</td><td>" + family.HouseNumber + " Sim Lane</td><td>" + family.FamilyGUIDs.Length + "</td></tr>\n");
            }
            body.Append("</table>\n<p>Families get a page when their lot is saved with Export HTML on.</p>");
            File.WriteAllText(Path.Combine(root, "index.html"), Page("Address Book", body.ToString()));
        }

        private static void ExportFamily(VM vm, FAMI family, Texture2D houseThumb, string root)
        {
            var nhood = Content.Get().Neighborhood;
            var dir = Path.Combine(root, Folder(family));
            Directory.CreateDirectory(dir);
            var name = FamilyName(family);
            var body = new StringBuilder();
            body.Append("<p><a href=\"../index.html\">Address Book</a></p>\n<h1>The " + Html(name) + " Family</h1>\n");
            body.Append("<p>Simoleons: §" + family.Budget + " &nbsp; Family friends: " + family.FamilyFriends + " &nbsp; Members: " + family.FamilyGUIDs.Length + "</p>\n");

            body.Append("<h2>Family Members</h2>\n");
            for (int i = 0; i < family.FamilyGUIDs.Length; i++)
            {
                var guid = family.FamilyGUIDs[i];
                var nid = nhood.GetNeighborIDForGUID(guid);
                var neighbour = (nid == null) ? null : nhood.GetNeighborByID(nid.Value);
                var data = neighbour?.PersonData;
                short get(VMPersonDataVariable v) => (data != null && (int)v < data.Length) ? data[(int)v] : (short)0;

                var obj = Content.Get().WorldObjects.Get(guid);
                var strings = obj?.Resource.Get<CTSS>(obj.OBJ.CatalogStringsID);
                var simName = strings?.GetString(0) ?? neighbour?.Name ?? "";
                var face = "member" + (i + 1) + ".png";
                if (!SaveFace(vm, guid, Path.Combine(dir, face))) face = null;

                body.Append("<div class=\"sim\">");
                body.Append(face == null ? "<div style=\"width:74px\"></div>" : "<img src=\"" + face + "\" alt=\"\">");
                body.Append("<div style=\"flex:1;min-width:240px\"><h2>" + Html(simName) + "</h2>");
                var adult = get(VMPersonDataVariable.PersonsAge) >= 18;
                var sign = ZodiacSign.Of(get(VMPersonDataVariable.NeatPersonality) / 100, get(VMPersonDataVariable.OutgoingPersonality) / 100,
                    get(VMPersonDataVariable.ActivePersonality) / 100, get(VMPersonDataVariable.PlayfulPersonality) / 100, get(VMPersonDataVariable.NicePersonality) / 100);
                body.Append("<p>" + (get(VMPersonDataVariable.Gender) == 1 ? "Female" : "Male") + ", " + (adult ? "adult" : "child")
                    + ((data != null) ? ", " + Html(ZodiacSign.Name(sign)) : "") + "</p>");
                body.Append("<p>" + Html(strings?.GetString(1) ?? "") + "</p>");
                var job = Content.Get().Jobs.GetJob((ushort)get(VMPersonDataVariable.JobType));
                var level = get(VMPersonDataVariable.JobPromotionLevel);
                if (job != null && level >= 0 && level < job.JobLevels.Length)
                {
                    var l = job.JobLevels[level];
                    body.Append("<p>" + Html(l.JobName) + " (" + Html(job.Name) + ", level " + (level + 1) + "), §" + l.Salary + " a day, performance "
                        + get(VMPersonDataVariable.JobPerformance) + "</p>");
                }
                body.Append("</div><div><table>");
                foreach (var trait in Traits) body.Append(Row(trait.Name, get(trait.Var)));
                body.Append("</table></div><div><table>");
                foreach (var skill in Skills) body.Append(Row(skill.Name, get(skill.Var)));
                body.Append("</table></div></div>\n");
            }

            var houseDesc = nhood.GetHouseNameDesc(family.HouseNumber);
            body.Append("<h2>House</h2>\n<div class=\"house\">");
            if (houseThumb != null)
            {
                using (var file = File.Create(Path.Combine(dir, "house.png"))) houseThumb.SaveAsPng(file, houseThumb.Width, houseThumb.Height);
                body.Append("<img src=\"house.png\" alt=\"\">");
            }
            body.Append("<p>" + family.HouseNumber + " Sim Lane" + (string.IsNullOrEmpty(houseDesc?.Item1) ? "" : ": " + Html(houseDesc.Item1)) + "</p>");
            if (!string.IsNullOrEmpty(houseDesc?.Item2)) body.Append("<p>" + Html(houseDesc.Item2) + "</p>");
            var arch = vm.Context.Architecture;
            var floors = Enumerable.Range(0, arch.Stories).Count(l => arch.Floors[l].Any(x => x.Pattern != 0) || arch.Walls[l].Any(x => x.Segments != 0));
            var stats = HouseStats.Of(vm);
            body.Append("<p>Floors: " + floors + " &nbsp; Value: §" + family.ValueInArch + " &nbsp; Square feet: " + stats.SquareFeet
                + " &nbsp; Bedrooms: " + stats.Bedrooms + " &nbsp; Bathrooms: " + stats.Bathrooms + "</p>");
            body.Append("<table>" + Row("Size", stats.Size * 100) + Row("Furnishings", stats.Furnishings * 100) + Row("Yard", stats.Yard * 100)
                + Row("Upkeep", stats.Upkeep * 100) + Row("Layout", stats.Layout * 100) + "</table></div>\n");

            //the album's photos: Auto Snapshot's and the camera's
            var album = Path.Combine(nhood.UserPath, "PhotoAlbum", AlbumFolder(name));
            var photos = Directory.Exists(album) ? Directory.GetFiles(album).Where(x => x.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)).OrderBy(x => x).ToList() : new List<string>();
            body.Append("<h2>Photo Album</h2>\n");
            if (photos.Count == 0) body.Append("<p>No snapshots yet.</p>");
            else
            {
                body.Append("<div class=\"album\">");
                foreach (var photo in photos)
                {
                    var file = Path.GetFileName(photo);
                    var caption = Path.GetFileNameWithoutExtension(photo);
                    //photos are named with the time they were taken, then their caption, until they're renamed in the album
                    if (caption.Length >= 19 && DateTime.TryParseExact(caption.Substring(0, 19), "yyyy-MM-dd HH-mm-ss", null, System.Globalization.DateTimeStyles.None, out _))
                        caption = caption.Substring(19).Trim();
                    body.Append("<figure><img src=\"../../PhotoAlbum/" + Uri.EscapeDataString(AlbumFolder(name)) + "/" + Uri.EscapeDataString(file) + "\" alt=\"\"><figcaption>"
                        + Html(caption) + "</figcaption></figure>");
                }
                body.Append("</div>");
            }
            File.WriteAllText(Path.Combine(dir, "index.html"), Page("The " + name + " Family", body.ToString()));
        }

        /// <summary>
        /// The family's folder in UserData/PhotoAlbum, as SpecialEffects names it.
        /// </summary>
        private static string AlbumFolder(string familyName) => SpecialEffects.Clean(familyName);

        private static string Row(string name, int value)
        {
            var points = Math.Max(0, Math.Min(10, value / 100));
            return "<tr><td>" + name + "</td><td><span class=\"bar\"><span style=\"width:" + (points * 10) + "%\"></span></span> " + points + "</td></tr>";
        }

        /// <summary>
        /// A Sim's face, drawn from a copy of the Sim made outside the lot, as the Relationships panel does.
        /// </summary>
        private static bool SaveFace(VM vm, uint guid, string path)
        {
            if (Content.Get().WorldObjects.Get(guid) == null) return false;
            var obj = vm.Context.CreateObjectInstance(guid, LotTilePos.OUT_OF_WORLD, Direction.NORTH, true);
            if (obj == null) return false;
            try
            {
                var face = UIIconCache.GetObject(obj.BaseObject);
                if (face == null) return false;
                using (var file = File.Create(path)) face.SaveAsPng(file, face.Width, face.Height);
                return true;
            }
            finally
            {
                obj.Delete(vm.Context);
            }
        }
    }
}
