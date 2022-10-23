using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace Vaccination
{
    public class Program
    {
        private static int doses = 0;

        public static void Main()
        {
            bool minors = false;
            string inputData = @"C:\Windows\Temp\People.csv";
            string outputData = @"C:\Windows\Temp\Vaccinations.csv";

            while (true)
            {
                string minorsMenu = minors ? "Ja" : "Nej";


                Console.Clear();
                Console.WriteLine("Huvudmeny");
                Console.WriteLine("---------");
                Console.WriteLine($"Antal tillgängliga vaccindoser {doses}");
                Console.WriteLine($"Vaccinering under 18 år: {minorsMenu}");
                Console.WriteLine($"Indatafil: {inputData}");
                Console.WriteLine($"Utdatafil: {outputData}");
                Console.WriteLine();

                int selected = ShowMenu("Vad vill du göra?", new[] { "Skapa prioritetsordning", "Ändra antal vaccindoser", "Ändra åldersgräns", "Ändra indatafil", "Ändra utdatafil", "Avsluta" });

                if (selected == 0)
                {

                }
                else if (selected == 1)
                {
                    doses = ReadInt("Ange nytt antal doser: ");
                }
                else if (selected == 2)
                {
                    Console.Clear();
                    minors = ShowMenu("Ska personer under 18 vaccineras?", new[] { "Ja", "Nej" }) == 0;
                }
                else if (selected == 3)
                {
                    inputData = ReadNewPath(true);
                }
                else if (selected == 4)
                {
                    outputData = ReadNewPath(false);
                }
                else if (selected == 5)
                {
                    break;
                }
            }


        }

        // Create the lines that should be saved to a CSV file after creating the vaccination order.
        //
        // Parameters:
        //
        // input: the lines from a CSV file containing population information
        // doses: the number of vaccine doses available
        // vaccinateChildren: whether to vaccinate people younger than 18
        public static string[] CreateVaccinationOrder(string[] input, int doses, bool vaccinateChildren)
        {
            // Replace with your own code.
            return new string[0];
        }

        public static string ReadNewPath(bool checkFile)
        {
            Console.Clear();
            Console.Write("Ange ny sökväg: ");
            string newPath = Console.ReadLine();
            bool validFile = File.Exists(@newPath);
            bool validDirectory = Directory.Exists(@newPath);
            if (checkFile)
            {
                if (validFile)
                    return newPath;
                else
                {
                    Console.WriteLine("Ogiltig sökväg.");
                    Thread.Sleep(1500);
                    return ReadNewPath(checkFile);
                }
            }
            else 
            {
                if (validFile || validDirectory)
                    return newPath;
                else
                {
                    Console.WriteLine("Ogiltig sökväg.");
                    Thread.Sleep(1500);
                    return ReadNewPath(checkFile);
                }
            }

        }
        public static int ReadInt(string prompt)
        {
            Console.Clear();
            Console.Write(prompt);
            string input = Console.ReadLine();
            int number;
            bool isValid = int.TryParse(input, out number);
            if (isValid)
            {
                number = int.Parse(input);
                if (number >= 0)
                    return number;
                else
                {
                    Console.WriteLine("Kan inte vara mindre än 0!");
                    Thread.Sleep(1500);
                    return ReadInt(prompt);
                }
            }
            else
            {
                Console.WriteLine("Ogiltig inmatning");
                Thread.Sleep(1500);
                return ReadInt(prompt);
            }


        }

        public static int ShowMenu(string prompt, IEnumerable<string> options)
        {
            if (options == null || options.Count() == 0)
            {
                throw new ArgumentException("Cannot show a menu for an empty list of options.");
            }

            Console.WriteLine(prompt);

            // Hide the cursor that will blink after calling ReadKey.
            Console.CursorVisible = false;

            // Calculate the width of the widest option so we can make them all the same width later.
            int width = options.Max(option => option.Length);

            int selected = 0;
            int top = Console.CursorTop;
            for (int i = 0; i < options.Count(); i++)
            {
                // Start by highlighting the first option.
                if (i == 0)
                {
                    Console.BackgroundColor = ConsoleColor.Blue;
                    Console.ForegroundColor = ConsoleColor.White;
                }

                var option = options.ElementAt(i);
                // Pad every option to make them the same width, so the highlight is equally wide everywhere.
                Console.WriteLine("- " + option.PadRight(width));

                Console.ResetColor();
            }
            Console.CursorLeft = 0;
            Console.CursorTop = top - 1;

            ConsoleKey? key = null;
            while (key != ConsoleKey.Enter)
            {
                key = Console.ReadKey(intercept: true).Key;

                // First restore the previously selected option so it's not highlighted anymore.
                Console.CursorTop = top + selected;
                string oldOption = options.ElementAt(selected);
                Console.Write("- " + oldOption.PadRight(width));
                Console.CursorLeft = 0;
                Console.ResetColor();

                // Then find the new selected option.
                if (key == ConsoleKey.DownArrow)
                {
                    selected = Math.Min(selected + 1, options.Count() - 1);
                }
                else if (key == ConsoleKey.UpArrow)
                {
                    selected = Math.Max(selected - 1, 0);
                }

                // Finally highlight the new selected option.
                Console.CursorTop = top + selected;
                Console.BackgroundColor = ConsoleColor.Blue;
                Console.ForegroundColor = ConsoleColor.White;
                string newOption = options.ElementAt(selected);
                Console.Write("- " + newOption.PadRight(width));
                Console.CursorLeft = 0;
                // Place the cursor one step above the new selected option so that we can scroll and also see the option above.
                Console.CursorTop = top + selected - 1;
                Console.ResetColor();
            }

            // Afterwards, place the cursor below the menu so we can see whatever comes next.
            Console.CursorTop = top + options.Count();

            // Show the cursor again and return the selected option.
            Console.CursorVisible = true;
            return selected;
        }
    }

    [TestClass]
    public class ProgramTests
    {
        [TestMethod]
        public void ExampleTest()
        {
            // Arrange
            string[] input =
            {
                "19720906-1111,Elba,Idris,0,0,1",
                "8102032222,Efternamnsson,Eva,1,1,0"
            };
            int doses = 10;
            bool vaccinateChildren = false;

            // Act
            string[] output = Program.CreateVaccinationOrder(input, doses, vaccinateChildren);

            // Assert
            Assert.AreEqual(output.Length, 2);
            Assert.AreEqual("19810203-2222,Efternamnsson,Eva,2", output[0]);
            Assert.AreEqual("19720906-1111,Elba,Idris,1", output[1]);
        }
    }
}