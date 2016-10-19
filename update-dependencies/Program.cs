﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.VersionTools;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Dependencies;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dotnet.Docker.Nightly
{
    public static class Program
    {
        private static readonly string s_repoRoot = Directory.GetCurrentDirectory();
        private static readonly Config s_config = Config.s_Instance;
        private static bool s_updateOnly = false;

        public static void Main(string[] args)
        {
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            if (ParseArgs(args))
            {
                DependencyUpdateResults updateResults = UpdateFiles();

                if (!s_updateOnly && updateResults.ChangesDetected())
                {
                    CreatePullRequest(updateResults).Wait();
                }
            }
        }
        
        private static bool ParseArgs(string[] args)
        {
            foreach (string arg in args)
            {
                if (string.Equals(arg, "--Update", StringComparison.OrdinalIgnoreCase))
                {
                    s_updateOnly = true;
                }
                else if (arg.Contains('='))
                {
                    int delimiterIndex = arg.IndexOf('=');
                    string name = arg.Substring(0, delimiterIndex);
                    string value = arg.Substring(delimiterIndex + 1);

                    Environment.SetEnvironmentVariable(name, value);
                }
                else
                {
                    Console.Error.WriteLine($"Unrecognized argument '{arg}'");
                    return false;
                }
            }

            return true;
        }

        private static DependencyUpdateResults UpdateFiles()
        {
            // The BuildInfo does not contain the CLI product version, so the version is retrieved from a
            // particular CLI package (e.g. Microsoft.DotNet.Cli.Utils). Once the BuildInfo includes the
            // product version, it should be utilized.
            //
            // This app does not update the version of the .NET Core runtime/framework in the Dockerfiles
            // because the infrastructure is not in place to retrieve the version on which the SDK depends.
            // This version is infrequently updated, so this is acceptable for now, but once the
            // infrastructure is in place, this app should update the runtime/framework version also.

            BuildInfo cliBuildInfo = BuildInfo.Get("Cli", s_config.CliVersionUrl, fetchLatestReleaseFile: false);
            IEnumerable<DependencyBuildInfo> buildInfos = 
                new[] { new DependencyBuildInfo(cliBuildInfo, true, Enumerable.Empty<string>()) };
            IEnumerable<IDependencyUpdater> updaters = GetUpdaters();

            return DependencyUpdateUtils.Update(updaters, buildInfos);
        }

        private static Task CreatePullRequest(DependencyUpdateResults updateResults)
        {
            string commitMessage = $"Update SDK to {updateResults.UsedBuildInfos.Single().LatestReleaseVersion}";

            GitHubAuth gitHubAuth = new GitHubAuth(s_config.Password, s_config.UserName, s_config.Email);

            PullRequestCreator prCreator = new PullRequestCreator(
                gitHubAuth,
                s_config.GitHubProject,
                s_config.GitHubUpstreamOwner,
                s_config.GitHubUpstreamBranch,
                s_config.UserName
            );

            return prCreator.CreateOrUpdateAsync(commitMessage, commitMessage, string.Empty);
        }

        private static IEnumerable<IDependencyUpdater> GetUpdaters()
        {
            string branchRoot = Path.Combine(s_repoRoot, s_config.CliBranch.Replace('/', '-'));
            return Directory.GetFiles(branchRoot, "Dockerfile", SearchOption.AllDirectories)
                .SelectMany(path => CreateRegexUpdaters(path, "Microsoft.DotNet.Cli.Utils"));
        }

        private static IEnumerable<IDependencyUpdater> CreateRegexUpdaters(string path, string packageId)
        {
            yield return new FileRegexPackageUpdater()
            {
                Path = path,
                PackageId = packageId,
                Regex = new Regex($@"ENV DOTNET_SDK_VERSION (?<version>[^\r\n]*)"),
                VersionGroupName = "version"
            };
            yield return new FileRegexChecksumUpdater()
            {
                Path = path,
                PackageId = packageId,
                Regex = new Regex($@"ENV DOTNET_SDK_SHA (?<sha>[^\r\n]*)"),
                VersionGroupName = "sha"
            };
        }
    }
}
