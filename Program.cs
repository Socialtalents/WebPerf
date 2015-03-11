﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Diagnostics;

namespace LoadTest
{
    class Program
    {
        static Random rnd = new Random();

        const string RandomKey = "-r";
        const string GzipKey = "-gzip";

        static Dictionary<string, string> options = new Dictionary<string, string>();

        static int Main(string[] args)
        {
            // parse main parameters - processes, test duration, url
            int processes = 10;
            int duration = 10;
            string url = string.Empty;
            if (args.Length < 3)
                return Usage();
            if (!int.TryParse(args[0], out processes))
                return Usage(); 
            if (!int.TryParse(args[1], out duration))
                return Usage();
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

            List<Process> children = new List<Process>();
            for (int i = 1; i < processes; i++)
            {
                string parameters = string.Format("{0} {1} {2} {3}", 1, duration, url, string.Join(" ", options.Keys));
                children.Add(System.Diagnostics.Process.Start("LoadTest.exe", parameters));
            }

            Worker(workerParam);

           
            int avarageRequest = (int)(totalContent / number);

            int totalNumber = number;
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
                avarageRequest / 1024,
                (totalNumber / duration) * avarageRequest * 8 / 1024,
                errors * 100 / number));
            return totalNumber;
        }

        public static int Usage()
        {
            Console.WriteLine(string.Format("Usage: LoadTest.exe Number Duration url [options]{0}" +
                "Options: [-r] randomize url adding random int to end {0}" + 
                "         [-gzip] adds Accept-encoding: gzip (takes CPU but improve bandwidth usage)", 
                Environment.NewLine));
            return -1;
        }

        public static void Worker(object parameter)
        {
            WorkerParam context = (WorkerParam)parameter;
            Stopwatch start = new Stopwatch();
            start.Start();
            long lastElapsed = 0;

            while (DateTime.Now < context.ExitTime)
            {
                if (start.ElapsedMilliseconds > lastElapsed + 100)
                {
                    lastElapsed = start.ElapsedMilliseconds;
                    printProgress();
                }

                var request = (HttpWebRequest)WebRequest.Create(UrlToCall(context));
                request.Method = WebRequestMethods.Http.Get;
                request.KeepAlive = true;
                request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
                if (options.ContainsKey(GzipKey))
                {
                    request.Headers.Add("Accept-Encoding", "gzip,deflate");
                }
    
                try
                {
                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    {
                        if (response.StatusCode == HttpStatusCode.OK)
                        {
                            totalContent += response.ContentLength;           
                        }
                        else
                            errors++;
                        response.Close();  
                    }
                }
                catch
                {
                    errors++;
                }
                finally
                {
                    number++;
                }
            }
        }


        private static int lastNumber = 0;
        private static char[] visualization = " 123456789ABCDEF*".ToCharArray();
        private static void printProgress()
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