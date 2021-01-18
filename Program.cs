using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using PureZSTD.Impl;

namespace PureZSTD
{

    public static class Benchmark
    {

        private static System.Diagnostics.Stopwatch Watch;

        private static TimeSpan Sum;

        public static void Reset() => Sum = new TimeSpan();
        public static void Start()
        {
            Watch = new System.Diagnostics.Stopwatch();
            Watch.Start();
        } 
        public static void Stop() 
        {
            Watch.Stop();
            Sum += Watch.Elapsed;
        }

        public static double MS => Sum.TotalMilliseconds;

        public static void Print() => Console.WriteLine($"Execution Time: {MS} ms");
    }

    class Program
    {
        private static FrameDecoder decoder = new FrameDecoder(32);
        private static void TestFile(string file)
        {
            if (file.EndsWith(".zst"))
            {
                using(var src = File.OpenRead(file))
                {
                    decoder.Init(null, false);
                    using(var dst = File.OpenWrite(file.Split(".zst")[0]))
                    {
                        while (!decoder.Done)
                        {
                            var readLength = src.Read(decoder.ReadBuffer);
                            decoder.ReadConsume(readLength);
                            if (decoder.NeedsFlush)
                            {
                                var writeLength = decoder.WriteLength;
                                dst.Write(decoder.WriteBuffer);
                                decoder.WriteConsume(writeLength);
                            }
                        }
                    }
                }
            }
        }

        private static void TestDir(string dir)
        {
            foreach(var file in Directory.GetFiles(dir))
            {
                TestFile(file);
            }
        }
        
        static void TestNoBenchmark()
        {
            for (var i = 0; i < 10; ++i)
            {
                TestDir("./decodecorpus_files");

            }
        }

        static void TestBenchmark()
        {
            for (var i = 0; i < 10; ++i)
            {
                Benchmark.Reset();
                Benchmark.Start();
                //TestFile("src.zip.zst");
                //TestFile("./raw.zst");
                TestDir("./decodecorpus_files");
                Benchmark.Stop();
                Benchmark.Print();
            }
        }

        static void Main(string[] args)
        {
            TestBenchmark();
        }
    }
}
