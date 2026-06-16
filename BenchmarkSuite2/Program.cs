// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// -----------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------------------

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using System;
using System.IO;

namespace BenchmarkSuite2
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string driveRoot = Path.GetPathRoot(AppContext.BaseDirectory) ?? "C:\\";
            string diagnosticsTempPath = Path.Combine(driveRoot, "kctmp");
            string artifactsPath = Path.Combine(driveRoot, "kcart");
            Directory.CreateDirectory(diagnosticsTempPath);
            Directory.CreateDirectory(artifactsPath);
            Environment.SetEnvironmentVariable("TEMP", diagnosticsTempPath);
            Environment.SetEnvironmentVariable("TMP", diagnosticsTempPath);
            IConfig config = ManualConfig.Create(DefaultConfig.Instance).WithArtifactsPath(artifactsPath);
            var _ = BenchmarkRunner.Run(typeof(Program).Assembly, config);
        }
    }
}
