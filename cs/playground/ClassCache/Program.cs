﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.

using FASTER.core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassCache
{
    class Program
    {
        static void Main(string[] args)
        {
            var context = default(CacheContext);

            var log =  Devices.CreateLogDevice(Path.GetTempPath() + "hlog.log", deleteOnClose: true);
            var objlog = Devices.CreateLogDevice(Path.GetTempPath() + "hlog.obj.log", deleteOnClose: true);
            var h = new FasterKV
                <CacheKey, CacheValue, CacheInput, CacheOutput, CacheContext, CacheFunctions>(
                1L << 20, new CacheFunctions(),
                new LogSettings { LogDevice = log, ObjectLogDevice = objlog },
                null,
                new SerializerSettings<CacheKey, CacheValue> { keySerializer = () => new CacheKeySerializer(), valueSerializer = () => new CacheValueSerializer() }
                );

            h.StartSession();

            const int max = 10000000;

            Stopwatch sw = new Stopwatch();
            sw.Start();
            for (int i = 0; i < max; i++)
            {
                if (i % 256 == 0)
                {
                    h.Refresh();
                    if (i % (1<<19) == 0)
                    {
                        long workingSet = Process.GetCurrentProcess().WorkingSet64;
                        Console.WriteLine($"{i}: {workingSet / 1048576}M");
                    }
                }
                var key = new CacheKey(i);
                var value = new CacheValue(i);
                h.Upsert(ref key, ref value, context, 0);
            }
            sw.Stop();
            Console.WriteLine("Total time to upsert {0} elements: {1:0.000} secs ({2:0.00} inserts/sec)", max, sw.ElapsedMilliseconds/1000.0, max / (sw.ElapsedMilliseconds / 1000.0));

            
            Console.WriteLine("Issuing uniform random read workload");

            var rnd = new Random();

            int statusPending = 0;
            var output = new CacheOutput();
            var input = default(CacheInput);
            sw.Restart();
            for (int i = 0; i < max; i++)
            {
                long k = rnd.Next(max);

                var key = new CacheKey(k);
                var status = h.Read(ref key, ref input, ref output, context, 0);

                switch (status)
                {
                    case Status.PENDING:
                        h.CompletePending(true);
                        statusPending++; break;
                    case Status.ERROR:
                        throw new Exception("Error!");
                }
                if (output.value.value != key.key)
                    throw new Exception("Read error!");
            }
            sw.Stop();
            Console.WriteLine("Total time to read {0} elements: {1:0.000} secs ({2:0.00} reads/sec)", max, sw.ElapsedMilliseconds / 1000.0, max / (sw.ElapsedMilliseconds / 1000.0));
            Console.WriteLine($"Reads completed with PENDING: {statusPending}");

            Console.WriteLine("Done");
            Console.ReadLine();
        }
    }
}
