#tool "nuget:?package=GitVersion.CommandLine&version=4.0.0-beta0012"

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var coin = Argument("coin", "BCH");

var solutionName = "bitprim.insight.sln";
var solutionTutorialsName = "bitprim.insight.tutorials.sln";

var platform = "/property:Platform=x64";

Task("Clean")
    .Does(() => {
        Information("Cleaning... ");
        CleanDirectory("./bitprim.insight/bin");
        CleanDirectory("./bitprim.insight.tests/bin");
        CleanDirectory("./bitprim.insight.tutorials/bin");
        CleanDirectory("./bitprim.insight.tutorials.tests/bin");
    });

Task("Restore")
    .Does(() => {
        DotNetCoreRestore(solutionName);
        DotNetCoreRestore(solutionTutorialsName);
    });

GitVersion versionInfo = null;
Task("Version")
    .Does(() => {
        
        GitVersion(new GitVersionSettings{
            UpdateAssemblyInfo = false,
            OutputType = GitVersionOutput.BuildServer
        });
        
        versionInfo = GitVersion(new GitVersionSettings{ OutputType = GitVersionOutput.Json });        

        Information("Version calculated: " + versionInfo.MajorMinorPatch);
    });


Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Version")
    .IsDependentOn("Restore")
    .Does(() => {

        Information("Building " + coin);

        MSBuild(solutionName, new MSBuildSettings {
            ArgumentCustomization = args => args.Append(platform + " /p:" + coin + "=true"),        
            Configuration = configuration
        });

        MSBuild(solutionTutorialsName, new MSBuildSettings {
            ArgumentCustomization = args => args.Append(platform + " /p:BCH=true"),        
            Configuration = configuration
        });
    });

Task("Test")
    .IsDependentOn("Build")
    .Does(() => {
        
        DotNetCoreTest("./bitprim.insight.tests",new DotNetCoreTestSettings
            {
                ArgumentCustomization = args=> args.Append(platform + " /p:" + coin + "=true"),
                Configuration = configuration
            });

        DotNetCoreTest("./bitprim.insight.tutorials.tests",new DotNetCoreTestSettings
            {
                ArgumentCustomization = args=> args.Append(platform + " /p:BCH=true"),
                Configuration = configuration
            });
    });


Task("UpdateVersionInfo")
    .IsDependentOn("Test")
    .WithCriteria(AppVeyor.IsRunningOnAppVeyor)
    .Does(() =>
    {
        var isTag = AppVeyor.Environment.Repository.Tag.IsTag && !string.IsNullOrWhiteSpace(AppVeyor.Environment.Repository.Tag.Name);
        if (isTag) 
        {
            AppVeyor.UpdateBuildVersion(AppVeyor.Environment.Repository.Tag.Name);
        }
    });


Task("Default")
    .IsDependentOn("UpdateVersionInfo");

RunTarget(target);