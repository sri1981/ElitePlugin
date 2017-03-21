using System;
using System.Collections.Generic;
using System.Linq;

namespace Elite.CRM.Plugins.ErrorHandling
{
    public static class ThrowIf
    {
        public static class Argument
        {
            /// <summary>
            /// Throws ArgumentNullException if argument == null.
            /// </summary>
            /// <param name="arg">Argument.</param>
            /// <param name="argName">Name of argument.</param>
            public static void IsNull(object arg, string argName)
            {
                if (arg == null)
                    throw new ArgumentNullException(argName);
            }

            /// <summary>
            /// Throws ArgumentException if argument is not valid (notValidCheck == true)
            /// </summary>
            /// <param name="notValidCheck">Result of validity check, true => exception, false => nothing</param>
            /// <param name="argName">Name of argument.</param>
            /// <param name="message">Message passed to exception</param>
            public static void IsNotValid(bool notValidCheck, string argName, string message)
            {
                if (notValidCheck)
                    throw new ArgumentException(message, argName);
            }

            /// <summary>
            /// Throws ArgumentException if collection argument is empty (!collection.Any())
            /// </summary>
            /// <typeparam name="T">Type of collection items.</typeparam>
            /// <param name="collection">Collection argument to check.</param>
            /// <param name="argName">Name of argument.</param>
            public static void IsEmpty<T>(IEnumerable<T> collection, string argName)
            {
                if (!collection.Any())
                    throw new ArgumentException(string.Format("'{0}' is empty", argName), argName);
            }

            public static void IsNullOrEmpty(string arg, string argName)
            {
                if (string.IsNullOrEmpty(arg))
                    throw new ArgumentNullException(argName);
            }
        }
    }
}
