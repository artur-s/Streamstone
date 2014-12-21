﻿#r "System.Xml"
#r "System.Xml.Linq"

using Nake.FS;
using Nake.Run;
using Nake.Log;

using System.Linq;
using System.Xml.Linq;
using System.Diagnostics;

const string Project = "Streamstone";
const string RootPath = "$NakeScriptDirectory$";
const string OutputPath = RootPath + @"\Output";
const string PackagePath = OutputPath + @"\Package";
const string ReleasePath = PackagePath + @"\Release";

/// Builds sources in Debug mode
[Task] void Default()
{
    Build();
}

/// Wipeout all build output and temporary build files
[Step] void Clean(string path = OutputPath)
{
    Delete(@"{path}\*.*|-:*.vshost.exe");
    RemoveDir(@"**\bin|**\obj|{path}\*|-:*.vshost.exe");
}

/// Builds sources using specified configuration and output path
[Step] void Build(string config = "Debug", string outDir = OutputPath)
{
    Clean(outDir);
    
    Exec(@"$ProgramFiles(x86)$\MSBuild\12.0\Bin\MSBuild.exe", 
          "{Project}.sln /p:Configuration={config};OutDir={outDir};ReferencePath={outDir}");
}

/// Runs unit tests 
[Step] void Test(string outDir = OutputPath)
{
    Build("Debug", outDir);

    var tests = new FileSet{@"{outDir}\*.Tests.dll"}.ToString(" ");
    Cmd(@"Packages\NUnit.Runners.2.6.3\tools\nunit-console.exe /framework:net-4.0 /noshadow /nologo {tests}");
}

/// Builds official NuGet package 
[Step] void Package()
{
    Test(PackagePath + @"\Debug");
    Build("Release", ReleasePath);

    var version = FileVersionInfo
        .GetVersionInfo(@"{ReleasePath}\{Project}.dll")
        .FileVersion;

    Cmd(@"Tools\Nuget.exe pack Build\{Project}.nuspec -Version {version} " +
        "-OutputDirectory {PackagePath} -BasePath {RootPath} -NoPackageAnalysis");
}

/// Publishes package to NuGet gallery
[Step] void Publish()
{
    Cmd(@"Tools\Nuget.exe push {PackagePath}\{Project}.{Version()}.nupkg $NuGetApiKey$");
}

string Version()
{
    return FileVersionInfo
            .GetVersionInfo(@"{ReleasePath}\{Project}.dll")
            .FileVersion;
}

/// Installs dependencies (packages) from NuGet 
[Task] void Install()
{
    var packagesDir = @"{RootPath}\Packages";

    var configs = XElement
        .Load(packagesDir + @"\repositories.config")
        .Descendants("repository")
        .Select(x => x.Attribute("path").Value.Replace("..", RootPath)); 

    foreach (var config in configs)
        Cmd(@"Tools\NuGet.exe install {config} -o {packagesDir}");

	// install packages required for building/testing/publishing package
    Cmd(@"Tools\NuGet.exe install Build/Packages.config -o {packagesDir}");
}