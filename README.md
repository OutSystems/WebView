# WebView

WebView is a WPF control that wraps [CefSharp](https://github.com/cefsharp/CefSharp) web view control. Provides the following additional features:
- Strongly-typed javascript evaluation: results of javascript evaluation returns the appropriate type
- Scripts are aggregated and executed in bulk for improved performance
- Synchronous javascript evaluation
- Javascript error handling with stack information
- Events to intercept and respond to resource load
- Events to track file download progress
- Ability to load embedded resources using a custom protocol
- Ability to disable history navigation
- Error handling
- Proxy configuration support
- Runs under AnyCPU configuration (works both on x64 and x86 configurations)

# Build pre-requisites
- NodeJS

# ViewPacker

We have a node package, _**node-sass**_ which creates a folder of vendors for each OS. However, to generate these vendors, the following command needs to run in each OS:
Inside ViewPacker with node_modules installed, the user should run `node scripts/install.js` which will generate a new vendor folder inside `node_modules/node_sass/vendor/` for the user's selected OS. This new vendor folder (e.g. `darwin-x64-72/`) and its **binding.node** content file shall then be added to ViewPacker's `build/node-sass-vendors/`.

For now we are using the following node versions:
- 10.16.3
- 12.18.4

**Note:** If you wish to upgrade the node version at use, don't forget to delete/replace any previous bindings related to the OS (`darwin-x64-72/` represents node 12.X.X's version).

###################
