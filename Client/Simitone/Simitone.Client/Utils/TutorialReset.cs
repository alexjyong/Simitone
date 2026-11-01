using FSO.Client.UI.Controls;
using FSO.Client.UI.Framework;
using FSO.Client;
using FSO.Content;
using FSO.Files.Formats.IFF;
using FSO.Files.Formats.IFF.Chunks;
using Simitone.Client.UI.Panels.Desktop;
using Simitone.Client.UI.Screens;
using System;
using System.IO;
using System.Linq;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// TS1's Reset Tutorial option. The tutorial house and its family (the Newbies) are kept in UserData/Tutorial.FAM,
    /// a house file with the family's records added: resetting writes that house back over the tutorial house, and
    /// moves the Newbies back in with their starting money, evicting any family that moved in since. The house file and
    /// the neighbourhood are copied to UserData/Backups first.
    /// </summary>
    public static class TutorialReset
    {
        /// <summary>
        /// Asks first, with the game's explanation of what's lost.
        /// </summary>
        public static void Confirm(TS1GameScreen game)
        {
            string str(int i) => GameFacade.Strings.GetString("145", i.ToString());
            Show(str(71), (close) => UIAlertButton.YesNo((b) => { close(); Reset(game); }, (b) => close()));
        }

        /// <summary>
        /// A message in the classic dialog, with buttons made given the action that closes it.
        /// </summary>
        private static void Show(string message, Func<Action, UIAlertButton[]> buttons)
        {
            UIElement alert = null;
            void close()
            {
                if (alert is UIClassicAlert ca) ca.Close();
                else if (alert != null) UIScreen.RemoveDialog(alert);
            }
            var options = new UIAlertOptions { Title = GameFacade.Strings.GetString("145", "69"), Message = message, Buttons = buttons(close) };
            alert = ClassicUIGraphics.Available ? new UIClassicAlert(options, null, false) : (UIElement)new UIAlert(options);
            UIScreen.GlobalShowDialog(alert, true);
        }

        private static void Reset(TS1GameScreen game)
        {
            string message;
            try
            {
                message = ResetFiles(game);
            }
            catch (Exception e)
            {
                message = "The tutorial couldn't be reset: " + e.Message;
            }
            Show(message, (close) => UIAlertButton.Ok((b) => close()));
        }

        private static string ResetFiles(TS1GameScreen game)
        {
            var nhood = Content.Get().Neighborhood;
            var famPath = Directory.EnumerateFiles(nhood.UserPath).FirstOrDefault(x => Path.GetFileName(x).Equals("Tutorial.FAM", StringComparison.OrdinalIgnoreCase));
            if (famPath == null) return "This neighborhood has no tutorial (Tutorial.FAM).";
            var tutorial = new IffFile(famPath);
            var start = tutorial.List<FAMI>()?.FirstOrDefault();
            if (start == null || start.HouseNumber == 0) return "Tutorial.FAM has no family or house.";
            var house = (short)start.HouseNumber;
            //the lot that's open is saved over the reset when it's left
            if (game.vm?.GetGlobalValue(10) == house) return "Leave the tutorial house first, then reset it from another lot.";

            var backup = Path.Combine(nhood.UserPath, "Backups", "Tutorial " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"));
            Directory.CreateDirectory(backup);
            var housePath = nhood.GetHousePath(house);
            if (File.Exists(housePath)) File.Copy(housePath, Path.Combine(backup, Path.GetFileName(housePath)));
            File.Copy(Path.Combine(nhood.UserPath, "Neighborhood.iff"), Path.Combine(backup, "Neighborhood.iff"));

            File.WriteAllBytes(housePath, FamilyImport.HouseOf(File.ReadAllBytes(famPath)));

            //the Newbies, found by their Sims
            var family = nhood.MainResource.List<FAMI>()?.FirstOrDefault(x => x.FamilyGUIDs.OrderBy(g => g).SequenceEqual(start.FamilyGUIDs.OrderBy(g => g)));
            var occupant = nhood.GetFamilyForHouse(house);
            if (occupant != null && occupant != family) nhood.MoveOut(house);
            var result = "The tutorial house is reset.";
            if (family != null)
            {
                if (family.HouseNumber != 0 && family.HouseNumber != house) nhood.MoveOut((short)family.HouseNumber);
                nhood.SetFamilyForHouse(house, family, false);
                family.Budget = start.Budget;
                family.ValueInArch = start.ValueInArch;
            }
            else result += " The tutorial family is no longer in the neighborhood, so the house is empty.";
            if (occupant != null && occupant != family) result += " The family that lived there has moved out.";
            nhood.SaveNeighbourhood(false);
            return result + " The old files are in UserData/Backups.";
        }
    }
}
