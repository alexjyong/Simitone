using FSO.Client;
using System;

namespace Simitone.Client.Utils
{
    /// <summary>
    /// A Sim's star sign. The game doesn't store it (person data 70 is 0 for every Sim): it shows the sign whose own
    /// personality is closest to the Sim's, and picking a sign in Create-a-Sim gives the Sim that personality. The
    /// names and descriptions are the game's (UIText tables 164 and 165).
    /// </summary>
    public static class ZodiacSign
    {
        //each sign's personality: neat, outgoing, active, playful, nice (25 points, as a Sim gets)
        public static readonly int[][] Personalities =
        {
            new[] { 5, 8, 6, 3, 3 }, //Aries
            new[] { 5, 5, 3, 8, 4 }, //Taurus
            new[] { 4, 7, 8, 3, 3 }, //Gemini
            new[] { 6, 3, 6, 4, 6 }, //Cancer
            new[] { 4, 10, 4, 4, 3 }, //Leo
            new[] { 9, 2, 6, 3, 5 }, //Virgo
            new[] { 2, 8, 2, 6, 7 }, //Libra
            new[] { 6, 5, 8, 3, 3 }, //Scorpio
            new[] { 2, 3, 9, 7, 4 }, //Sagittarius
            new[] { 7, 4, 1, 8, 5 }, //Capricorn
            new[] { 4, 4, 4, 7, 6 }, //Aquarius
            new[] { 5, 3, 7, 3, 7 }, //Pisces
        };

        /// <summary>
        /// The closest sign (0 Aries to 11 Pisces) to a personality given as neat, outgoing, active, playful, nice. Ties go
        /// to the earlier sign, which makes an even personality (all 0 or all 5) a Cancer, as in the game.
        /// </summary>
        public static int Of(int neat, int outgoing, int active, int playful, int nice)
        {
            var points = new[] { neat, outgoing, active, playful, nice };
            int best = 0, bestDistance = int.MaxValue;
            for (int i = 0; i < Personalities.Length; i++)
            {
                var distance = 0;
                for (int j = 0; j < 5; j++) distance += (points[j] - Personalities[i][j]) * (points[j] - Personalities[i][j]);
                if (distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }
            return best;
        }

        public static string Name(int sign) => GameFacade.Strings.GetString("164", sign.ToString());

        /// <summary>
        /// The game's "About Aries" popup: its title and text.
        /// </summary>
        public static (string Title, string Text) About(int sign) =>
            (GameFacade.Strings.GetString("165", (sign * 2).ToString()), GameFacade.Strings.GetString("165", (sign * 2 + 1).ToString()));

        /// <summary>
        /// The popup as a tooltip: its title, then its text.
        /// </summary>
        public static string Tooltip(int sign)
        {
            var (title, text) = About(sign);
            return title + "\n" + text;
        }
    }
}
