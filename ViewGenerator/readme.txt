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

2) Create the typescript of your component. 
	The properties and behaviors interfaces must follow the following naming conventions start with I and end with Properties or Behaviors suffix.
	Don't forget to export default your component.
	Any types present in the IProperties or IBehaviors inetrfaces must be defined on the entry module.
	Behavior methods cannot return any values ie. their return type must be void.
	All types must be exported in order for its C# binding be generated.

	Eg:

	import * as React from "react";
	import "lib/third-party-lib";
	import "./LocalModule";
	import "css!styles/styles.css";

	export interface ISomeType {
		name: string;
	}

	export interface IExampleProperties {
		click(arg: ISomeType): void;
	}

	export interface IExampleBehaviors {
		callMe(): void;
	}

	export default class Example extends React.Component<IExampleProperties, {}> implements IExampleBehaviors {

	    callMe(): void {
			alert("hey");
		}

		render() {
			return <button onClick={() => this.props.click(null)}>Click me!</button>
		}
	}


3) Build the project and run.


Development Notes:
------------------

A) To import css's use the following instruction:

	import "css!styles/<name-of-the-css-file.css>";


B) To import 3rd party modules, place the module (must be AMD) javascript under the View\lib folder and the Typescript definition file (d.ts) under the node_modules\lib\<name-of-the-3rd-party-lib-module>:

	import "lib/<name-of-the-3rd-party-lib-module>";


C) To import local modules:

	import "./LocalModule";


