#tool "nuget:?package=Microsoft.TestPlatform&version=15.7.0"
#addin nuget:?package=Cake.Json&version=4.0.0
#addin nuget:?package=Newtonsoft.Json&version=9.0.1
#addin nuget:?package=Cake.Incubator&version=5.1.0

////////////////////////////////////////////////////////////////
// Use always this structure. If you don't need to run some   //
// task, comment the code inside it.                          //
////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
string solutionPath;
string testsdllFilePath;
string nuspecFilePath;
string csprojFilePath;
string packageRootPath;
var config=Task("Configuration")
    .Does(()=>
    {
        Information("Starting Configuration");
        var conf = ParseJsonFromFile("config.json");
        if(conf["csprojFilePath"]!=null){
            csprojFilePath=conf["csprojFilePath"].ToString();
            if(conf["slnFilePath"]!=null)
                solutionPath=conf["slnFilePath"].ToString();
            else
                solutionPath=conf["csprojFilePath"].ToString();
        }
        else if(conf["slnFilePath"]!=null)
            solutionPath=conf["slnFilePath"].ToString();
        else{
            Information("No .csproj or .sln file found");
        }
        if(conf["configuration"]!=null)
            configuration=conf["configuration"].ToString();
        if(conf["testsdllFilePath"]!=null){
            testsdllFilePath=conf["testsdllFilePath"].ToString();
        }else{
            Information("Tests didn't found");
        }
        if(conf["nuspecFilePath"]!=null)
            nuspecFilePath=conf["nuspecFilePath"].ToString();
        if(conf["packageRootPath"]!=null){
            packageRootPath=conf["packageRootPath"].ToString();
        }else{
            throw new Exception(String.Format("You need to specify the root path of the package in the config file!"));
        }
        Information("Ending Configuration");
    });

var restore=Task("Restore-NuGet-Packages")
    .IsDependentOn("Configuration")
    .Does(() =>
    {
        Information("Starting Restore");
        if(solutionPath!=null)
            NuGetRestore(solutionPath); 
        Information("Ending Restore");
    });

var build = Task("Build")
    .IsDependentOn("Configuration")
    .Does(()=>
    {
        Information("Starting Build");
        MSBuild(solutionPath, settings => {
            settings.WithTarget("Rebuild");
            settings.SetConfiguration(configuration);
        });
        Information("Ending Build");
    });

var tests = Task("Tests")
    .IsDependentOn("Configuration")
    .Does(()=>
    {
        Information("Starting Tests");
        var testSettings = new VSTestSettings{
            ToolPath = Context.Tools.Resolve("vstest.console.exe"),
            ArgumentCustomization = arg => arg.Append("/logger:trx;LogFileName=TestResults.xml")
        };
        if(testsdllFilePath!=null)
            VSTest(testsdllFilePath, testSettings);
        Information("Ending Tests");
    });


var packageNuspecFile = Task("Package")
    .IsDependentOn("Configuration")
    .Does(()=>
    {
        Information("Starting Package");
        
        if(csprojFilePath != null){
            var settings = new DotNetCorePackSettings();
            settings.Configuration = configuration;
            if(packageRootPath.EndsWithIgnoreCase("\\") || packageRootPath.EndsWithIgnoreCase("/"))
                settings.OutputDirectory =  packageRootPath+@"artifacts\";
            else
                settings.OutputDirectory =  packageRootPath+@"\artifacts\";
            DotNetCorePack(csprojFilePath,settings);
        }
         
        Information("Ending Package");
    });

Task("Default")
    .IsDependentOn("Package");

RunTarget(target);
