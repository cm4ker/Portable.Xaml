﻿using System;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Validators;

namespace Portable.Xaml.Benchmark
{
	class MainClass
	{
		public static void Main(string[] args)
		{
			/**  Uncomment to test using performance profiler */
			if (args?.FirstOrDefault() == "profile")
			{
				//var benchmark = new Json.JsonLoadComplexBenchmark();
				var benchmark = new Json.JsonSaveComplexBenchmark();
				//var benchmark = new Xml.XmlLoadSimpleBenchmark();
				//var benchmark = new Xml.XmlLoadComplexBenchmark();
				//var benchmark = new Xml.XmlSaveSimpleBenchmark();
				//var benchmark = new Xml.XmlSaveComplexBenchmark();
				for (int i = 0; i < 1000; i++)
				{
					benchmark.PortableXaml();
					//benchmark.PortableXamlNoCache();
					//benchmark.SystemXaml();
					//benchmark.SystemXamlNoCache();
				}
				return;
			}
			/**/

			// BenchmarkSwitcher doesn't automatically exclude abstract benchmark classes
			var types = typeof(MainClass)
				.Assembly
				.GetExportedTypes()
				.Where(r => typeof(IXamlBenchmark).IsAssignableFrom(r) && !r.IsAbstract)
				.OrderBy(r => r.Name);


			var config = new ManualConfig();
        	config.Add(DefaultConfig.Instance.GetLoggers().ToArray());
        	config.Add(DefaultConfig.Instance.GetExporters().ToArray());
        	config.Add(DefaultConfig.Instance.GetColumnProviders().ToArray());

			config.Add(JitOptimizationsValidator.DontFailOnError);
			config.Add(Job.Default);
			config.Add(MemoryDiagnoser.Default);
			config.Add(StatisticColumn.OperationsPerSecond);
			config.Add(RankColumn.Arabic);

			var switcher = new BenchmarkSwitcher(types.ToArray());
			switcher.Run(args, config);
		}
	}
}
