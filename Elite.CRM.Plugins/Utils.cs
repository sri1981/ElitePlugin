using Elite.CRM.Plugins.ErrorHandling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins
{
    static class Utils
    {
        /// <summary>
        /// Converts excel column name (letters group such as 'A' or 'BD') into column number, starting by column 1.
        /// </summary>
        /// <param name="letters">Name of excel column (e.g. 'M').</param>
        /// <returns>Number of column, starting from 1.</returns>
        public static int LettersToNumber(string letters)
        {
            ThrowIf.Argument.IsNullOrEmpty(letters, "letters");
            ThrowIf.Argument.IsNotValid(letters.Any(l => !char.IsLetter(l)), "letters", "Column letters string can contain only letter characters.");

            int value = 0;
            var lettersUpper = letters.ToUpperInvariant();
            for (int i = 0; i < lettersUpper.Length; i++)
            {
                value = value * 26; // shift by one order of magnitude
                var l = lettersUpper[i] - 'A' + 1;
                value += l;
            }

            return value;
        }

        /// <summary>
        /// Normalizes postal code for searching, removes all non-alphanumeric characters from string and
        /// converts in to lowercase.
        /// </summary>
        /// <param name="postalCode"></param>
        /// <returns></returns>
        public static string NormalizePostalCode(string postalCode)
        {
            if (string.IsNullOrEmpty(postalCode))
                return null;

            return Regex.Replace(postalCode.ToLowerInvariant(), @"[^a-z0-9]+", "", RegexOptions.CultureInvariant);
        }
    }
}
