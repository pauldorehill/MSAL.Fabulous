namespace FSharp.Azure.MSAL

open Microsoft.Identity.Client
open System.Security.Cryptography
open System.IO

type ClientId = 
    | ClientId of string
    member this.Value = match this with | ClientId x -> x

type TenantId =
    | TenantId of string
    member this.Value = match this with | TenantId x -> x

[<RequireQualifiedAccess>]
type WPFCache =
    | Default
    | NoCache
    | CustomLocation of FileInfo
    | Custom of TokenCache

[<RequireQualifiedAccess>]
type ApplicationPlatform = 
    | Android of UIParent
    | IOS
    | UWP
    | WPF of WPFCache

type Authentication =
    { PublicClientApp : PublicClientApplication 
      Platform : ApplicationPlatform }

type SignInStatus =
    | SignedIn of Authentication * AuthenticationResult
    | SignedOut of Authentication

module Authentication =
    
    let private getUserCache (cacheLocation : FileInfo option) =

        let defaultFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location + ".msalcache.bin" |> FileInfo
        let cacheFilePath = Option.defaultValue defaultFilePath cacheLocation

        // Not locking here since the method in TokencacheExtensions locks the cache (TokenCache.LockObject is internal)
        // https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/blob/master/src/Microsoft.Identity.Client/Features/PublicClientWithTokenCache/TokenCacheExtensions.cs
        let beforeAccessNotification (args : TokenCacheNotificationArgs) =
            if File.Exists cacheFilePath.FullName then
                // These only work on windows, but this will only run on windows
                try
                    ProtectedData.Unprotect(File.ReadAllBytes cacheFilePath.FullName, null, DataProtectionScope.CurrentUser)
                    |> args.TokenCache.Deserialize
                with :? CryptographicException ->
                    // If unable to decrypt, delete the file
                    File.Delete cacheFilePath.FullName
            else ()

        let afterAccessNotification (args : TokenCacheNotificationArgs) =
            if args.HasStateChanged
            then
                ProtectedData.Protect(args.TokenCache.Serialize(), null, DataProtectionScope.CurrentUser)
                |> fun bs -> File.WriteAllBytes(cacheFilePath.FullName, bs)
            else ()

        let userTC = TokenCache()
        userTC.SetBeforeAccess(TokenCache.TokenCacheNotification beforeAccessNotification)
        userTC.SetAfterAccess(TokenCache.TokenCacheNotification afterAccessNotification)
        userTC

    let internal getAccounts (publicClientApp : PublicClientApplication) =
        async {
            let! token = 
                publicClientApp.GetAccountsAsync()
                |> Async.AwaitTask
                |> Async.StartChild
            
            let! accounts = token
            return Seq.tryExactlyOne accounts
        }
    
    // If an empty list is passed an exception will be thrown
    let private validateScopes scopes = if List.isEmpty scopes then [""] else scopes
    
    let internal silentAuth (publicClientApp : PublicClientApplication) account (scopes : string list) =
        let scopes = validateScopes scopes
        async {
            let! token =
                publicClientApp.AcquireTokenSilentAsync(scopes, account)
                |> Async.AwaitTask
                |> Async.StartChild
            return! token
        }

    let internal getAuthentication (uIParent : UIParent option) (publicClientApp : PublicClientApplication) (scopes : string list) =
        let scopes = validateScopes scopes
        async {
            let nonSilentAuth () =
                async {
                    let! token =
                        match uIParent with
                        | Some uip -> publicClientApp.AcquireTokenAsync(scopes, uip)
                        | None -> publicClientApp.AcquireTokenAsync scopes
                        |> Async.AwaitTask
                        |> Async.StartChild
                    return! token
                }

            match! getAccounts publicClientApp with
            | Some account ->
                try 
                    return! silentAuth publicClientApp account scopes
                with 
                | :? MsalUiRequiredException -> return! nonSilentAuth ()

            | None -> return! nonSilentAuth ()
        }

    let internal signOut (publicClientApp : PublicClientApplication) =
        async {
            match! getAccounts publicClientApp with
            | Some account ->
                let! token =
                    publicClientApp.RemoveAsync account
                    |> Async.AwaitTask
                    |> Async.StartChild
                return! token
            | None -> return ()
        }

    let internal create platform (ClientId clientId) (TenantId tenantId) =
        let tennant = @"https://login.microsoftonline.com/" + tenantId
        // Only WPF needs to implement a user defined cache
        // https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/token-cache-serialization
        let pca =
            match platform with
            | ApplicationPlatform.WPF cache -> 
                match cache with
                | WPFCache.Custom custom -> PublicClientApplication(clientId, tennant, custom)
                | WPFCache.CustomLocation file -> PublicClientApplication(clientId, tennant, getUserCache (Some file))
                | WPFCache.Default -> PublicClientApplication(clientId, tennant, getUserCache None)
                | WPFCache.NoCache -> PublicClientApplication(clientId, tennant)
            
            | _ -> PublicClientApplication(clientId, tennant)
        
        { PublicClientApp = pca
          Platform = platform }

module SignInStatus =

    let create platform clientId tenantId scopes =
        let authentication = Authentication.create platform clientId tenantId
        // Try to see if can sign in silently first
        async {
            match! Authentication.getAccounts authentication.PublicClientApp with
            | Some account ->
                try 
                    let! r = Authentication.silentAuth authentication.PublicClientApp account scopes
                    return SignedIn (authentication, r)
                with 
                    | :? MsalUiRequiredException -> return SignedOut authentication
            | None ->
                return SignedOut authentication
        }

type SignInStatus with
    member this.SignIn scopes =
        match this with
        | SignedIn _ -> async { return this }
        | SignedOut auth ->
            match auth.Platform with
            | ApplicationPlatform.Android uIParent ->
                async {
                    let! authResult = Authentication.getAuthentication (Some uIParent) auth.PublicClientApp scopes
                    return SignedIn (auth, authResult)
                }
            | _ -> 
                async {
                    let! authResult = Authentication.getAuthentication None auth.PublicClientApp scopes
                    return SignedIn (auth, authResult)
                }

    member this.SignOut () = 
        match this with
        | SignedIn (auth, _) -> 
            async { 
                do! Authentication.signOut auth.PublicClientApp 
                return SignedOut auth
            }

        | SignedOut _ -> async { return this }