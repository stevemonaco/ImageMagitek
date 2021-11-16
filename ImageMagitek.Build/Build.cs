using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[ShutdownDotNetAfterServerBuild]
[GitHubActions("ci",
    GitHubActionsImage.WindowsLatest,
    PublishArtifacts = true,
    On = new[] { GitHubActionsTrigger.WorkflowDispatch },
    InvokedTargets = new[] { nameof(Package) }
)]
class Build : NukeBuild
{
    public static int Main () => Execute<Build>(x => x.Package);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    public readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter("TileShop version")]
    public readonly string TileShopVersion;

    [Parameter("TileShopCLI version")]
    public readonly string TileShopCLIVersion;

    [Solution]
    public readonly Solution Solution;

    [GitRepository]
    public readonly GitRepository GitRepository;

    AbsolutePath TestsDirectory => RootDirectory / "tests";
    AbsolutePath OutputDirectory => RootDirectory / "output";

    Project TestProject => Solution.GetProject("ImageMagitek.UnitTests");

    Project TileShopProject => Solution.GetProject("TileShop.WPF");
    AbsolutePath TileShopOutputDirectory => OutputDirectory / "TileShop";
    string TileShopPublishProfilex64 = @"Properties\PublishProfiles\TileShop win-x64-single.pubxml";
    string TileShopPublishProfilex86 = @"Properties\PublishProfiles\TileShop win-x86-single.pubxml";

    Project TileShopCLIProject => Solution.GetProject("TileShop.CLI");
    AbsolutePath TileShopCLIPortableOutputDirectory => OutputDirectory / "TileShopCLI";
    AbsolutePath TileShopCLIWinx64OutputDirectory => OutputDirectory / "TileShopCLI-win-x64";
    string TileShopCliPublishProfilex64 = @"Properties\PublishProfiles\TileShop.CLI win-x64-single.pubxml";
    string TileShopCliPublishProfilePortable = @"Properties\PublishProfiles\TileShop.CLI portable.pubxml";

    Target Clean => _ => _
        .Executes(() =>
        {
            var dirs = RootDirectory.GlobDirectories("**/bin", "**/obj")
                .Where(x => !x.ToString().Contains("ImageMagitek.Build"));

            dirs.ForEach(DeleteDirectory);

            EnsureCleanDirectory(OutputDirectory);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
        DotNetBuild(s => s
            .SetProjectFile(Solution)
            .SetConfiguration(Configuration)
            .EnableNoRestore());
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(_ => _
                .SetProjectFile(TestProject)
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .EnableNoBuild());
        });

    Target PublishTileShop => _ => _
        .DependsOn(Test)
        .Executes(() =>
        {
            // win-x64 single file
            DotNetPublish(_ => _
                .SetProject(TileShopProject)
                .EnableNoRestore()
                .SetConfiguration(Configuration)
                .SetOutput(TileShopOutputDirectory)
                .SetPublishProfile(TileShopPublishProfilex64));
        });

    Target PublishTileShopCLI => _ => _
        .DependsOn(Test)
        .Executes(() =>
        {
            // Portable
            DotNetPublish(_ => _
                .SetProject(TileShopCLIProject)
                .EnableNoRestore()
                .SetConfiguration(Configuration)
                .SetOutput(TileShopCLIPortableOutputDirectory)
                .SetPublishProfile(TileShopCliPublishProfilePortable));

            // win-x64 single file
            DotNetPublish(_ => _
                .SetProject(TileShopCLIProject)
                .EnableNoRestore()
                .SetConfiguration(Configuration)
                .SetOutput(TileShopCLIWinx64OutputDirectory)
                .SetPublishProfile(TileShopCliPublishProfilex64));
        });

    Target Package => _ => _
        .DependsOn(PublishTileShop, PublishTileShopCLI)
        .Requires(() => Configuration == Configuration.Release)
        .Produces(OutputDirectory / "*.zip")
        .Executes(() =>
        {
            var files = Directory.GetFiles(TileShopOutputDirectory)
                .Concat(Directory.GetFiles(TileShopCLIPortableOutputDirectory))
                .Where(filename => Path.GetExtension(filename).Equals(".pdb", StringComparison.OrdinalIgnoreCase));
            
            foreach (var file in files)
                File.Delete(file);
            
            File.Copy(TileShopCLIWinx64OutputDirectory / "TileShopCLI.exe", TileShopOutputDirectory / "TileShopCLI.exe");
            
            ZipFile.CreateFromDirectory(TileShopOutputDirectory,
                OutputDirectory / $"TileShop v{TileShopVersion}.zip",
                CompressionLevel.Optimal,
                true);

            ZipFile.CreateFromDirectory(TileShopCLIPortableOutputDirectory,
                OutputDirectory / $"TileShopCLI v{TileShopCLIVersion}.zip",
                CompressionLevel.Optimal, 
                true);
        });
}
