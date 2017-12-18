/////////////////////////////////////////////////////////////////////
// ADDINS
/////////////////////////////////////////////////////////////////////

#addin "Cake.FileHelpers"
#addin "nuget:?package=NuGet.Core&version=2.14.0"

//////////////////////////////////////////////////////////////////////
// TOOLS
//////////////////////////////////////////////////////////////////////

///////////////////////////////////////////////////////////////////////////////
// USINGS
///////////////////////////////////////////////////////////////////////////////

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var platform = Argument("platform", "AnyCPU");
var configuration = Argument("configuration", "Release");

///////////////////////////////////////////////////////////////////////////////
// CONFIGURATION
///////////////////////////////////////////////////////////////////////////////

var MainRepo = "VitalElement/AvalonStudioTemplates";
var MasterBranch = "master";
var ReleasePlatform = "Any CPU";
var ReleaseConfiguration = "Release";

///////////////////////////////////////////////////////////////////////////////
// PARAMETERS
///////////////////////////////////////////////////////////////////////////////

var isPlatformAnyCPU = StringComparer.OrdinalIgnoreCase.Equals(platform, "Any CPU");
var isPlatformX86 = StringComparer.OrdinalIgnoreCase.Equals(platform, "x86");
var isPlatformX64 = StringComparer.OrdinalIgnoreCase.Equals(platform, "x64");
var isLocalBuild = BuildSystem.IsLocalBuild;
var isRunningOnUnix = IsRunningOnUnix();
var isRunningOnWindows = IsRunningOnWindows();
var isRunningOnAppVeyor = BuildSystem.AppVeyor.IsRunningOnAppVeyor;
var isPullRequest = BuildSystem.AppVeyor.Environment.PullRequest.IsPullRequest;
var isMainRepo = StringComparer.OrdinalIgnoreCase.Equals(MainRepo, BuildSystem.AppVeyor.Environment.Repository.Name);
var isMasterBranch = StringComparer.OrdinalIgnoreCase.Equals(MasterBranch, BuildSystem.AppVeyor.Environment.Repository.Branch);
var isTagged = BuildSystem.AppVeyor.Environment.Repository.Tag.IsTag 
               && !string.IsNullOrWhiteSpace(BuildSystem.AppVeyor.Environment.Repository.Tag.Name);
var isMyGetRelease = !isTagged;
var isNuGetRelease = isTagged;

var artifactsDir = (DirectoryPath)Directory("./artifacts");
var buildDir = artifactsDir.Combine("build");
var nugetRoot = artifactsDir.Combine("nuget");

///////////////////////////////////////////////////////////////////////////////
// VERSION
///////////////////////////////////////////////////////////////////////////////

var version = "0.2.0";

if (isRunningOnAppVeyor)
{
    if (isTagged)
    {
        // Use Tag Name as version
        version = BuildSystem.AppVeyor.Environment.Repository.Tag.Name;
    }
    else
    {
        // Use AssemblyVersion with Build as version
        version += "-build" + EnvironmentVariable("APPVEYOR_BUILD_NUMBER") + "-alpha";
    }
}

///////////////////////////////////////////////////////////////////////////////
// NUGET NUSPECS
///////////////////////////////////////////////////////////////////////////////

public NuGetPackSettings GetPackSettings(string version, string nugetRoot)
{
    var nuspecNuGetBehaviors = new NuGetPackSettings()
    {
        Id = "VitalElement.AvalonStudio.Templates",
        Version = version,
        Authors = new [] { "VitalElement" },
        Owners = new [] { "Dan Walmsley (dan at walms.co.uk)" },
        LicenseUrl = new Uri("http://opensource.org/licenses/MIT"),
        ProjectUrl = new Uri("https://github.com/VitalElement/AvalonStudio/"),
        RequireLicenseAcceptance = false,
        Symbols = false,
        NoPackageAnalysis = true,
        Description = "Default Templates for AvalonStudio",
        Copyright = "Copyright 2017",
        Tags = new [] { "AvalonStudio" },
        Files = new []
        {
            new NuSpecContent { Source = "**", Target = "content/" },
        },
        BasePath = buildDir,
        OutputDirectory = nugetRoot
    };

    return nuspecNuGetBehaviors;
}

///////////////////////////////////////////////////////////////////////////////
// INFORMATION
///////////////////////////////////////////////////////////////////////////////

Information("Building version {0} of AvalonStudio Templates ({1}, {2}, {3}) using version {4} of Cake.", 
    version,
    platform,
    configuration,
    target,
    typeof(ICakeContext).Assembly.GetName().Version.ToString());

if (isRunningOnAppVeyor)
{
    Information("Repository Name: " + BuildSystem.AppVeyor.Environment.Repository.Name);
    Information("Repository Branch: " + BuildSystem.AppVeyor.Environment.Repository.Branch);
}

Information("Target: " + target);
Information("Platform: " + platform);
Information("Configuration: " + configuration);
Information("IsLocalBuild: " + isLocalBuild);
Information("IsRunningOnUnix: " + isRunningOnUnix);
Information("IsRunningOnWindows: " + isRunningOnWindows);
Information("IsRunningOnAppVeyor: " + isRunningOnAppVeyor);
Information("IsPullRequest: " + isPullRequest);
Information("IsMainRepo: " + isMainRepo);
Information("IsMasterBranch: " + isMasterBranch);
Information("IsTagged: " + isTagged);
Information("IsMyGetRelease: " + isMyGetRelease);
Information("IsNuGetRelease: " + isNuGetRelease);

///////////////////////////////////////////////////////////////////////////////
// TASKS
/////////////////////////////////////////////////////////////////////////////// 

Task("Clean")
.Does(()=>{
    CleanDirectory(nugetRoot);
    CleanDirectory(buildDir);
});

Task("Build")
.Does(()=>{
    CopyDirectory ("Cs", buildDir);
    CopyDirectory ("Cpp", buildDir);
});

Task("Generate-NuGetPackages")
.WithCriteria(()=>((isTagged && isMainRepo && isMasterBranch && isRunningOnAppVeyor && !isPullRequest) || isLocalBuild))
.Does(()=>{
        NuGetPack(GetPackSettings(version, nugetRoot.ToString()));
});

Task("Publish-AppVeyorNuget")
    .IsDependentOn("Generate-NuGetPackages")        
    .WithCriteria(()=>(isTagged && isMainRepo && isMasterBranch && isRunningOnAppVeyor && !isPullRequest))   
    .Does(() =>
{
    var apiKey = EnvironmentVariable("NUGET_API_KEY");
    if(string.IsNullOrEmpty(apiKey)) 
    {
        throw new InvalidOperationException("Could not resolve MyGet API key.");
    }

    var apiUrl = EnvironmentVariable("NUGET_API_URL");
    if(string.IsNullOrEmpty(apiUrl)) 
    {
        throw new InvalidOperationException("Could not resolve MyGet API url.");
    }

    var nuspec = GetPackSettings(version, nugetRoot.ToString());
        var settings  = nuspec.OutputDirectory.CombineWithFilePath(string.Concat(nuspec.Id, ".", nuspec.Version, ".nupkg"));

        NuGetPush(settings, new NuGetPushSettings
        {
            Source = apiUrl,
            ApiKey = apiKey
        });
});

Task("Default")
    .IsDependentOn("Clean")
    .IsDependentOn("Build")
    .IsDependentOn("Generate-NuGetPackages")
    .IsDependentOn("Publish-AppVeyorNuget");

RunTarget(target);
