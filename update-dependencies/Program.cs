// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.VersionTools;
using Microsoft.DotNet.VersionTools.Automation;
using Microsoft.DotNet.VersionTools.Dependencies;
using System;
using System.Collections.Generic;
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
            IEnumerable<BuildInfo> buildInfos = new[] { BuildInfo.Get("Cli", s_config.CliVersionUrl, fetchLatestReleaseFile: false) };
            IEnumerable<IDependencyUpdater> updaters = GetUpdaters();

            DependencyUpdater updater = new DependencyUpdater();
            return updater.Update(updaters, buildInfos);
        }

        private static Task CreatePullRequest(DependencyUpdateResults updateResults)
        {
            string commitMessage = $"Update CLI to {updateResults.UsedBuildInfos.Single().LatestReleaseVersion}";

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
            return Directory.GetFiles(s_repoRoot, "Dockerfile", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains("ENV DOTNET_SDK_VERSION")) 
                .Select(path => CreateRegexUpdater(path, "Microsoft.DotNet.Cli.Utils"));
        }

        private static IDependencyUpdater CreateRegexUpdater(string path, string packageId)
        {
            return new FileRegexPackageUpdater()
            {
                Path = path,
                PackageId = packageId,
                Regex = new Regex($@"ENV DOTNET_SDK_VERSION (?<version>.*)"),
                VersionGroupName = "version"
            };
        }
    }
}
