namespace MSAL.Fabulous

open Fabulous.Core
open Fabulous.DynamicViews
open Xamarin.Forms
open FSharp.Azure.MSAL

module App =

    type Model = 
      { SignInStatus : SignInStatus }

    type Msg = 
        | MsgSignIn
        | MsgSignOut
        | MsgSignInStatusChange of SignInStatus

    // Register your app following these guidelines
    // https://docs.microsoft.com/en-us/azure/active-directory/develop/quickstart-v2-uwp
    let clientId = ClientId "Enter_the_Application_Id_here"
    let tenantId = TenantId "Enter_the_Tenant_Info_Here"

    // https://docs.microsoft.com/en-us/azure/active-directory/develop/v2-permissions-and-consent
    // https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/Acquiring-tokens-interactively#how-to-get-consent-for-several-resources
    let scopes = ["insert your scopes here"]

    let initModel platform = 
        // No GUI running yet so can call here blocking
        let signInStatus = 
            SignInStatus.create platform clientId tenantId scopes 
            |> Async.RunSynchronously

        { SignInStatus = signInStatus }

    let init platform () = initModel platform, Cmd.none

    let signIn (model : Model) =
        async { 
            let! s = model.SignInStatus.SignIn scopes
            return MsgSignInStatusChange s
        }
        |> Cmd.ofAsyncMsg

    let signOut (model : Model) =
        async {
            let! s = model.SignInStatus.SignOut ()
            return MsgSignInStatusChange s
        }
        |> Cmd.ofAsyncMsg

    let update msg (model : Model) =
        match msg with
        | MsgSignIn -> model, signIn model
        | MsgSignOut -> model, signOut model
        | MsgSignInStatusChange ss -> { model with SignInStatus = ss }, Cmd.none

    let view (model: Model) dispatch =
        View.ContentPage(
          content = View.StackLayout(padding = 20.0, verticalOptions = LayoutOptions.Center,
            children = 
                match model.SignInStatus with
                | SignInStatus.SignedIn (_, authResult) ->
                    [ View.Label(text = "Hello " + authResult.Account.Username)
                      View.Button(text = "Sign Out" , command = (fun () -> dispatch MsgSignOut))]
                | SignInStatus.SignedOut _ ->
                    [ View.Button(text = "Sign In" , command = (fun () -> dispatch MsgSignIn))]
            ))
    let program platform = Program.mkProgram (init platform) update view

type App (platform : ApplicationPlatform) as app =
    inherit Application ()

    let runner = 
        App.program platform
#if DEBUG
        |> Program.withConsoleTrace
#endif
        |> Program.runWithDynamicView app