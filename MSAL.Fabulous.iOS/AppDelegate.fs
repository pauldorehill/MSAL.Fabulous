// Copyright 2018 Fabulous contributors. See LICENSE.md for license.
namespace MSAL.Fabulous.iOS

open System
open UIKit
open Foundation
open Xamarin.Forms
open Xamarin.Forms.Platform.iOS
open FSharp.Azure.MSAL
open Microsoft.Identity.Client

[<Register ("AppDelegate")>]
type AppDelegate () =
    inherit FormsApplicationDelegate ()

    override this.FinishedLaunching (app, options) =
        Forms.Init()
        let appcore = new MSAL.Fabulous.App(ApplicationPlatform.IOS)
        this.LoadApplication (appcore)
        base.FinishedLaunching(app, options)

    override this.OpenUrl(app, url, options) =
        AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs url

module Main =
    [<EntryPoint>]
    let main args =
        UIApplication.Main(args, null, "AppDelegate")
        0