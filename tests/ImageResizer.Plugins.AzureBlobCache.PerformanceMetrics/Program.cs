using CommandLine;
using ImageResizer.Caching.Core;
using ImageResizer.Caching.Core.Identity;
using ImageResizer.Caching.Core.Indexing;
using ImageResizer.Configuration;
using ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Models;
using ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Pages;
using ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Services;
using ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.TestPages;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SystemTimer = System.Timers.Timer;

namespace ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics
{
    public class Options
    {
        [Option('i', "input", Required = true, HelpText = "Path to search recursively.")]
        public string Input { get; set; }

        [Option('o', "output", Required = true, HelpText = "Path to output to.")]
        public string Output { get; set; }

        [Option('r', "requests", Required = false, Default = 60, HelpText = "Amount of requests to simulate (per second).")]
        public int Requests { get; set; }

        [Option('p', "period", Required = false, Default = "00:01:00", HelpText = "Testing period")]
        public string Period { get; set; }

        [Option('b', "baseline", Required = false, Default = false, HelpText = "Testing period")]
        public bool Baseline { get; set; }
    }

    class Program
    {
        const string BlobConnectionName = "ResizerAzureBlobs";
        const string BlobContainerName = "testblobperformance";

        static readonly Random _randomizer = new Random();
        static readonly string[] _extensions = new[]
        {
            ".jpg", ".jpeg", ".png"
        };

        static async Task Main(string[] args)
        {
            await Parser.Default.ParseArguments<Options>(args)
                                .MapResult((o) => Run(o), (e) => Task.FromResult(Abort(e)));
        }

        static async Task Run(Options options)
        {
            try
            {
                EnsurePath(options.Input);
                EnsurePathCreated(options.Output);

                var configuration = Config.Current;
                var testPeriod = TimeSpan.Parse(options.Period);
                var requestPeriod = TimeSpan.FromSeconds(1.0 / options.Requests);

                Console.WriteLine("Reading input directory...");
                var files = GetFiles(options.Input);

                Console.WriteLine($"Generating random pages from {files.Count} found images...");
                var pages = GeneratePages(files);

                Report report;
                string reportName;

                Console.WriteLine($"Simulating {options.Requests} requests per second for {testPeriod.TotalSeconds} seconds...");

                if (options.Baseline)
                {
                    reportName = "Report-baseline";
                    report = await RunBaselineAnalysis(testPeriod, requestPeriod, pages);

                    Console.WriteLine($"Baseline average response time is {report.Average:0.00} ms.");
                }
                else
                {
                    reportName = "Report";
                    report = await RunCacheAnalysis(testPeriod, requestPeriod, pages);

                    Console.WriteLine($"Cached average response time is {report.Average:0.00} ms.");
                }

                var outputFile = $"{reportName}-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv";

                Console.WriteLine($"Writing report: {outputFile} to disk");
                File.WriteAllText(Path.Combine(options.Output, outputFile), CompileReport(report));

                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal exception: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static string CompileReport(Report report)
        {
            var stringBuilder = new StringBuilder();
            var headings = new [] { "#", "Result", "Time", "Elapsed", "Processor", "Memory" };

            stringBuilder.AppendLine(string.Join("\t", headings));

            for (var i = 0; i < report.DataPoints.Count; i++)
            {
                var point = report.DataPoints[i];
                var line = new [] { $"{i + 1}", point.Result.ToString(), point.StartedMilliseconds.ToString(), point.ElapsedMilliseconds.ToString(), point.Processor.ToString(), point.Memory.ToString() };

                stringBuilder.AppendLine(string.Join("\t", line));
            }

            return stringBuilder.ToString();
        }

        static async Task<Report> RunBaselineAnalysis(TimeSpan testPeriod, TimeSpan requestPeriod, IList<PageInstructions> pages)
        {
            return await RunAnalysis(testPeriod, requestPeriod, pages, (path, instructions) => 
            {
                using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var buffer = new MemoryStream(4096))
                {
                    var job = new ImageJob(source, buffer, instructions);
                    
                    job.Build();
                    buffer.Seek(0, SeekOrigin.Begin);

                    // Simulate copy action
                    var content = buffer.ToArray();

                    return Task.FromResult(CacheQueryResult.Miss);
                }
            });
        }

        static async Task<Report> RunCacheAnalysis(TimeSpan testPeriod, TimeSpan requestPeriod, IList<PageInstructions> pages)
        {
            var provider = GetCacheProvider();
            var keyGenerator = GetKeyGenerator();
            var usedKeys = new Dictionary<Guid, string>(pages.Count);

            return await RunAnalysis(testPeriod, requestPeriod, pages, async (path, instructions) =>
            {
                var requestKey = $"{path}|{instructions}";
                var cacheKey = keyGenerator.Generate(requestKey);
                var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                
                var cacheResult = await provider.GetAsync(cacheKey, tokenSource.Token);
                if (cacheResult.Result == CacheQueryResult.Miss)
                {
                    cacheResult = await provider.CreateAsync(cacheKey, tokenSource.Token, (stream) => 
                    {
                        using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            var job = new ImageJob(source, stream, instructions);
                            job.Build();
                        }

                        return Task.CompletedTask;
                    });
                }

                return cacheResult.Result;
            });
        }

        static async Task<Report> RunAnalysis(TimeSpan testPeriod, TimeSpan requestPeriod, IList<PageInstructions> pages, Func<string, Instructions, Task<CacheQueryResult>> task)
        {
            ThreadPool.GetAvailableThreads(out int workerThreads, out int completionPortThreads);

            var completionSource = new TaskCompletionSource<bool>();
            
            using (var pageLoadWorker = new LoadWorker(pages, workerThreads, requestPeriod, task))
            using (var periodTimer = GetTimer(testPeriod, () => completionSource.SetResult(true)))
            {
                pageLoadWorker.Start();
                await completionSource.Task;
                
                pageLoadWorker.Stop();
                return pageLoadWorker.GetReport();
            }
        }

        static SystemTimer GetTimer(TimeSpan timeSpan, Action action)
        {
            var timer = new SystemTimer
            {
                Interval = timeSpan.TotalMilliseconds,
                AutoReset = false,
                Enabled = true
            };

            timer.Elapsed += (o, s) => action();

            return timer;
        }

        static int Abort(IEnumerable<Error> errors)
        {
            foreach (var error in errors)
            {
                Console.WriteLine(error.Tag);
            }

            return 0;
        }

        static IList<PageInstructions> GeneratePages(ICollection<string> files)
        {
            var pages = new List<PageInstructions>();
            var images = new Queue<string>(files);

            while (images.Count > 0)
            {
                var (type, required, max) = GetRandomPageType();

                if (required > images.Count)
                    break;

                if (max > images.Count)
                    max = images.Count;

                var count = _randomizer.Next(required, max);
                var pageImages = new List<string>(count);

                for (var i = 0; i < count; i++)
                {
                    pageImages.Add(images.Dequeue());
                }

                var page = GetPage(type, pageImages);
                var instructions = new PageInstructions(page.GetImageGroups());

                pages.Add(instructions);
            }

            return pages;
        }

        static (PageType type, int required, int max) GetRandomPageType()
        {
            var seed = _randomizer.Next(0, 3);
            var type = (PageType)seed;
            var required = 8;
            var max = 15;

            switch (type)
            {
                case PageType.CampaignPage: required = 5; max = 25; break;
                case PageType.CategoryPage: required = 2; max = 50; break;
            }

            return (type, required, max);

        }

        static PageBase GetPage(PageType type, IList<string> images)
        {
            switch (type)
            {
                case PageType.CampaignPage: return new CampaignPage(images);
                case PageType.CategoryPage: return new CategoryPage(images);
                default: return new ProductPage(images);
            }
        }

        static ICollection<string> GetFiles(string path)
        {
            var filter = new HashSet<string>(_extensions, StringComparer.InvariantCultureIgnoreCase);

            return Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                            .Where(x => filter.Contains(Path.GetExtension(x)))
                            .ToList();
        }

        static void EnsurePathCreated(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        static void EnsurePath(string path)
        {
            if (!Directory.Exists(path))
                throw new IOException($"Path '{path}' does not exist");
        }

        static string GetConnection(string name)
        {
            return ConfigurationManager.ConnectionStrings[name]?.ConnectionString;
        }

        static ICacheKeyGenerator GetKeyGenerator()
        {
            return new MD5CacheKeyGenerator();
        }

        static IAsyncCacheProvider GetCacheProvider()
        {
            return new AzureBlobCache(GetConnection(BlobConnectionName), BlobContainerName, GetCacheIndex(), GetCacheStore());
        }

        static ICacheIndex GetCacheIndex()
        {
            var connection = GetConnection("ResizerEfConnection");

            if (!string.IsNullOrEmpty(connection))
                return new AzureBlobCacheIndex(GetConnection(BlobConnectionName), BlobContainerName, null, 100_000);

            return new NullCacheIndex();
        }

        static ICacheStore GetCacheStore()
        {
            return new AzureBlobCacheMemoryStore(1000, "00:05:00");
        }
    }
}
