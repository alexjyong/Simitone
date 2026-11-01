using FSO.Client;
using FSO.Content;
using FSO.Files.Formats.IFF.Chunks;
using System;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// What the neighbourhood says about a house, from the game's rollover strings (UIText table 134): who lives there,
    /// or for an empty lot whether it's a community lot or what it costs, and whether the family moving in can afford it.
    /// </summary>
    public static class HouseRollover
    {
        private static string Money(int amount) => "§" + amount.ToString("##,#0");

        private static string Str(int i, params string[] args) => GameFacade.Strings.GetString("134", i.ToString(), args);

        /// <summary>
        /// The house's street address, such as "75 Sim Lane", or "" if it has none.
        /// </summary>
        public static string Address(int houseID)
        {
            var streets = Content.Get().Neighborhood.StreetNames;
            if (!int.TryParse(streets?.Get<STR>(2001)?.GetString(houseID - 1), out var street)) return "";
            return streets.Get<STR>(2000)?.GetString(street - 1)?.Replace("%s", houseID.ToString()) ?? "";
        }

        public static string FamilyName(FAMI family) =>
            Content.Get().Neighborhood.MainResource.Get<FAMs>(family.ChunkID)?.GetString(0) ?? "?";

        /// <summary>
        /// The family living there: "The Goth Family", their net worth and friends.
        /// </summary>
        public static string Family(FAMI family)
        {
            return Str(0, FamilyName(family), Money(family.ValueInArch + family.Budget), family.FamilyFriends.ToString());
        }

        /// <summary>
        /// If the house is a community lot. Residential lots are listed in the neighbourhood's zoning; Studio Town's
        /// unlisted lots are its own kind (still not residential).
        /// </summary>
        public static bool IsCommunity(int houseID)
        {
            if (!Content.Get().Neighborhood.ZoningDictionary.TryGetValue((short)houseID, out var zone))
                zone = (short)((houseID >= 81 && houseID <= 89) ? 2 : 1);
            return zone > 0;
        }

        /// <summary>
        /// The lines after a house's name and description: who lives there, that it's a community lot, or its cost and
        /// whether the moving family (if any) can afford it. Valid is whether the house can be entered, or moved into.
        /// </summary>
        public static string Status(int houseID, FAMI moveIn, out bool valid)
        {
            var neigh = Content.Get().Neighborhood;
            valid = true;
            var family = neigh.GetFamilyForHouse((short)houseID);
            if (family != null)
            {
                if (moveIn == null) return Family(family);
                valid = false;
                return GameFacade.Strings.GetString("132", "15"); //someone lives there
            }

            if (IsCommunity(houseID))
            {
                valid = moveIn == null;
                return Str(valid ? 17 : 18, "", "").Substring(4); //a community lot (which can't be moved into)
            }

            //the strings start with the name and description ("%s\r\n%s\r\n"), which the caller shows itself
            var price = neigh.GetHouse(houseID).Get<SIMI>(1)?.PurchaseValue ?? 0;
            if (houseID >= 90 && houseID <= 92)
            {
                //Magic Town's houses also cost MagiCoins
                var required = Str(32 + Math.Abs(91 - houseID));
                if (moveIn == null) return Str(29, "", "", Money(price), required).Substring(4);
                var magicoins = neigh.GetMagicoinsForFamily(moveIn);
                var name = FamilyName(moveIn);
                if (magicoins >= int.Parse(required))
                {
                    if (moveIn.Budget >= price) return Str(31, "", "", Money(price), required).Substring(4);
                    valid = false;
                    return Str(38, "", "", Money(price), required, name, Money(moveIn.Budget)).Substring(4);
                }
                valid = false;
                return Str((moveIn.Budget >= price) ? 36 : 37, "", "", Money(price), required, name, magicoins.ToString()).Substring(4);
            }

            if (moveIn == null) return Str(1, "", "", Money(price)).Substring(4);
            if (moveIn.Budget >= price) return Str(5, "", "", Money(price), Money(moveIn.Budget - price)).Substring(4);
            valid = false;
            return Str(4, "", "", Money(price), FamilyName(moveIn), Money(moveIn.Budget)).Substring(4);
        }
    }
}
