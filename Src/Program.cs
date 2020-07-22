using System;
using System.Reflection;
using System.Text;
using RT.PostBuild;
using RT.PropellerApi;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace Timwi.Rank
{
    partial class Program
    {
#if DEBUG
        public const bool IsDebug = true;
#else
        public const bool IsDebug = false;
#endif

        static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; }
            catch { }

            if (args.Length == 2 && args[0] == "--post-build-check")
                return PostBuildChecker.RunPostBuildChecks(args[1], Assembly.GetExecutingAssembly());

            Console.BackgroundColor = IsDebug ? ConsoleColor.DarkBlue : ConsoleColor.DarkRed;
            Console.ForegroundColor = ConsoleColor.White;
            var msg = IsDebug ? "DEBUG MODE" : "RELEASE MODE";
            var spaces = new string(' ', (Console.BufferWidth - msg.Length - 7) / 2);
            Console.WriteLine("{0}┌──{1}──╖{0}".Fmt(spaces, new string('─', msg.Length)));
            Console.WriteLine("{0}│  {1}  ║{0}".Fmt(spaces, msg));
            Console.WriteLine("{0}╘══{1}══╝{0}".Fmt(spaces, new string('═', msg.Length)));
            Console.ResetColor();

            PropellerUtil.RunStandalone(PathUtil.AppPathCombine("Rank.Settings.json"), new RankModule());
            return 0;
        }
    }
}
