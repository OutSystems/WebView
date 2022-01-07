namespace WebViewControl.Avalonia.FuncUI.Sample

module Counter =
    open Avalonia.Controls
    open Avalonia.FuncUI.DSL
    open WebViewControl
    
    type State = { address : string; toBrowse : string  }
    let init =
        WebView.Settings.OsrEnabled <- false
        let defaultAddress = "https://github.com"
        { address = defaultAddress; toBrowse = defaultAddress }

    type Msg =
        | AddressChanged of string
        | NavigateToAddress

    let update (msg: Msg) (state: State) : State =
        match msg with
        | AddressChanged url -> { state with address = url }
        | NavigateToAddress -> { state with toBrowse = state.address }
    
    let view (state: State) (dispatch) =
        DockPanel.create [
            DockPanel.children [
                TextBox.create [
                    TextBox.dock Dock.Top
                    TextBox.onTextChanged (fun s -> dispatch (AddressChanged s) )
                    TextBox.text state.address
                    TextBox.fontSize 20.0
                ]
                Button.create [
                    Button.dock Dock.Top
                    Button.onClick (fun _ -> dispatch NavigateToAddress)
                    Button.content "Run"
                    Button.fontSize 20.0
                ]
                WebView.create [
                    WebView.dock Dock.Top
                    WebView.address state.toBrowse
                ]
            ]
        ]