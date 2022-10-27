﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading;

namespace Vaccination
{
    public class Patient
    {
        public string BirthNumber;
        public int Age;
        public string FirstName;
        public string LastName;
        public int VaccinationGroup;
        public int DosesGet;



        public Patient(string fname, string surname, string birthyear, int med, int risk, int infected)
        {
           FirstName = fname;
           LastName = surname;
           BirthNumber = FormatBirthdate(birthyear);
           Age = GetAge(BirthNumber);
           VaccinationGroup = GetVaccinationGroup(med, risk);
           DosesGet = 2 - infected; 
           
        }

        public int GetVaccinationGroup(int med, int risk)
        {

            int group = med == 1 ? 1 : Age >= 65 ? 2 : risk == 1 ? 3 : 4;

            return group;
        }

        public string FormatBirthdate(string birth)
        {
            
            string newFormat = birth;

            if (birth.Length <= 11)
            {
                if (int.Parse(birth[..2]) > 22)
                    newFormat = 19 + birth;
                else
                    newFormat = 20 + birth;
            }
            if (newFormat[8] != '-')
            {
                newFormat = newFormat[..8] + '-' + newFormat[8..];
            }

            return newFormat;

        }

        public int GetAge(string birthyear)
        {
            int birth = int.Parse(birthyear[..8]);
            int now = int.Parse(DateTime.Now.ToString("yyyyMMdd"));
            int age = (now - birth) / 10000;
            return age;
        }
    }

    public class Program
    {
        private static int doses = 20;
        private static string inputData = @"C:\Windows\Temp\People.csv";
        private static string outputPath = @"C:\Windows\Temp\Vaccinations.csv";

        public static void Main()
        {
            bool minors = false;

            while (true)
            {
                string minorsMenu = minors ? "Ja" : "Nej"; 

                Console.Clear();
                Console.WriteLine("Huvudmeny");
                Console.WriteLine("---------");
                Console.WriteLine($"Antal tillgängliga vaccindoser {doses}");
                Console.WriteLine($"Vaccinering under 18 år: {minorsMenu}");
                Console.WriteLine($"Indatafil: {inputData}");
                Console.WriteLine($"Utdatafil: {outputPath}");
                Console.WriteLine();

                int selected = ShowMenu("Vad vill du göra?", new[] { "Skapa prioritetsordning", "Ändra antal vaccindoser", "Ändra åldersgräns", "Ändra indatafil", "Ändra utdatafil", "Avsluta" });

                if (selected == 0)
                {
                    WriteCSV(CreateVaccinationOrder(File.ReadAllLines(inputData), doses, minors));
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
                    outputPath = ReadNewPath(false);
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
            int errorCount = 0;
            int dosesLeft = doses;
            List<Patient> sortedList = new List<Patient>();
            List<string> finalList = new List<string>();

            foreach (string person in input)
            {
                string[] fields = person.Split(',');

                if (fields.Any(x => x.Length == 0))
                {
                    errorCount++;
                }
                else
                {
                    try
                    {
                        Patient patient = new Patient(fields[2], fields[1], fields[0], int.Parse(fields[3]), int.Parse(fields[4]), int.Parse(fields[5]));
                        sortedList.Add(patient);
                    }
                    catch
                    {
                        errorCount++;
                    }
                }

            }

            if (errorCount > 0)
            {
                Console.Clear();
                Console.WriteLine($"Fel vid inläsning av CSV-fil på {errorCount} rader.");
                Program.Main();

            }
            
            sortedList = sortedList.OrderBy(x => x.VaccinationGroup).ThenBy(x => int.Parse(x.BirthNumber[..8])).ToList();

            if (!vaccinateChildren)
                sortedList = sortedList.Where(x => x.Age >= 18).ToList();

            foreach (Patient person in sortedList)
            {
                if (dosesLeft >= person.DosesGet)
                {
                    finalList.Add($"{person.BirthNumber},{person.LastName},{person.FirstName},{person.DosesGet}");
                    dosesLeft -= person.DosesGet;
                }
            }

            return finalList.ToArray();
            
        }

        public static void WriteCSV(string[] input)
        {
            Console.Clear();
            bool write = true;

            if (File.Exists(outputPath))
            {
                int selected = ShowMenu($"Filen {outputPath} finns redan, vill du ersätta innehållet i filen?", new[] {"Ja","Nej"});

                if (selected == 1)
                {
                    write = false;
                    Console.WriteLine("Filen har inte ändrats");
                    Thread.Sleep(1500);
                }

            }

            if (write)
            {
                try
                {
                    File.WriteAllLines(outputPath, input);
                    Console.WriteLine($"Resultatet har sparats i {outputPath}");
                    Thread.Sleep(1500);
                }
                catch
                {
                    Console.WriteLine("Det gick inte att skriva till filen");
                    Console.WriteLine("Ändra sökväg och försök igen!");
                    Thread.Sleep(1500);
                }
            }
        }

        public static string ReadNewPath(bool checkFile)
        {
            Console.Clear();
            Console.Write("Ange ny sökväg: ");

            string newPath = Console.ReadLine();
            bool validFile = File.Exists(@newPath);
            bool validDirectory = Directory.Exists(newPath);

            if(!validDirectory)
                validDirectory = Directory.Exists(Directory.GetParent(@newPath).ToString());
            
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
                if (validDirectory)
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