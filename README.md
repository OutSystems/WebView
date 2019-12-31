# dotNetron

Build cross platform desktop apps with React.js and .Net.

dotNetron is a framework that provides a shell for wrapping desktop apps built with web-technologies (such as react.js, javascript and css). Via the dotNetron Javascript interop bridge it is possible to invoke APIs from .Net and vice-versa.
Powered by [.Net Core](https://dotnet.microsoft.com/download), [React.js](https://reactjs.org/), [CefGlue](https://gitlab.com/joaompneves/cefglue) and [Avalonia](https://avaloniaui.net/), dotNetron apps can run on Windows, Linux and Mac using the same code base.

This framework is inspired on the [Electron](https://github.com/electron/electron) concept. Similarly to Electron, this framework provides a native shell with a browser (CefGlue) with the react.js library embedded, out-of-the-box.

Contrary to [Electron.Net](https://github.com/ElectronNET/Electron.NET), dotNetron does not use an embedded ASP.Net core server. Instead, .Net code runs all on the same process, and all the UI resources are fetched automatically by the browser and served directly from the application bundle. This approach improves the performance of the apps, while leveraging the web-techonologies capabilities for building UIs. 

## Features

dotNetron provides tooling and libraries for building web-based UIs that can easily interop with .Net APIs, in the same process.
With dotNetron you can
- build react.js UIs
- use typescript (which enables the companion tooling to generate strongly-typed APIs)
- style with SASS
- call .Net APIs from javascript in an asynchronous fashion
- call javascript APIs in a strongly-typed fashion
- SASS support
- pass information (simple or more complex types) from .Net to javascript and vice-versa (the js interop bridge takes care of the data serialization) 
- seamly use images and other UI resources on the react.js UIs

## Usage

### Bootstrap
You start by creating a .Net Core project. Then add the dotNetron Nuget packages.

// TODO

### Configuration

Change the ts2lang.json file on the root of your project to match your needs:

	{
		"tasks": [
			{
				"input": Path where the declaration of the UI (typescript) files live,
				"output": Path where the generated C# class files will be written to,
				"template": Leave empty,
				"parameters": {
					"namespace": Namespace to be used in the generated C# classes,
					"baseComponentClass": Name of the base class of the UI component. Use this if you need to inherit from a base component other than the default,
					"javascriptDistPath": Place where the typescript compiler will place the compiled js files
				}
			}
		]
	}

### Hello World

Create the entry typescript (.tsx) module that will host your application UI. Check out the following simple example.

	import * as React from "react";
	import "styles.sass";

	export interface IHelloWorldProperties {
		click(arg: string): void;
	}

	export interface IHelloWorldBehaviors {
		sayHello(): void;
	}

	export default class HelloWorld extends React.Component<IHelloWorldProperties, {}> implements IHelloWorldBehaviors {

	    sayHello(): void {
			alert("Hello World");
		}

		render() {
			return <button onClick={() => this.props.click("hello from js")}>Click me!</button>
		}
	}

#### Properties and Behaviors

The Hello World example contains 2 interfaces: IHelloWorldProperties and IHelloWorldBehaviors.
These interfaces are special, in way that enables the dotNetron tooling to generate the .Net APIs for the interop between javascript and .Net. 
The Properties interface defines the .Net APIs that will be available to javascript, while the Behaviors interface defines the methods javascript methods that can be called from .Net. 
The properties and behaviors interfaces must follow the following naming conventions: start with I and end with Properties or Behaviors suffix.
You can use other types in these interfaces, as long as they are declared in the same module. All types must be exported in order for its C# APIs be generated.
Methods in the Properties interface must either be void or Promise.
Methods in the Behaviors interface cannot return any values ie. their return type must be void.

#### React component

The component is a standard react component that should implement the Properties interface. The Behaviors interface is optional.
Don't forget to export default your component.

### Generated C# code

TODO

### Wrapping all

TODO

