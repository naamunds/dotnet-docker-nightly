// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.VersionTools;
using Microsoft.DotNet.VersionTools.Dependencies;
using System.Collections.Generic;

namespace Dotnet.Docker.Nightly
{
    public class FileRegexChecksumUpdater : FileRegexPackageUpdater
    {
        protected override string TryGetDesiredValue(
            IEnumerable<DependencyBuildInfo> dependencyBuildInfos,
            out IEnumerable<BuildInfo> usedBuildInfos)
        {
            string version = base.TryGetDesiredValue(dependencyBuildInfos, out usedBuildInfos);

            // TODO:
            // 1. Retrieve DOTNET_SDK_DOWNLOAD_URL from file
            // 2. Calculate checksum URL by replacing dotnetcli with dotnetclichecksums and appending "sha"
            //    and substuting {version} for $DOTNET_SDK_VERSION
            // 3. Return the checksum by reading the file at the checksum URL

            return null;
        }
    }
}
