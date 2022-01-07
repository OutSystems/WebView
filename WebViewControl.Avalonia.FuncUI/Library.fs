module WebViewControl

open Avalonia.FuncUI.Builder
open Avalonia.FuncUI.Types
open WebViewControl

let private stringsEqual(a : obj, b : obj) =
    (a :?> string) = (b :?> string)

type WebView with
    static member create(attrs : IAttr<WebView> list): IView<WebView> =
        ViewBuilder.Create<WebView>(attrs)

    static member address(address : string) =
        AttrBuilder<WebView>.CreateProperty<string>(WebView.AddressProperty, address, stringsEqual |> ValueOption<_>.Some)