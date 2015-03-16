using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Diagnostics;
using System.IO;

namespace SocialTalents.WebPerf.LoadTest
{
    class Program
    {
        static int Main(string[] args)
        {
            // parse main parameters - processes, test duration, url
            int processes = 10;
            int duration = 10;
            string url = string.Empty;
            if (args.Length < 3)
                return PrintUsage();
            if (!int.TryParse(args[0], out processes))
                return PrintUsage(); 
            if (!int.TryParse(args[1], out duration))
                return PrintUsage();
            url = args[2];
            
            // parse options
            options = args.Skip(3).Select(arg => arg.ToLower()).ToDictionary(arg => arg, arg => arg);

            DateTime ExitTime = DateTime.Now.AddSeconds(duration);
            WorkerParam workerParam = new WorkerParam() { ExitTime = ExitTime, Url = url };
            
            if (options.ContainsKey(RandomKey))
            {
                Console.WriteLine("Randomizing by adding random number to end of url...");
                Console.WriteLine("Sample url:");
                Console.WriteLine(UrlToCall(workerParam));
            }
            
            Console.WriteLine(string.Format("Url: {0}\nThreads: {1}\nExitTime: {2}", url, processes, ExitTime));

            // running child processes to execute requests in parallel
            List<Process> children = new List<Process>();
            for (int i = 1; i < processes; i++)
            {
                string parameters = string.Format("{0} {1} {2} {3}", 1, duration, url, string.Join(" ", options.Keys));
                children.Add(System.Diagnostics.Process.Start("LoadTest.exe", parameters));
            }

            // do actual work
            ExecuteRequests(workerParam);
           
            int avarageRequestSize = (int)(totalContent / number);
            int totalNumber = number;

            // collect number of operations from child processes with exit codes
            foreach (var processInstance in children)
            {
                while (!processInstance.HasExited)
                    Thread.Sleep(100);
                totalNumber += processInstance.ExitCode;
            }

            Console.WriteLine("");
            Console.WriteLine(string.Format("{0} operations total, {1} operations per second {2} Kb Each, {3} Kbps; ~{4}% Errors ", 
                totalNumber, 
                totalNumber / duration, 
                avarageRequestSize / 1024,
                (totalNumber / duration) * avarageRequestSize * 8 / 1024,
                errors * 100 / number)); // errors are not collected from child processes yet, so this number is very rough

            if (lastError != null)
            {
                Console.WriteLine(string.Format("!!! At least {0} http errors registered. Last error was:", errors));
                Console.WriteLine(lastError);
            }
            return totalNumber;
        }

        public static int PrintUsage()
        {
            Console.WriteLine(string.Format("Usage: LoadTest.exe Parallelism Duration Url [options]{0}" +
                "Options: [{1}] randomize url adding random int to end {0}" + 
                "         [{2}] adds Accept-encoding: gzip (takes CPU but improve bandwidth usage)", 
                Environment.NewLine, RandomKey, GzipKey));
            return -1;
        }

        private static string lastError = null;

        public static void ExecuteRequests(WorkerParam context)
        {
            Stopwatch start = new Stopwatch();
            start.Start();
            long lastElapsed = 0;

            while (DateTime.Now < context.ExitTime)
            {
                if (start.ElapsedMilliseconds > lastElapsed + 100)
                {
                    lastElapsed = start.ElapsedMilliseconds;
                    PrintProgress();
                }

                var request = (HttpWebRequest)WebRequest.Create(UrlToCall(context));
                request.CookieContainer = new CookieContainer();
                request.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
                request.AllowAutoRedirect = true;
                request.Method = WebRequestMethods.Http.Get;
                request.KeepAlive = true;
                request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
                if (options.ContainsKey(GzipKey))
                {
                    request.Headers.Add("Accept-Encoding", "gzip,deflate");
                }

                HttpWebResponse response = null;
                try
                {
                    response = (HttpWebResponse)request.GetResponse();
                    if (response.StatusCode == HttpStatusCode.OK)
                        totalContent += response.ContentLength;
                    else
                    {
                        var objStream = response.GetResponseStream();
                        var objSR = new StreamReader(objStream, Encoding.UTF8, true);
                        lastError = objSR.ReadToEnd();
                        errors++;
                    }
                }
                catch (Exception ex)
                {
                    lastError = ex.Message;
                    errors++;
                }
                finally
                {
                    if (response != null)
                        response.Close();
                    number++;
                }
            }
        }

        static Random rnd = new Random();

        const string RandomKey = "-r";
        const string GzipKey = "-gzip";

        static Dictionary<string, string> options = new Dictionary<string, string>();

        private static int lastNumber = 0;
        private static char[] visualization = " 123456789ABCDEF*".ToCharArray();
        private static void PrintProgress()
        {
            int newNumber = number;
            int delta = newNumber - lastNumber;
            lastNumber = newNumber;

            if (delta >= visualization.Length)
                delta = visualization.Length - 1;

            Console.Write(visualization[delta]);
        }

        private static string UrlToCall(WorkerParam context)
        {
            return options.ContainsKey(RandomKey) ? context.Url + (10000 + rnd.Next(90000)).ToString() : context.Url;
        }

        public static string lockContext = "lock";
        public static int number = 0;
        public static int errors = 0;
        public static long totalContent = 0;
    }

    public class WorkerParam
    {
        public WorkerParam() { }

        public string Url { get; set; }
        public DateTime ExitTime { get; set; }
        
    }
}
