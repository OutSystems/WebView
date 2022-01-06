module WebViewControl

open Avalonia.FuncUI.Builder
open Avalonia.FuncUI.Types
open WebViewControl

type WebView with
    static member create(attrs : IAttr<WebView> list): IView<WebView> =
        ViewBuilder.Create<WebView>(attrs)

    static member address(address : string) =
        AttrBuilder<WebView>.CreateProperty<string>(WebView.AddressProperty, address, (fun (a : obj, b : obj) -> a.Equals(b)) |> ValueOption<_>.Some)