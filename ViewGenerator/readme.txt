*****************
* ViewGenerator *
*****************

Generates the .Net bindings to the React Web views based on the typescript declarations.

How to use:
-----------

1) Create a file on the root of your project named ts2lang.json with the following content:

	{
		"tasks": [
			{
				"input": "<directory-of-the-ui-files>",
				"output": "<directory-to-place-the-generated-files>",
				"template": "",
				"parameters": {
					"namespace": "<namespace-of-your-project>",
					"baseComponentClass": "<base-component-classname>",
					"javascriptDistPath": "<path-of-the-generated-js-files>"
				}
			}
		]
	}

2) You may customize the following parameters in the ts2lang.json file:

	- <directory-of-the-ui-files> 
		Path where the declaration of the UI (typescript) files live.
		eg: "View/src/*.tsx"

	- <directory-to-place-the-generated-files> 
		Path where the UI generated files will be written
		eg: "Generated/"

	- <namespace-of-your-project> 
		Namespace to be used in the generated C# files
		eg: "MyAwesomeProject"

	- <base-component-classname>
		Name of the base class of the UI component. Use this if you need to inherit from a base component other than the default.
		eg: "MyCustomBaseControl"

	- <path-of-the-generated-js-files>
		Place where the typescript compiler will place the compiled js files. Defaults to "dist/"

3) Create the typescript files of your UI. 
	Eg:

	import * as React from 'react';
	import "css!../css/styles.css";

	interface IExampleProperties {
		click(): void;
	}

	interface IExampleBehaviors {
		callMe(): void;
	}

	class Example extends React.Component<IExampleProperties, {}> implements IExampleBehaviors {

		render() {
			return <Button click={() => this.props.click()}>Click me!</Button>
		}
	}

5) Build the project.

6) Include the all the js generated files in the project as Embedded Resources.

7) Build again and run.


Notes:
------

- This tools uses nodejs. If you need to use a different nodejs version you can customize the path of the node executable. 
Edit you csproj file and search for the following:

  <Target Name="EnsureNuGetPackageBuildImports" BeforeTargets="PrepareForBuild">

Add the following snippet to your csproj file:

  <Target Name="EnsureNuGetPackageBuildImports" BeforeTargets="PrepareForBuild">
    <PropertyGroup>
      <NodeJsExe>$(ProjectDir)CustomNodeJs\node.exe</NodeJsExe>
    </PropertyGroup>

