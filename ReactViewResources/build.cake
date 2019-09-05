#tool "nuget:?package=Microsoft.TestPlatform&version=15.7.0"

////////////////////////////////////////////////////////////////
// Use always this structure. If you don't need to run some   //
// task, comment the code inside it.                          //
////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release"); //Here you can configure if you want do debug or release
var solutionPath=@"../WebView.sln";// Here you put the location of csproj file. If your csproj have dependencies with other, put the location of sln file instead.


var restore=Task("Restore-NuGet-Packages")
    .Does(() =>
{
    Information("Starting Restore");
    NuGetRestore(solutionPath); 
    Information("Ending Restore");
});

var build = Task("Build")
    .Does(()=>
    {
        Information("Starting Build");
        MSBuild(solutionPath, settings =>
            settings.SetConfiguration(configuration)
        );
        Information("Ending Build");
    });

var tests = Task("Tests")
    .Does(()=>
    {
        Information("Starting Tests");
        var testSettings = new VSTestSettings{
            ToolPath = Context.Tools.Resolve("vstest.console.exe")
        };
        VSTest(@"../Release/Tests.dll", testSettings); //Here you need to put your tests dll for the NuGet built. If you don't have, let the String empty.
        Information("Ending Tests");
    });

////////////////////////////////////////////////////////////////
// If you have a Nuspecfile, comment task packageNoNuspecFile //
// If you don't, comment packageNuspecFile                    //
////////////////////////////////////////////////////////////////

var packageNuspecFile = Task("Package")
    .Does(()=>
    {
        Information("Starting Package");
         var testSettings = new NuGetPackSettings{ //Define NuGet metadata. 
            OutputDirectory =  @"..\artifacts\"
        };
        NuGetPack(@"ReactViewResources.nuspec",testSettings); //Here you need to put nuspec file location.
        Information("Ending Package");
    });

/*var packageNoNuspecFile = Task("Package")
    .Does(()=>
    {
        Information("Starting Package");
         var settings = new DotNetCorePackSettings
        {
            Configuration = "Release",
            OutputDirectory = "./artifacts/"
        };

        DotNetCorePack(@"PATH",settings);  //Here you need to put your csproj location
       
        Information("Ending Package");
    });
*/
Task("Default")
    .IsDependentOn("Package");

RunTarget(target);