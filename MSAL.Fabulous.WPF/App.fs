namespace MSAL.Fabulous.WPF

open System
open Xamarin.Forms
open Xamarin.Forms.Platform.WPF
open FSharp.Azure.MSAL

type MainWindow() = 
    inherit FormsApplicationPage()

module Main = 
    [<EntryPoint>]
    [<STAThread>]
    let main _ =
        let app = System.Windows.Application()
        Forms.Init()
        let window = MainWindow() 
        window.LoadApplication(MSAL.Fabulous.App(ApplicationPlatform.WPF WPFCache.Default))
        app.Run window