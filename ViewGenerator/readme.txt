*****************
* ViewGenerator *
*****************

Generates the .Net bindings to the React Web views based on the typescript declarations.

Notes:
------

This tools uses nodejs to generate the .Net bindings. If you need to use a different nodejs version you can customize the path of the node executable. 
Edit your .csproj file and search for the following:

  <Target Name="EnsureNuGetPackageBuildImports" BeforeTargets="PrepareForBuild">

Add the following snippet to your csproj file with the path of the nodejs executable:

  <Target Name="EnsureNuGetPackageBuildImports" BeforeTargets="PrepareForBuild">
    <PropertyGroup>
      <NodeJsExe>$(ProjectDir)CustomNodeJs\node.exe</NodeJsExe>
    </PropertyGroup>


How to use:
-----------

1) Change the ts2lang.json file on the root of your project to match your needs:

{
    "tasks": [
        {
            "input": Path where the declaration of the UI (typescript) files live,
            "output": Path where the generated C# class files will be written,
            "template": Leave empty,
            "parameters": {
                "namespace": Namespace to be used in the generated C# classes,
                "baseComponentClass": Name of the base class of the UI component. Use this if you need to inherit from a base component other than the default,
				"javascriptDistPath": Place where the typescript compiler will place the compiled js files
            }
        }
    ]
}

2) Create the typescript files of your UI. 
	Eg:

	import * as React from "react";
	import "css!../styles/styles.css";

	interface IExampleProperties {
		click(): void;
	}

	interface IExampleBehaviors {
		callMe(): void;
	}

	class Example extends React.Component<IExampleProperties, {}> implements IExampleBehaviors {

	    callMe(): void {
			alert("hey");
		}

		render() {
			return <button onClick={() => this.props.click()}>Click me!</button>
		}
	}

3) Build the project and run.


