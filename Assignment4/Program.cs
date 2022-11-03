using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;
using System.Text;
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
        private static string iscPath = @"C:\Windows\Temp\Schedule.ics";
        private static bool minors = false;

        public static void Main()
        {

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

                int selected = ShowMenu("Vad vill du göra?", new[] { "Skapa prioritetsordning", "Schemalägg vaccinationer", "Ändra antal vaccindoser", "Ändra åldersgräns", "Ändra indatafil", "Ändra utdatafil", "Avsluta" });

                try
                {
                    if (selected == 0)
                    {
                        WriteCSV(CreateVaccinationOrder(File.ReadAllLines(inputData), doses, minors));
                    }
                    else if (selected == 1)
                    {
                        WriteISC(CreateVaccinationCalendar());
                    }
                    else if (selected == 2)
                    {
                        Console.Clear();
                        Console.WriteLine("Ange nytt antal doser: ");
                        doses = ReadInt();
                    }
                    else if (selected == 3)
                    {
                        Console.Clear();
                        minors = ShowMenu("Ska personer under 18 vaccineras?", new[] { "Ja", "Nej" }) == 0;
                    }
                    else if (selected == 4)
                    {
                        inputData = ReadNewPath(true);
                    }
                    else if (selected == 5)
                    {
                        outputPath = ReadNewPath(false);
                    }
                    else if (selected == 6)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Thread.Sleep(1500);
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

        public static string CreateVaccinationCalendar()
        {
            //Variables for CreateISC
            DateTime startDate = new DateTime();
            string startTime = "08:00";
            string endTime = "20:00";
            int simultaneous = 2;
            int minutesPerVisit = 5;


            Console.Clear();
            Console.WriteLine("Schemalägg vaccinationer");
            Console.WriteLine("---------");
            Console.WriteLine("Mata in blankrad för att välja standardvärde");
            Console.WriteLine();
            startDate = GetDate();

            string startTimeInput = GetTime("Startid: ");
            if (startTimeInput != "")
                startTime = startTimeInput;

            string endTimeInput = GetTime("Sluttid: ");
            if (endTimeInput != "")
                endTime = endTimeInput;

            Console.Write("Antal samtidiga vaccinationer: ");

            string simultaneousInput = Console.ReadLine();
            if (simultaneousInput != "")
                simultaneous = ReadInt(simultaneousInput);


            Console.Write("Minuter per vaccination: ");
            string minutesInput = Console.ReadLine();
            if (minutesInput != "")
                minutesPerVisit = ReadInt(minutesInput);

            string newPathInput = ReadICSPath("Kalenderfil: ");
            if (newPathInput != "")
                iscPath = newPathInput;

            if (TimeSpan.Parse(startTime) >= TimeSpan.Parse(endTime))
                throw new ArgumentException("Sluttiden måste vara senare än starttiden.");

            try
            {
                return CreateISCtext(OrderPatients(File.ReadAllLines(inputData), minors, doses), startDate, startTime, endTime, simultaneous, minutesPerVisit);
            }
            catch
            {
                throw new ArgumentException("Fel inträffade vid skapandet av ISC-filen.");
            }

        }

        public static string CreateISCtext(List<Patient> input, DateTime startDate, string startTime, string endTime, int simultaneous, int minutesPerVisit)
        {
            StringBuilder sb = new StringBuilder();
            var sortedList = input;

            //Keep track of number of patients in the same timeblock
            int sameTimeAppointment = 0;

            //New DateTime variables with correct start and end times
            var newStart = new DateTime(startDate.Year, startDate.Month, startDate.Day, int.Parse(startTime[..startTime.IndexOf(':')]),
                int.Parse(startTime[(startTime.IndexOf(':') + 1)..]), 0);
            var newEnd = new DateTime(startDate.Year, startDate.Month, startDate.Day, int.Parse(endTime[..endTime.IndexOf(':')]),
                int.Parse(endTime[(endTime.IndexOf(':') + 1)..]), 0);
            var timeOfVisit = newStart;

            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:1.0");
            sb.AppendLine("PRODID:Folkhälsomyndigheten");
            sb.AppendLine("CALSCALE:GREGORIAN");
            sb.AppendLine("METHOD:PUBLISH");

            foreach (Patient p in sortedList)
            {
                sameTimeAppointment++;

                //If the max number of patients during this time is full, move to next time instead
                if (sameTimeAppointment > simultaneous)
                {
                    timeOfVisit = timeOfVisit.AddMinutes(minutesPerVisit);
                    sameTimeAppointment = 1;
                }

                //If the visit will surpass the end time, move to the next day instead
                if (timeOfVisit.AddMinutes(minutesPerVisit) > newEnd)
                {
                    newStart = newStart.AddDays(1);
                    newEnd = newEnd.AddDays(1);
                    timeOfVisit = newStart;
                }

                if (!(timeOfVisit.AddMinutes(minutesPerVisit) > newEnd))
                {
                    sb.AppendLine("BEGIN:VEVENT");
                    sb.AppendLine("DTSTAMP:" + timeOfVisit.ToString("yyyyMMddTHHmm00"));
                    sb.AppendLine("UID:" + p.BirthNumber + "Z-123401@example.com");
                    sb.AppendLine("DTSTART:" + timeOfVisit.ToString("yyyyMMddTHHmm00"));
                    sb.AppendLine("DTEND:" + timeOfVisit.AddMinutes(minutesPerVisit).ToString("yyyyMMddTHHmm00"));

                    sb.AppendLine($"SUMMARY: {p.FirstName} {p.LastName}");
                    sb.AppendLine("LOCATION:" + "Göteborg" + "");
                    sb.AppendLine("DESCRIPTION:" + " Första dosen");
                    sb.AppendLine("PRIORITY:3");
                    sb.AppendLine("END:VEVENT");
                }
            }

            sb.AppendLine("END:VCALENDAR");

            return sb.ToString();
        }

        public static List<Patient> OrderPatients(string[] input, bool vaccinateChildren, int doses)
        {
            int dosesLeft = doses;
            int errorCount = 0;
            List<Patient> sortedList = new List<Patient>(); 
            List<Patient> getsVaccinated = new List<Patient>();

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

            if (!vaccinateChildren)
                sortedList = sortedList.Where(x => x.Age >= 18).ToList();

            sortedList = sortedList.OrderBy(x => x.VaccinationGroup).ThenBy(x => int.Parse(x.BirthNumber[..8])).ToList();

            foreach (Patient patient in sortedList)
            {
                if (dosesLeft >= patient.DosesGet)
                {
                    getsVaccinated.Add(patient);
                    dosesLeft -= patient.DosesGet;
                }
                else
                    break;
            }

            if (errorCount > 0)
            {
                throw new ArgumentException($"Fel vid inläsning av CSV-fil på {errorCount} rader.");
            }

            return getsVaccinated;
        }

        public static string[] CreateVaccinationOrder(string[] input, int doses, bool vaccinateChildren)
        {
            var orderedList = OrderPatients(input, vaccinateChildren, doses);
            List<string> finalList = new List<string>();

            foreach (Patient person in orderedList)
            {
                    finalList.Add($"{person.BirthNumber},{person.LastName},{person.FirstName},{person.DosesGet}");
            }

            return finalList.ToArray();

        }


        //Methods for writing content to files
        public static void WriteISC(string text)
        {
            bool write = true;

            // If a file already exists, ask user if they want to overwrite it

            if (File.Exists(iscPath))
            {
                int selected = ShowMenu($"Filen {iscPath} finns redan, vill du ersätta innehållet i filen?", new[] { "Ja", "Nej" });

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
                    File.WriteAllText(iscPath, text);
                    Console.WriteLine($"Resultatet har sparats i {iscPath}");
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
        public static void WriteCSV(string[] input)
        {
            Console.Clear();
            bool write = true;

            if (File.Exists(outputPath))
            {
                int selected = ShowMenu($"Filen {outputPath} finns redan, vill du ersätta innehållet i filen?", new[] { "Ja", "Nej" });

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


        //Below are methods for reading user input, along with error-handling
        public static DateTime GetDate()
        {
            Console.Write("Startdatum (YYYY-MM-DD): ");
            string input = Console.ReadLine();

            DateTime chosenDate;

            if (input != "")
            {
                try
                {
                    chosenDate = DateTime.Parse(input);
                }
                catch
                {
                    Console.WriteLine("Felaktigt format - Prova igen.");
                    Thread.Sleep(1500);
                    chosenDate = GetDate();
                }

            }
            else
            {
                chosenDate = DateTime.Now.AddDays(7);

            }

            return chosenDate;
        }

        public static string GetTime(string prompt)
        {
            Console.Write(prompt);
            string input = Console.ReadLine();
            TimeSpan time;
            if (input != "")
            {
                if (TimeSpan.TryParse(input, out time))
                    time = TimeSpan.Parse(input);
                else
                {
                    Console.WriteLine("Felaktigt format");
                    time = TimeSpan.Parse(GetTime(prompt));
                }

                return time.ToString()[..5];
            }
            else
                return input;

        }

        public static string ReadNewPath(bool checkFile)
        {
            Console.Clear();
            Console.Write("Ange ny sökväg: ");

            string newPath = Console.ReadLine();
            bool validFile = File.Exists(@newPath);
            bool isDirectory = Directory.Exists(newPath);
            bool insideValidDir = Directory.Exists(Directory.GetParent(@newPath).ToString());

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
                if (!isDirectory && insideValidDir)
                    return newPath;
                else
                {
                    Console.WriteLine("Ogiltig sökväg.");
                    Thread.Sleep(1500);
                    return ReadNewPath(checkFile);
                }
            }

        }

        public static string ReadICSPath(string prompt = "")
        {
            if (prompt != "")
                Console.Write(prompt);
            else
                Console.WriteLine("Ange ny sökväg: ");

            string newPath = Console.ReadLine();


            if (newPath != "")
            {
                bool isDirectory = Directory.Exists(newPath);
                bool insideValidDir = Directory.Exists(Directory.GetParent(@newPath).ToString());
                if (!isDirectory && insideValidDir && newPath.EndsWith(".ics"))
                    return newPath;
                else
                {
                    Console.WriteLine("Ogiltig sökväg.");
                    Thread.Sleep(1500);
                    return ReadICSPath();
                }
            }
            else
                return "";

        }
        public static int ReadInt()
        {
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
                    Console.WriteLine("Kan inte vara mindre än 0 - Ange ny inmatning:");
                    Thread.Sleep(1500);
                    return ReadInt();
                }
            }
            else
            {
                Console.WriteLine("Ogiltig inmatning - Ange ny inmatning:");
                Thread.Sleep(1500);
                return ReadInt();
            }


        }
        public static int ReadInt(string input)
        {
            int number;
            bool isValid = int.TryParse(input, out number);
            if (isValid)
            {
                number = int.Parse(input);
                if (number >= 0)
                    return number;
                else
                {
                    Console.WriteLine("Kan inte vara mindre än 0 - Ange ny inmatning:");
                    Thread.Sleep(1500);
                    return ReadInt();
                }
            }
            else
            {
                Console.WriteLine("Ogiltig inmatning - Ange ny inmatning:");
                Thread.Sleep(1500);
                return ReadInt();
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

    //
    //
    // ***TESTS BELOW***

    [TestClass]
    public class ProgramTests
    {
        // Tests for CreateISCtext
        [TestMethod]
        public void ThirdWheelVaxxer()
        {

            string[] input =
            {
                "19720906-1111,Elba,Idris,0,1,1",
                "8102032222,Jolie,Angelina,1,1,0",
                "820723-2132,Pitt,Brad,0,0,0"
            };
            DateTime dateTime = DateTime.Parse("2022-10-12");
            string startTime = "10:00";
            string endTime = "12:00";
            int sameTimePatients = 2;
            int minutes = 60;
            int doses = 20;

            // Act
            string[] output = Program.CreateISCtext(Program.OrderPatients(input, true, doses), dateTime, startTime, endTime, sameTimePatients, minutes)
                .Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Assert
            //Only 2 patients in 1st timeblock
            Assert.AreEqual("DTSTART:20221012T100000", output[8]);
            Assert.AreEqual("DTEND:20221012T110000", output[9]);
            Assert.AreEqual(output[9], output[19]);
            Assert.AreEqual(output[8], output[18]);
            Assert.AreEqual("UID:19820723-2132Z-123401@example.com", output[27]);
            Assert.AreEqual("DTSTART:20221012T110000", output[28]);
            Assert.AreEqual("DTEND:20221012T120000", output[29]);
        }

        [TestMethod]
        public void SameTimeVax()
        {

            string[] input =
            {
                "19720906-1111,Elba,Idris,0,0,1",
                "8102032222,Efternamnsson,Eva,1,1,0"
            };
            DateTime dateTime = DateTime.Parse("2022-10-12");
            string startTime = "10:00";
            string endTime = "12:00";
            int sameTimePatients = 2;
            int minutes = 60;
            int doses = 20;

            // Act
            string[] output = Program.CreateISCtext(Program.OrderPatients(input, true, doses), dateTime, startTime, endTime, sameTimePatients, minutes)
                .Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Assert
            //Both appointments should fit in 1 day
            Assert.AreEqual("DTSTART:20221012T100000", output[8]);
            Assert.AreEqual("DTEND:20221012T110000", output[9]);
            Assert.AreEqual(output[9], output[19]);
            Assert.AreEqual(output[8], output[18]);

        }

        [TestMethod]
        public void SameDayVax()
        {

            string[] input =
            {
                "19720906-1111,Elba,Idris,0,0,1",
                "8102032222,Efternamnsson,Eva,1,1,0"
            };
            DateTime dateTime = DateTime.Parse("2022-10-12");
            string startTime = "10:00";
            string endTime = "11:00";
            int sameTimePatients = 1;
            int minutes = 30;
            int doses = 20;

            // Act
            string[] output = Program.CreateISCtext(Program.OrderPatients(input, true, doses), dateTime, startTime, endTime, sameTimePatients, minutes)
                .Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Assert
            //Both appointments should fit in 1 day
            Assert.AreEqual("DTEND:20221012T103000", output[9]);
            Assert.AreEqual("DTEND:20221012T110000", output[19]);
        }

        [TestMethod]
        public void OneMinOverTime()
        {

            string[] input =
            {
                "19720906-1111,Elba,Idris,0,0,1",
                "8102032222,Efternamnsson,Eva,1,1,0"
            };
            DateTime dateTime = DateTime.Parse("2022-10-12");
            string startTime = "10:00";
            string endTime = "11:00";
            int sameTimePatients = 1;
            int minutes = 31;
            int doses = 20;

            // Act
            string[] output = Program.CreateISCtext(Program.OrderPatients(input, true, doses), dateTime, startTime, endTime, sameTimePatients, minutes)
                .Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Assert
            //No time for both in 1 day
            Assert.AreEqual("DTSTART:20221012T100000", output[8]);
            Assert.AreEqual("DTEND:20221012T103100", output[9]);
            Assert.AreEqual("DTSTART:20221013T100000", output[18]);
            Assert.AreEqual("DTEND:20221013T103100", output[19]);
        }


        [TestMethod]
        public void NoTimeForAppointment()
        {

            string[] input =
            {
                "19720906-1111,Elba,Idris,0,0,1",
                "8102032222,Efternamnsson,Eva,1,1,0"
            };
            DateTime dateTime = DateTime.Parse("2022-10-12");
            string startTime = "10:00";
            string endTime = "11:00";
            int sameTimePatients = 2;
            int minutes = 61;
            int doses = 20;
            // Act
            //Produces invalid ISC file with no events
            string[] output = Program.CreateISCtext(Program.OrderPatients(input, true, doses), dateTime, startTime, endTime, sameTimePatients, minutes)
                .Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Assert
            Assert.AreEqual("BEGIN:VCALENDAR", output[0]);
            Assert.AreEqual(false, "BEGIN:VEVENT" == output[5]);
            Assert.AreEqual(false, output.Contains("BEGIN:VEVENT")); 
        }

        [TestMethod]
        public void EmptyInputList()
        {

            string[] input =
            {
            };
            DateTime dateTime = DateTime.Parse("2022-10-12");
            string startTime = "10:00";
            string endTime = "11:00";
            int sameTimePatients = 1;
            int minutes = 30;
            int doses = 20;

            // Act
            string[] output = Program.CreateISCtext(Program.OrderPatients(input, true, doses), dateTime, startTime, endTime, sameTimePatients, minutes)
                .Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Assert
            Assert.AreEqual("BEGIN:VCALENDAR", output[0]);
            Assert.AreEqual(false, "BEGIN:VEVENT" == output[5]);
            Assert.AreEqual(false, output.Contains("BEGIN:VEVENT"));
        }

        [TestMethod]
        public void FuturisticSpecialVax()
        {

            string[] input =
            {
                "19720906-1111,Elba,Idris,0,0,1",
                "8102032222,Efternamnsson,Eva,1,1,0"
            };
            DateTime dateTime = DateTime.Parse("2032-01-01");
            string startTime = "00:00";
            string endTime = "12:00";
            int sameTimePatients = 1;
            int minutes = 360;
            int doses = 3;

            // Act
            string[] output = Program.CreateISCtext(Program.OrderPatients(input, true, doses), dateTime, startTime, endTime, sameTimePatients, minutes)
                .Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Assert
            Assert.AreEqual("UID:19810203-2222Z-123401@example.com", output[7]);
            Assert.AreEqual("DTSTART:20320101T000000", output[8]);
            Assert.AreEqual("DTEND:20320101T060000", output[9]);
            Assert.AreEqual("UID:19720906-1111Z-123401@example.com", output[17]);
            Assert.AreEqual("DTSTART:20320101T060000", output[18]);
            Assert.AreEqual("DTEND:20320101T120000", output[19]);
        }


        // Tests for CreateVaccinationOrder
        [TestMethod]
        public void StandardOrderTest()
        {
            // Arrange
            string[] input =
            {
                "19720906-1111,Elba,Idris,0,0,0",
                "8102032222,Efternamnsson,Eva,1,1,0"
            };
            int doses = 10;
            bool vaccinateChildren = false;

            // Act
            string[] output = Program.CreateVaccinationOrder(input, doses, vaccinateChildren);

            // Assert
            Assert.AreEqual(output.Length, 2);
            Assert.AreEqual("19810203-2222,Efternamnsson,Eva,2", output[0]);
            Assert.AreEqual("19720906-1111,Elba,Idris,2", output[1]);
        }

        [TestMethod]
        public void VaxMinorTest()
        {
            // Arrange
            string[] input =
            {
                "20050906-1111,Thunborg,Rickard,0,1,0",
                "9402032222,Efternamnsson,Alex,1,0,0"
            };
            int doses = 10;
            bool vaccinateChildren = true;

            // Act
            string[] output = Program.CreateVaccinationOrder(input, doses, vaccinateChildren);

            // Assert
            Assert.AreEqual(output.Length, 2);
            Assert.AreEqual("19940203-2222,Efternamnsson,Alex,2", output[0]);
            Assert.AreEqual("20050906-1111,Thunborg,Rickard,2", output[1]);
        }

        [TestMethod]
        public void NoMinorTest()
        {
            // Arrange
            string[] input =
            {
                "20050906-1111,Thunborg,Rickard,0,1,0",
                "9402032222,Efternamnsson,Alex,1,0,0"
            };
            int doses = 10;
            bool vaccinateChildren = false;

            // Act
            string[] output = Program.CreateVaccinationOrder(input, doses, vaccinateChildren);

            // Assert
            Assert.AreEqual(output.Length, 1);
            Assert.AreEqual("19940203-2222,Efternamnsson,Alex,2", output[0]);
        }
        [TestMethod]
        public void OneDose()
        {
            // Arrange
            string[] input =
            {
                "19720906-1111,Elba,Idris,0,0,1",
                "8102032222,Efternamnsson,Eva,1,1,1"
            };
            int doses = 10;
            bool vaccinateChildren = false;

            // Act
            string[] output = Program.CreateVaccinationOrder(input, doses, vaccinateChildren);

            // Assert
            Assert.AreEqual(output.Length, 2);
            Assert.AreEqual("19810203-2222,Efternamnsson,Eva,1", output[0]);
            Assert.AreEqual("19720906-1111,Elba,Idris,1", output[1]);
        }

        [TestMethod]
        public void TooFewDoses()
        {
            // Arrange
            string[] input =
            {
                "19720906-1111,Elba,Idris,0,0,1",
                "8102032222,Efternamnsson,Eva,1,1,1"
            };
            int doses = 1;
            bool vaccinateChildren = false;

            // Act
            string[] output = Program.CreateVaccinationOrder(input, doses, vaccinateChildren);

            // Assert
            Assert.AreEqual(output.Length, 1);
            Assert.AreEqual("19810203-2222,Efternamnsson,Eva,1", output[0]);
        }

        [TestMethod]
        public void SortsByGroup()
        {
            // Arrange
            string[] input =
            {
                "19720906-1111,Elba,Idris,0,0,1",
                "8102032222,Efternamnsson,Eva,1,0,0",
                "500906-1111,Alex,Gru,0,0,1",
                "8103032222,Rickard,Evasson,0,1,1"
            };
            int doses = 5;
            bool vaccinateChildren = false;

            // Act
            string[] output = Program.CreateVaccinationOrder(input, doses, vaccinateChildren);

            // Assert
            Assert.AreEqual(output.Length, 4);
            Assert.AreEqual("19810203-2222,Efternamnsson,Eva,2", output[0]);
            Assert.AreEqual("19500906-1111,Alex,Gru,1", output[1]);
            Assert.AreEqual("19810303-2222,Rickard,Evasson,1", output[2]);
            Assert.AreEqual("19720906-1111,Elba,Idris,1", output[3]);
        }
        [TestMethod]
        public void SortsByGroupThenAge()
        {
            // Arrange
            string[] input =
            {
                "19720906-1111,Elba,Idris,1,0,1",
                "500906-1111,Alex,Gru,0,0,1",
                "8102032222,Efternamnsson,Eva,1,0,0",
                "5009052222,Rickard,Evasson,0,1,1"
            };
            int doses = 5;
            bool vaccinateChildren = false;

            // Act
            string[] output = Program.CreateVaccinationOrder(input, doses, vaccinateChildren);

            // Assert
            Assert.AreEqual(output.Length, 4);
            Assert.AreEqual("19720906-1111,Elba,Idris,1", output[0]);
            Assert.AreEqual("19810203-2222,Efternamnsson,Eva,2", output[1]);
            Assert.AreEqual("19500905-2222,Rickard,Evasson,1", output[2]);
            Assert.AreEqual("19500906-1111,Alex,Gru,1", output[3]);
        }

        [TestMethod]
        public void EnoughDosesButTooFewForFirst()
        {
            // Arrange
            string[] input =
            {
                "19720906-1111,Elba,Idris,0,0,1",
                "8102032222,Efternamnsson,Eva,1,1,0"
            };
            int doses = 1;
            bool vaccinateChildren = false;

            // Act
            string[] output = Program.CreateVaccinationOrder(input, doses, vaccinateChildren);

            // Assert
            Assert.AreEqual(output.Length, 0);
        }

    }
}
