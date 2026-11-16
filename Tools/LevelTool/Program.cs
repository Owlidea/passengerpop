using System;
using System.Globalization;
using System.IO;
using MetroSort.Levels;

namespace MetroSort.Tools
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            string command = args.Length > 0 ? args[0] : "validate";
            if (command == "validate") return ValidateAll();
            if (command == "generate") return Generate(args);
            Console.Error.WriteLine("Usage: validate | generate <colors> <groupsPerColor> <queues> <platforms> <seed> [minDead] [maxDead]");
            return 2;
        }

        private static string FindLevels()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "Assets", "MetroSort", "Resources", "Levels");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }

        private static int ValidateAll()
        {
            string dir = FindLevels();
            if (dir == null) { Console.Error.WriteLine("Levels directory not found"); return 2; }
            string[] files = Directory.GetFiles(dir, "level_*.json");
            Array.Sort(files, string.CompareOrdinal);
            int failed = 0;
            foreach (string file in files)
            {
                LevelData level = LevelData.FromJson(File.ReadAllText(file));
                ValidationReport report = LevelValidator.Validate(level);
                if (report.IsValid && level.Par != report.Analysis.MinSolutionLength)
                    report.Errors.Add("par " + level.Par + " != solver minimum " + report.Analysis.MinSolutionLength);
                if (!report.IsValid) failed++;
                Console.WriteLine(report.ToString());
            }
            Console.WriteLine(files.Length + " levels checked, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }

        private static int Generate(string[] a)
        {
            if (a.Length < 6) { Console.Error.WriteLine("generate needs: colors groupsPerColor queues platforms seed"); return 2; }
            var p = new GeneratorParams
            {
                Colors = int.Parse(a[1], CultureInfo.InvariantCulture),
                GroupsPerColor = int.Parse(a[2], CultureInfo.InvariantCulture),
                Queues = int.Parse(a[3], CultureInfo.InvariantCulture),
                Platforms = int.Parse(a[4], CultureInfo.InvariantCulture)
            };
            int seed = int.Parse(a[5], CultureInfo.InvariantCulture);
            if (a.Length > 6) p.MinDeadEndRatio = float.Parse(a[6], CultureInfo.InvariantCulture);
            if (a.Length > 7) p.MaxDeadEndRatio = float.Parse(a[7], CultureInfo.InvariantCulture);
            ValidationReport report;
            LevelData level = LevelGenerator.Generate(p, seed, out report);
            if (level == null) { Console.Error.WriteLine("No level found inside the requested band"); return 1; }
            Console.WriteLine(level.ToJson());
            Console.Error.WriteLine(report.ToString());
            return 0;
        }
    }
}
