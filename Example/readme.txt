Example Project structure:
    ->Example View: resources for the ReactView, such as images, stylesheet files and the typescript files for each page (combined with the respective C# adapter code). 
	  The .ts ans .tsx (tsx files are TypeScript files which have JSX embed) files will be the contents of our React Views. These files will be used (defined in ts2lang.json) 
	  to later generate C# bindings as well.
    
	->App.config: C#.NET project file for defining configuration files for our application. This file will be copied to the compilation directory, 
	  with the same name as our executable. In our case, we are only specifying which .NET Framework version our application supports by means of the “supportedRuntime” tag.
    
	->App.xaml: C#.NET project file for definition of our entry window.
    
	->ExtendedReactView.cs and ExtendedReactViewFactory.cs: these files define some extra properties for our React view and will be used in the ExampleView.Generated.cs 
	  (look in the Generated folder after the build). The generation of this ExampleView.Generated.cs file with a reference to the ExtendedReactView.cs was achieved by using 
	  the ts2lang.json file, where it was defined that the baseComponentClass for our ExampleView would be Example.ExtendedReactView.
    
	->MainWindow.xaml: Defines our main view and inside it contains the C# logic
    
	->packages.config: NuGet file for defining the dependencies referenced by our project. This file allows Nuget to easily restore the project’s dependencies when the project 
	  is to be transported to a different machine.
   
   ->ReactViewExamples.xaml:  component that serves our ReactViewExample view. Inside, you have the C#  logic for interaction with our MainWindow.xaml
    
	->ts2lang.json: read ViewGenerator/readme.txt. This is basically the file defining which TypeScript modules will use nodejs to generate .Net bindings.
    
	->tsconfig.json: file containing additional options for the Typescript compiler. The presence of a tsconfig.json file in a directory indicates that the directory is the 
	  root of a TypeScript project. For more details follow this link.
		
		-compilerOptions: For more details follow this link.
			
			+module: specify module code generation. “Amd” stands for Asynchronous Module Definition and its API specifies a mechanism for defining modules such that the 
			module and its dependencies can be asynchronously loaded.
			
			+jsx: enables support of JSX in .tsx files. The “react” mode will emit  React.createElement, does not need to go through a JSX transformation before use, and
			the output will have a .js file extension.
			
			+moduleResolution: strategy for locating files representing imported modules. “node” strategy tries to mimic the Node.js module resolution mechanism at runtime. 
			
			+sourceMap: “false” means that the compiler only emits .js files and not source maps, which are used to debug.
			
			+allowJs: allow Javascript files to be compiled.
			
			+noImplicitReturns: raise error when not all code paths in function return a value.
			
			+noImplicitAny: raise error on expressions and declarations with an implied any type.
			
			+supressImplicitAnyIndexErrors: suppress --noImplicitAny errors for indexing objects lacking index signatures. More details.
		
		-compileOnSave: This  property can be used to direct the IDE to automatically compile the included TypeScript files and generate the output.
		
		-exclude: files/packages specified here won’t be compiled.
    
	->WebViewExample.xaml: component that defines our WebViewExample view. Inside you can see the C# logic for interaction with our MainWindow.xaml
